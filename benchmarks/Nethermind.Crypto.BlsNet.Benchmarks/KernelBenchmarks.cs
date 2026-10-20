using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static unsafe partial class TowerBenchmarks
{
    private static Fp2 _fp2Sink;
    private static FpWide _wideSink;

    internal static void RunKernels(bool nativeAvailable)
    {
        NativeAvailable = nativeAvailable;
        Fp2 a = Field2(), b = Field2();
        string[] names = ["Fp multiply", "Fp square", "Fp2 multiply", "Fp2 square", "Fp inverse",
            "MultiplyWide", "ReduceWide", "ReduceWideSmall", "AddWide", "SubtractWide", "AddUnreduced", "Halve", "Negate", "SubtractSumComposite",
            "Fp add", "Fp subtract", "MultiplyComposite", "ReduceWidePositive", "ReduceWideDifference", "Fp2 add", "Fp2 subtract", "Fp2 mul by 3", "Fp2 mul by 4", "Fp2 mul by 8", "MultiplyWideNoAlias", "ReduceWideSmallLate", "ReduceWideSmallRow5", "ReduceWideSmallHoisted", "ReduceWideSmallSignedHoisted"];
        Fp.MultiplyWide(out FpWide x, a.C0, b.C0);
        Fp.MultiplyWide(out FpWide y, a.C1, b.C1);
        Console.WriteLine("Operation | managed ns | blst ns | managed bytes");
        for (int op = 0; op < names.Length; op++)
        {
            if (!BenchmarkOptions.Matches(names[op])) continue;
            int count = op == 4 ? 30000 : 1000000;
            if (BenchmarkOptions.Quick) count /= 10;
            double managed = MeasureKernel(op, false, count, a, b, x, y, out long allocated);
            bool hasReference = NativeAvailable && (op < 5 || op is 12 or 14 or 15 or 16 or 19 or 20 or 21 or 22 or 23 ||
                NativeKernelReference.Available && op is 5 or 6 or 7 or 17 or 18 or 24 or 25 or 26 or 27 or 28);
            double native = hasReference ? MeasureKernel(op, true, count, a, b, x, y, out _) : double.NaN;
            Console.WriteLine($"{names[op]} | {managed:F2} | {native:F2} | {allocated}");
        }
    }

    private static double MeasureKernel(int op, bool native, int count, in Fp2 a, in Fp2 b, in FpWide x, in FpWide y, out long allocated)
    {
        long warmup = Stopwatch.GetTimestamp();
        do { RunKernel(op, native, count, a, b, x, y); }
        while (Stopwatch.GetTimestamp() - warmup < Stopwatch.Frequency / 2);
        long best = long.MaxValue;
        allocated = 0;
        for (int round = 0; round < 7; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            RunKernel(op, native, count, a, b, x, y);
            long elapsed = Stopwatch.GetTimestamp() - start;
            if (elapsed < best) { best = elapsed; allocated = GC.GetAllocatedBytesForCurrentThread() - before; }
        }
        return best * 1e9 / Stopwatch.Frequency / count;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunKernel(int op, bool native, int count, in Fp2 a, in Fp2 b, in FpWide x, in FpWide y)
    {
        Fp r = default;
        Fp2 r2 = default;
        FpWide wide = default;
        if (native)
        {
            switch (op)
            {
                case 0: for (int i = 0; i < count; i++) KernelNative.blst_fp_mul(out r, a.C0, b.C0); break;
                case 1: for (int i = 0; i < count; i++) KernelNative.blst_fp_sqr(out r, a.C0); break;
                case 2: for (int i = 0; i < count; i++) KernelNative.blst_fp2_mul(out r2, a, b); break;
                case 3: for (int i = 0; i < count; i++) KernelNative.blst_fp2_sqr(out r2, a); break;
                case 4: for (int i = 0; i < count; i++) KernelNative.blst_fp_inverse(out r, a.C0); break;
                case 5: case 24: for (int i = 0; i < count; i++) NativeKernelReference.Multiply(out wide, a.C0, b.C0); break;
                case 6: case 7: case 17: case 25: case 26: case 27: case 28: for (int i = 0; i < count; i++) NativeKernelReference.Reduce(out r, x); break;
                case 18: for (int i = 0; i < count; i++) NativeKernelReference.SubtractReduce(out r, x, y); break;
                case 12: for (int i = 0; i < count; i++) KernelNative.blst_fp_cneg(out r, a.C0, 1); break;
                case 14: for (int i = 0; i < count; i++) KernelNative.blst_fp_add(out r, a.C0, b.C0); break;
                case 15: for (int i = 0; i < count; i++) KernelNative.blst_fp_sub(out r, a.C0, b.C0); break;
                case 16: for (int i = 0; i < count; i++) KernelNative.blst_fp_mul(out r, a.C0, b.C0); break;
                case 19: for (int i = 0; i < count; i++) KernelNative.blst_fp2_add(out r2, a, b); break;
                case 20: for (int i = 0; i < count; i++) KernelNative.blst_fp2_sub(out r2, a, b); break;
                case 21: for (int i = 0; i < count; i++) KernelNative.blst_fp2_mul_by_3(out r2, a); break;
                case 22: for (int i = 0; i < count; i++) KernelNative.blst_fp2_lshift(out r2, a, 2); break;
                case 23: for (int i = 0; i < count; i++) KernelNative.blst_fp2_mul_by_8(out r2, a); break;
            }
        }
        else
        {
            switch (op)
            {
                case 0: RunFpMultiply(count, a.C0, b.C0, out r); break;
                case 1: RunFpSquare(count, a.C0, out r); break;
                case 2: for (int i = 0; i < count; i++) Fp2.Multiply(out r2, a, b); break;
                case 3: for (int i = 0; i < count; i++) Fp2.Square(out r2, a); break;
                case 4: for (int i = 0; i < count; i++) Fp.Inverse(out r, a.C0); break;
                case 5: for (int i = 0; i < count; i++) Fp.MultiplyWide(out wide, a.C0, b.C0); break;
                case 6: for (int i = 0; i < count; i++) Fp.ReduceWide(out r, x); break;
                case 7: for (int i = 0; i < count; i++) Fp.ReduceWideSmall(out r, x); break;
                case 8: for (int i = 0; i < count; i++) Fp.AddWide(out wide, x, y); break;
                case 9: for (int i = 0; i < count; i++) Fp.SubtractWide(out wide, x, y); break;
                case 10: for (int i = 0; i < count; i++) Fp.AddUnreduced(out r, a.C0, b.C0); break;
                case 11: for (int i = 0; i < count; i++) Fp.Halve(out r, a.C0); break;
                case 12: for (int i = 0; i < count; i++) Fp.Negate(out r, a.C0); break;
                case 13: for (int i = 0; i < count; i++) Fp.SubtractSumComposite(out r, a.C0, a.C1, b.C0); break;
                case 14: for (int i = 0; i < count; i++) Fp.Add(out r, a.C0, b.C0); break;
                case 15: for (int i = 0; i < count; i++) Fp.Subtract(out r, a.C0, b.C0); break;
                case 16: for (int i = 0; i < count; i++) Fp.MultiplyComposite(out r, a.C0, b.C0); break;
                case 17: RunPositiveKernel(count, x, out r); break;
                case 18: RunDifferenceKernel(count, x, y, out r); break;
                case 19: for (int i = 0; i < count; i++) Fp2.Add(out r2, a, b); break;
                case 20: for (int i = 0; i < count; i++) Fp2.Subtract(out r2, a, b); break;
                case 21: for (int i = 0; i < count; i++) Fp2.MulBy3(out r2, a); break;
                case 22: for (int i = 0; i < count; i++) Fp2.MulBy4(out r2, a); break;
                case 23: for (int i = 0; i < count; i++) Fp2.MulBy8(out r2, a); break;
                case 24: RunDisjointKernel(count, a.C0, b.C0, out wide); break;
                case 25: for (int i = 0; i < count; i++) Fp.ReduceWideSmallLateArm64(out r, x); break;
                case 26: for (int i = 0; i < count; i++) Fp.ReduceWideSmallRow5Arm64(out r, x); break;
                case 27: for (int i = 0; i < count; i++) Fp.ReduceWideSmallHoistedArm64(out r, x); break;
                case 28: for (int i = 0; i < count; i++) Fp.ReduceWideSmallSignedHoistedArm64(out r, x); break;
            }
        }
        _fpSink = r; _fp2Sink = r2; _wideSink = wide;
    }

    // Give each large inline kernel its own JIT budget. Combining two CIOS
    // kernels and all the add/subtract cases in one switch changes codegen.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunFpMultiply(int count, in Fp a, in Fp b, out Fp r)
    {
        r = default;
        for (int i = 0; i < count; i++) Fp.Multiply(out r, a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunFpSquare(int count, in Fp a, out Fp r)
    {
        r = default;
        for (int i = 0; i < count; i++) Fp.Square(out r, a);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunPositiveKernel(int count, in FpWide x, out Fp r)
    {
        r = default;
        for (int i = 0; i < count; i++) Fp.ReduceWidePositive(out r, x);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunDifferenceKernel(int count, in FpWide x, in FpWide y, out Fp r)
    {
        r = default;
        for (int i = 0; i < count; i++) Fp.ReduceWideDifference(out r, x, y);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RunDisjointKernel(int count, in Fp a, in Fp b, out FpWide r)
    {
        r = default;
        for (int i = 0; i < count; i++) Fp.MultiplyWideNoAlias(out r, a, b);
    }

}
