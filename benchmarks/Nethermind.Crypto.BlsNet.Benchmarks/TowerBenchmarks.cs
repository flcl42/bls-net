using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Pair = Nethermind.Crypto.BlsNet.Pairing.Pairing;
using static TowerInterop;

internal static unsafe partial class TowerBenchmarks
{
    private static bool NativeAvailable;
    private static readonly Random Random = new(2537);
    private static readonly ulong[] PMinus2 = [0xb9feffffffffaaa9, 0x1eabfffeb153ffff, 0x6730d2a0f6b0f624, 0x64774b84f38512bf, 0x4b1ba7b6434bacd7, 0x1a0111ea397fe69a];
    private static Fp12 _sink;
    private static Fp _fpSink;
    private static bool _boolSink;
    private static Fp Field()
    {
        Span<byte> bytes = stackalloc byte[48];
        do { Random.NextBytes(bytes); bytes[0] &= 0x1f; } while (!Fp.TryFromBigEndian(bytes, out _));
        Fp.TryFromBigEndian(bytes, out Fp f); return f;
    }
    private static Fp2 Field2() => new(Field(), Field());
    private static Fp6 Field6() => new() { C0 = Field2(), C1 = Field2(), C2 = Field2() };
    private static Fp12 Field12() => new() { C0 = Field6(), C1 = Field6() };
    private static void Points(out G1Affine p, out G2Affine q, ulong a = 0, ulong b = 0)
    {
        Span<byte> scalar = stackalloc byte[32];
        if (a == 0) Random.NextBytes(scalar); else System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(scalar, a);
        if (NativeAvailable) { fixed (byte* s = scalar) { Native.blst_p1_mult(out P1 x, Native.blst_p1_generator(), s, 256); Native.blst_p1_to_affine(out p, x); } }
        else { G1Projective.Multiply(G1Affine.Generator, scalar, out G1Projective x); p = x.ToAffine(); }
        scalar.Clear();
        if (b == 0) Random.NextBytes(scalar); else System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(scalar, b);
        if (NativeAvailable) { fixed (byte* s = scalar) { Native.blst_p2_mult(out P2 x, Native.blst_p2_generator(), s, 256); Native.blst_p2_to_affine(out q, x); } }
        else { G2Projective.Multiply(G2Affine.Generator, scalar, out G2Projective x); q = x.ToAffine(); }
    }
    internal static void Run(bool nativeAvailable)
    {
        NativeAvailable = nativeAvailable;
        Fp12 a = Field12(), b = Field12(); Fp fp = Field();
        G1Affine[] ps = new G1Affine[8]; G2Affine[] qs = new G2Affine[8];
        for (int i = 0; i < 8; i++) Points(out ps[i], out qs[i]);
        Pair.MillerLoop(ps.AsSpan(0, 1), qs.AsSpan(0, 1), out Fp12 f);
        Pair.FinalExponentiation(f, out Fp12 cyclo);
        string[] names = ["Fp inverse", "Fp inverse Fermat", "Fp12 multiply", "Fp12 square", "Cyclotomic square", "Miller 1", "Miller 8", "Final exponentiation", "Pairing check 1", "Pairing check 2", "Pairing check 8", "Fp12 sparse multiply", "Fp12 two sparse multiplies"];
        Console.WriteLine("Operation | managed ns | native ns | managed bytes");
        for (int op = 0; op < names.Length; op++)
        {
            if (!BenchmarkOptions.Matches(names[op])) continue;
            bool tower = op <= 4 || op >= 11;
            int iterations = BenchmarkOptions.Quick ? (tower ? 1000 : 10) : (tower ? 30000 : 250);
            double m = Measure(op, false, iterations, fp, a, b, cyclo, f, ps, qs, out long allocated);
            double n = !NativeAvailable || op == 1 ? double.NaN : Measure(op, true, iterations, fp, a, b, cyclo, f, ps, qs, out _);
            Console.WriteLine($"{names[op]} | {m:F2} | {n:F2} | {allocated}");
        }
    }
    private static double Measure(int op, bool native, int iterations, in Fp fp, in Fp12 a, in Fp12 b, in Fp12 cyclo, in Fp12 f, G1Affine[] ps, G2Affine[] qs, out long allocated)
    {
        long warmupStart = Stopwatch.GetTimestamp();
        do { Run(op, native, iterations, fp, a, b, cyclo, f, ps, qs); }
        while (Stopwatch.GetTimestamp() - warmupStart < Stopwatch.Frequency / 2);
        long best = long.MaxValue; allocated = 0;
        for (int round = 0; round < (!BenchmarkOptions.Quick ? 7 : 21); round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            Run(op, native, iterations, fp, a, b, cyclo, f, ps, qs);
            long elapsed = Stopwatch.GetTimestamp() - start, bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (elapsed < best) { best = elapsed; allocated = bytes; }
        }
        return best * 1e9 / Stopwatch.Frequency / iterations;
    }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Run(int op, bool native, int iterations, in Fp fp, in Fp12 a, in Fp12 b, in Fp12 cyclo, in Fp12 f, G1Affine[] ps, G2Affine[] qs)
    {
        Fp12 r = default; Fp fr = default;
        G1Affine** pp = stackalloc G1Affine*[8]; G2Affine** qq = stackalloc G2Affine*[8];
        fixed (G1Affine* px = ps) fixed (G2Affine* qx = qs)
        {
            for (int i = 0; i < 8; i++) { pp[i] = px + i; qq[i] = qx + i; }
            if (native)
            {
                switch (op)
                {
                    case 0: for (int i = 0; i < iterations; i++) Native.blst_fp_inverse(out fr, fp); break;
                    case 2: for (int i = 0; i < iterations; i++) Native.blst_fp12_mul(out r, a, b); break;
                    case 3: for (int i = 0; i < iterations; i++) Native.blst_fp12_sqr(out r, a); break;
                    case 4: for (int i = 0; i < iterations; i++) Native.blst_fp12_cyclotomic_sqr(out r, cyclo); break;
                    case 5: for (int i = 0; i < iterations; i++) Native.blst_miller_loop(out r, qs[0], ps[0]); break;
                    case 6: for (int i = 0; i < iterations; i++) Native.blst_miller_loop_n(out r, qq, pp, 8); break;
                    case 7: for (int i = 0; i < iterations; i++) Native.blst_final_exp(out r, f); break;
                    case 11: for (int i = 0; i < iterations; i++) Native.blst_fp12_mul_by_xy00z0(out r, a, b.C0); break;
                    case 12: for (int i = 0; i < iterations; i++) { Native.blst_fp12_mul_by_xy00z0(out r, a, b.C0); Native.blst_fp12_mul_by_xy00z0(out r, r, b.C1); } break;
                    default:
                        nuint count = op == 8 ? 1u : op == 9 ? 2u : 8u;
                        for (int i = 0; i < iterations; i++) { Native.blst_miller_loop_n(out r, qq, pp, count); Native.blst_final_exp(out r, r); _boolSink = r.IsOne; }
                        break;
                }
            }
            else
            {
                switch (op)
                {
                    case 0: for (int i = 0; i < iterations; i++) Fp.Inverse(out fr, fp); break;
                    case 1: for (int i = 0; i < iterations; i++) Fp.Pow(out fr, fp, PMinus2); break;
                    case 2: for (int i = 0; i < iterations; i++) Fp12.Multiply(out r, a, b); break;
                    case 3: for (int i = 0; i < iterations; i++) Fp12.Square(out r, a); break;
                    case 4: for (int i = 0; i < iterations; i++) Fp12.CyclotomicSquare(out r, cyclo); break;
                    case 5: for (int i = 0; i < iterations; i++) Pair.MillerLoop(ps.AsSpan(0, 1), qs.AsSpan(0, 1), out r); break;
                    case 6: for (int i = 0; i < iterations; i++) Pair.MillerLoop(ps, qs, out r); break;
                    case 7: for (int i = 0; i < iterations; i++) Pair.FinalExponentiation(f, out r); break;
                    case 11: for (int i = 0; i < iterations; i++) Fp12.MultiplyBySparse(out r, a, b.C0); break;
                    case 12: for (int i = 0; i < iterations; i++) SparseTower.MultiplyPair(out r, a, b.C0, b.C1); break;
                    default:
                        int count = op == 8 ? 1 : op == 9 ? 2 : 8;
                        for (int i = 0; i < iterations; i++) _boolSink = Pair.PairingCheck(ps.AsSpan(0, count), qs.AsSpan(0, count));
                        break;
                }
            }
        }
        _sink = r; _fpSink = fr;
    }
}
