using System.Diagnostics;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe class SecretBenchmarks
{
    internal static void Run(bool native)
    {
        Fp u = G1Affine.Generator.X; Fp2 u2 = new(u, Fp.One);
        MapToCurve.MapToG1(u, out G1Projective p); MapToCurve.MapToG2(u2, out G2Projective q);
        G1Affine a = p.ToAffine(); G2Affine b = q.ToAffine();
        byte[] scalar = new byte[32]; new Random(0x4354).NextBytes(scalar);
        Fr fr = Fr.FromLittleEndianConstantTime(scalar);
        // The native exports dispatch to GLV/GLS only for canonical nonzero scalars.
        // Keep the same canonical scalar in all three timed multiplication paths.
        fr.ToLittleEndianConstantTime(scalar);
        Console.WriteLine("secret_operation,constant_time_us,variable_time_us,blst_us");
        Measure("CT Fp inverse", () => Fp.InverseConstantTime(out _, u), () => Fp.Inverse(out _, u), () => KernelNative.blst_fp_inverse(out _, u), native);
        Measure("CT Fp2 inverse", () => Fp2.InverseConstantTime(out _, u2), () => Fp2.Inverse(out _, u2), () => KernelNative.blst_fp2_inverse(out _, u2), native);
        Measure("CT Fr inverse", () => Fr.InverseConstantTime(out _, fr), null, () => KernelNative.blst_fr_inverse(out _, fr), native);
        Measure("CT G1 multiply", () => G1Projective.MultiplyConstantTime(a, scalar, out _), () => G1Projective.Multiply(a, scalar, out _), () => { fixed (byte* s = scalar) CurveNative.blst_p1_mult(out _, p, s, 256); }, native);
        Measure("CT G2 multiply", () => G2Projective.MultiplyConstantTime(b, scalar, out _), () => G2Projective.Multiply(b, scalar, out _), () => { fixed (byte* s = scalar) CurveNative.blst_p2_mult(out _, q, s, 256); }, native);
        Measure("CT G1 to-affine", () => _ = p.ToAffineConstantTime(), () => _ = p.ToAffine(), () => CurveNative.blst_p1_to_affine(out _, p), native);
        Measure("CT G2 to-affine", () => _ = q.ToAffineConstantTime(), () => _ = q.ToAffine(), () => CurveNative.blst_p2_to_affine(out _, q), native);
        Measure("CT map G1", () => MapToCurve.MapToG1ConstantTime(u, out _), () => MapToCurve.MapToG1(u, out _), () => CurveNative.blst_map_to_g1(out _, u, null), native);
        Measure("CT map G2", () => MapToCurve.MapToG2ConstantTime(u2, out _), () => MapToCurve.MapToG2(u2, out _), () => CurveNative.blst_map_to_g2(out _, u2, null), native);
    }

    private static void Measure(string name, Action ct, Action? vt, Action blst, bool native)
    {
        if (!BenchmarkOptions.Matches(name)) return;
        long start = Stopwatch.GetTimestamp();
        do { ct(); vt?.Invoke(); if (native) blst(); } while (Stopwatch.GetElapsedTime(start).TotalSeconds < 0.5);
        double Time(Action action)
        {
            long calibration = Stopwatch.GetTimestamp(); for (int i = 0; i < 10; i++) action();
            double seconds = Stopwatch.GetElapsedTime(calibration).TotalSeconds / 10;
            int count = Math.Clamp((int)(0.01 / seconds), 1, 100000);
            double best = double.PositiveInfinity;
            for (int round = 0; round < 21; round++)
            {
                long before = Stopwatch.GetTimestamp(); for (int i = 0; i < count; i++) action();
                best = Math.Min(best, Stopwatch.GetElapsedTime(before).TotalMicroseconds / count);
            }
            return best;
        }
        Console.WriteLine($"{name},{Time(ct):F3},{(vt is null ? double.NaN : Time(vt)):F3},{(native ? Time(blst) : double.NaN):F3}");
    }
}
