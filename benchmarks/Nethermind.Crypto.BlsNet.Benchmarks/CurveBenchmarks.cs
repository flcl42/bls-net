using System.Diagnostics;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class CurveBenchmarks
{
    private static bool NativeAvailable;
    private static readonly Random Rng = new(0x2537);
    private static Fp RandomFp()
    {
        Span<byte> bytes = stackalloc byte[48];
        do { Rng.NextBytes(bytes); bytes[0] &= 31; } while (!Fp.TryFromBigEndian(bytes, out _));
        Fp.TryFromBigEndian(bytes, out Fp r); return r;
    }
    private static Fp2 RandomFp2() => new(RandomFp(), RandomFp());

    private static void Measure(string name, int iterations, Action managed, Action native)
    {
        if (!BenchmarkOptions.Matches(name)) return;
        bool shortRounds = BenchmarkOptions.Quick;
        if (shortRounds)
        {
            for (int i = 0; i < 10; i++) managed();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 10; i++) managed();
            double perOperation = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency / 10;
            double target = 3 / 1000;
            iterations = Math.Min(iterations, Math.Max(1, (int)(target / perOperation)));
        }
        long warmupStart = Stopwatch.GetTimestamp();
        do
        {
            for (int i = 0; i < iterations; i++) managed();
            if (NativeAvailable)
            {
                for (int i = 0; i < iterations; i++) native();
            }
        }
        while (Stopwatch.GetTimestamp() - warmupStart < Stopwatch.Frequency / 2);
        double MeasureOne(Action action)
        {
            double best = double.MaxValue;
            for (int round = 0; round < (shortRounds ? 21 : 7); round++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++) action();
                double us = (Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency / iterations;
                best = Math.Min(best, us);
            }
            return best;
        }
        Console.WriteLine($"{name},{MeasureOne(managed):F3},{(NativeAvailable ? MeasureOne(native) : double.NaN):F3}");
    }
    internal static void Run(bool nativeAvailable)
    {
        NativeAvailable = nativeAvailable;
        Console.WriteLine("operation,managed_us,blst_us");
        byte[] scalar = new byte[32]; Rng.NextBytes(scalar);
        {
            Fp u = RandomFp(); MapToCurve.MapToG1(u, out G1Projective p);
            G1Affine affine = p.ToAffine(); G1Projective g = G1Projective.Generator;
            Measure("G1 add+affine", 2000,
                () => { G1Projective.Add(p, g, out G1Projective r); _ = r.ToAffine(); },
                () => { CurveNative.blst_p1_add_or_double(out G1Projective r, p, g); CurveNative.blst_p1_to_affine(out _, r); });
            Measure("G1 subgroup", 2000, () => _ = affine.IsInSubgroup(), () => _ = CurveNative.blst_p1_affine_in_g1(affine));
            Measure("G1 multiply", 1000, () => G1Projective.Multiply(affine, scalar, out _),
                () => { fixed (byte* sp = scalar) CurveNative.blst_p1_mult(out _, p, sp, 256); });
            Measure("map G1", 1000, () => MapToCurve.MapToG1(u, out _), () => CurveNative.blst_map_to_g1(out _, u, null));
            foreach (int count in MsmSizes) BenchmarkMsm1(count);
        }
        {
            Fp2 u = RandomFp2(); MapToCurve.MapToG2(u, out G2Projective p);
            G2Affine affine = p.ToAffine(); G2Projective g = G2Projective.Generator;
            Measure("G2 add+affine", 2000,
                () => { G2Projective.Add(p, g, out G2Projective r); _ = r.ToAffine(); },
                () => { CurveNative.blst_p2_add_or_double(out G2Projective r, p, g); CurveNative.blst_p2_to_affine(out _, r); });
            Measure("G2 subgroup", 2000, () => _ = affine.IsInSubgroup(), () => _ = CurveNative.blst_p2_affine_in_g2(affine));
            Measure("G2 multiply", 1000, () => G2Projective.Multiply(affine, scalar, out _),
                () => { fixed (byte* sp = scalar) CurveNative.blst_p2_mult(out _, p, sp, 256); });
            Measure("map G2", 1000, () => MapToCurve.MapToG2(u, out _), () => CurveNative.blst_map_to_g2(out _, u, null));
            foreach (int count in MsmSizes) BenchmarkMsm2(count);
        }
    }
    private static readonly int[] MsmSizes = Environment.GetEnvironmentVariable("BLST_MSM_SIZES") is string sizes
        ? Array.ConvertAll(sizes.Split(','), int.Parse)
        : BenchmarkOptions.Quick ? [1, 128] : [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096];
    private static int MsmIterations(int count) => Math.Max(1, Math.Min(100, 4096 / count));
    private static void BenchmarkMsm1(int count)
    {
        G1Affine[] points = new G1Affine[count]; byte[] scalars = new byte[count * 32]; Rng.NextBytes(scalars);
        for (int i = 0; i < count; i++) { MapToCurve.MapToG1(RandomFp(), out G1Projective p); points[i] = p.ToAffine(); }
        G1Affine** ps = stackalloc G1Affine*[count]; byte** ss = stackalloc byte*[count];
        ulong* scratch = NativeAvailable ? (ulong*)NativeMemory.Alloc(CurveNative.blst_p1s_mult_pippenger_scratch_sizeof((nuint)count)) : null;
        try
        {
            fixed (G1Affine* pp = points) fixed (byte* sp = scalars)
            {
                for (int i = 0; i < count; i++) { ps[i] = pp + i; ss[i] = sp + i * 32; }
                Measure($"G1 MSM {count}", MsmIterations(count),
                    () => G1Projective.MultiScalarMultiply(points, scalars, out _),
                    () => CurveNative.blst_p1s_mult_pippenger(out _, ps, (nuint)count, ss, 256, scratch));
            }
        }
        finally { NativeMemory.Free(scratch); }
    }
    private static void BenchmarkMsm2(int count)
    {
        G2Affine[] points = new G2Affine[count]; byte[] scalars = new byte[count * 32]; Rng.NextBytes(scalars);
        for (int i = 0; i < count; i++) { MapToCurve.MapToG2(RandomFp2(), out G2Projective p); points[i] = p.ToAffine(); }
        G2Affine** ps = stackalloc G2Affine*[count]; byte** ss = stackalloc byte*[count];
        ulong* scratch = NativeAvailable ? (ulong*)NativeMemory.Alloc(CurveNative.blst_p2s_mult_pippenger_scratch_sizeof((nuint)count)) : null;
        try
        {
            fixed (G2Affine* pp = points) fixed (byte* sp = scalars)
            {
                for (int i = 0; i < count; i++) { ps[i] = pp + i; ss[i] = sp + i * 32; }
                Measure($"G2 MSM {count}", MsmIterations(count),
                    () => G2Projective.MultiScalarMultiply(points, scalars, out _),
                    () => CurveNative.blst_p2s_mult_pippenger(out _, ps, (nuint)count, ss, 256, scratch));
            }
        }
        finally { NativeMemory.Free(scratch); }
    }
}
