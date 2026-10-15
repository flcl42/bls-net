using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static class MulAddBenchmarks
{
    private static Fr _frSink;
    private static Fp _fpSink;

    internal static void Run(bool nativeAvailable)
    {
        int count = BenchmarkOptions.Quick ? 100_000 : 1_000_000;
        Fr a = Fr.FromUInt64(0x1234_5678_9abc_def0UL), b = Fr.FromUInt64(0xfedc_ba98_7654_3210UL);
        Fp x = Fp.FromUInt64(0x1234_5678_9abc_def0UL), y = Fp.FromUInt64(0xfedc_ba98_7654_3210UL);
        if (BenchmarkOptions.Matches("Fr mul+add"))
        {
            if (nativeAvailable && RunFr(count, Fr.One, a, b) != RunNativeFr(count, Fr.One, a, b))
                throw new InvalidOperationException("Managed and native Fr mul+add results differ.");
            Measure("Fr mul+add", count, nativeAvailable,
                () => _frSink = RunFr(count, Fr.One, a, b), () => _frSink = RunNativeFr(count, Fr.One, a, b));
        }
        if (BenchmarkOptions.Matches("Fp mul+add"))
        {
            if (nativeAvailable && RunFp(count, Fp.One, x, y) != RunNativeFp(count, Fp.One, x, y))
                throw new InvalidOperationException("Managed and native Fp mul+add results differ.");
            Measure("Fp mul+add", count, nativeAvailable,
                () => _fpSink = RunFp(count, Fp.One, x, y), () => _fpSink = RunNativeFp(count, Fp.One, x, y));
        }
    }

    private static void Measure(string name, int count, bool native, Action managed, Action reference)
    {
        double One(Action loop)
        {
            long warm = Stopwatch.GetTimestamp();
            do { loop(); } while (Stopwatch.GetTimestamp() - warm < Stopwatch.Frequency / 2);
            long best = long.MaxValue;
            for (int round = 0; round < 7; round++)
            {
                long start = Stopwatch.GetTimestamp();
                loop();
                best = Math.Min(best, Stopwatch.GetTimestamp() - start);
            }
            return best * 1e9 / Stopwatch.Frequency / count;
        }
        Console.WriteLine($"{name} | managed {One(managed):F2} ns | native {(native ? One(reference) : double.NaN):F2} ns");
    }

    // Separate managed/native methods keep the original inline kernels' JIT budgets.
    // Each side keeps the dependent accumulator local and reads operands by reference.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fr RunFr(int count, Fr acc, in Fr a, in Fr b)
    {
        for (int i = 0; i < count; i++) { Fr.Multiply(out acc, acc, a); Fr.Add(out acc, acc, b); }
        return acc;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fr RunNativeFr(int count, Fr acc, in Fr a, in Fr b)
    {
        for (int i = 0; i < count; i++) { KernelNative.blst_fr_mul(out acc, acc, a); KernelNative.blst_fr_add(out acc, acc, b); }
        return acc;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fp RunFp(int count, Fp acc, in Fp a, in Fp b)
    {
        for (int i = 0; i < count; i++) { Fp.Multiply(out acc, acc, a); Fp.Add(out acc, acc, b); }
        return acc;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fp RunNativeFp(int count, Fp acc, in Fp a, in Fp b)
    {
        for (int i = 0; i < count; i++) { KernelNative.blst_fp_mul(out acc, acc, a); KernelNative.blst_fp_add(out acc, acc, b); }
        return acc;
    }
}
