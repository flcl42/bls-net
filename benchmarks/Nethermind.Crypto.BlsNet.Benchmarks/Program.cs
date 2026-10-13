using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static class Program
{
    private const int Iterations = 1_000_000;
    private const int MeasurementRounds = 7;

    public static void Main()
    {
        Console.WriteLine($"{RuntimeInformation.FrameworkDescription}, iterations: {Iterations:N0}, rounds: {MeasurementRounds}");
        Fr fr = BenchmarkManagedFr();
        Fp fp = BenchmarkManagedFp();
        BenchmarkNativeIfAvailable(fr, fp);
    }

    private static Fr BenchmarkManagedFr()
    {
        Fr a = Fr.FromUInt64(0x1234_5678_9abc_def0UL);
        Fr b = Fr.FromUInt64(0xfedc_ba98_7654_3210UL);
        Fr acc = Fr.One;

        Measure("managed Fr mul+add", Iterations, () => acc = RunFr(Iterations, acc, a, b));

        return acc;
    }

    // The measured loops keep the accumulator in a local, not in a field of the lambda's closure, and
    // read the operands through references like the native loops below.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fr RunFr(int iterations, Fr acc, in Fr a, in Fr b)
    {
        for (int i = 0; i < iterations; i++)
        {
            Fr.Multiply(out acc, acc, a);
            Fr.Add(out acc, acc, b);
        }

        return acc;
    }

    private static Fp BenchmarkManagedFp()
    {
        Fp a = Fp.FromUInt64(0x1234_5678_9abc_def0UL);
        Fp b = Fp.FromUInt64(0xfedc_ba98_7654_3210UL);
        Fp acc = Fp.One;

        Measure("managed Fp mul+add", Iterations, () => acc = RunFp(Iterations, acc, a, b));

        return acc;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Fp RunFp(int iterations, Fp acc, in Fp a, in Fp b)
    {
        for (int i = 0; i < iterations; i++)
        {
            Fp.Multiply(out acc, acc, a);
            Fp.Add(out acc, acc, b);
        }

        return acc;
    }

    // Both sides run the same number of iterations from the same values, so the final
    // accumulators must agree; this checks the measured managed loops.
    private static void BenchmarkNativeIfAvailable(Fr managedFr, Fp managedFp)
    {
        if (!NativeBlstLoader.TryLoad(out nint handle)) return;
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) => name == "blst.dll" ? handle : 0);
        try
        {
            NativeBlst.Touch();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Console.WriteLine("native BLST benchmark skipped: run scripts/build-native-blst.ps1 to build blst.dll.");
            return;
        }

        NativeBlst.BenchmarkFr(Iterations, managedFr);
        NativeBlst.BenchmarkFp(Iterations, managedFp);
    }

    private static void Measure(string name, int iterations, Action body)
    {
        body();
        body();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long bestTicks = long.MaxValue;
        long bestAllocated = 0;
        for (int i = 0; i < MeasurementRounds; i++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestampBefore = Stopwatch.GetTimestamp();
            body();
            long timestampAfter = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            long ticks = timestampAfter - timestampBefore;

            if (ticks < bestTicks)
            {
                bestTicks = ticks;
                bestAllocated = allocated;
            }
        }

        double elapsedSeconds = bestTicks / (double)Stopwatch.Frequency;
        double nsPerIteration = elapsedSeconds * 1_000_000_000.0 / iterations;
        Console.WriteLine($"{name}: {nsPerIteration:N2} ns/iteration, {bestAllocated:N0} B allocated");
    }

    private static void PrintNativeResult(string name, int iterations, long ticks, long allocated)
    {
        double elapsedSeconds = ticks / (double)Stopwatch.Frequency;
        double nsPerIteration = elapsedSeconds * 1_000_000_000.0 / iterations;
        Console.WriteLine($"{name}: {nsPerIteration:N2} ns/iteration, {allocated:N0} B allocated");
    }

    private static unsafe class NativeBlst
    {
        public static void Touch()
        {
            FrNative ret = default;
            ulong* one = stackalloc ulong[4];
            one[0] = 1;
            blst_fr_from_uint64(&ret, one);
        }

        public static void BenchmarkFr(int iterations, Fr expected)
        {
            FrNative a = default;
            FrNative b = default;
            FrNative acc = default;
            ulong* limbs = stackalloc ulong[4];

            limbs[0] = 0x1234_5678_9abc_def0UL;
            blst_fr_from_uint64(&a, limbs);
            limbs[0] = 0xfedc_ba98_7654_3210UL;
            blst_fr_from_uint64(&b, limbs);
            limbs[0] = 1;
            blst_fr_from_uint64(&acc, limbs);

            RunFr(iterations, &acc, &a, &b);
            RunFr(iterations, &acc, &a, &b);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long bestTicks = long.MaxValue;
            long bestAllocated = 0;
            for (int i = 0; i < MeasurementRounds; i++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long timestampBefore = Stopwatch.GetTimestamp();
                RunFr(iterations, &acc, &a, &b);
                long timestampAfter = Stopwatch.GetTimestamp();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                long ticks = timestampAfter - timestampBefore;

                if (ticks < bestTicks)
                {
                    bestTicks = ticks;
                    bestAllocated = allocated;
                }
            }

            PrintNativeResult("native C Fr mul+add", iterations, bestTicks, bestAllocated);

            ulong* canonical = stackalloc ulong[4];
            blst_uint64_from_fr(canonical, &acc);
            Span<byte> bytes = stackalloc byte[Fr.ByteCount];
            expected.ToLittleEndian(bytes);
            if (!bytes.SequenceEqual(new ReadOnlySpan<byte>(canonical, Fr.ByteCount)))
            {
                throw new InvalidOperationException("Managed and native Fr mul+add results differ.");
            }
        }

        public static void BenchmarkFp(int iterations, Fp expected)
        {
            FpNative a = default;
            FpNative b = default;
            FpNative acc = default;
            ulong* limbs = stackalloc ulong[6];

            limbs[0] = 0x1234_5678_9abc_def0UL;
            blst_fp_from_uint64(&a, limbs);
            limbs[0] = 0xfedc_ba98_7654_3210UL;
            blst_fp_from_uint64(&b, limbs);
            limbs[0] = 1;
            blst_fp_from_uint64(&acc, limbs);

            RunFp(iterations, &acc, &a, &b);
            RunFp(iterations, &acc, &a, &b);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long bestTicks = long.MaxValue;
            long bestAllocated = 0;
            for (int i = 0; i < MeasurementRounds; i++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long timestampBefore = Stopwatch.GetTimestamp();
                RunFp(iterations, &acc, &a, &b);
                long timestampAfter = Stopwatch.GetTimestamp();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                long ticks = timestampAfter - timestampBefore;

                if (ticks < bestTicks)
                {
                    bestTicks = ticks;
                    bestAllocated = allocated;
                }
            }

            PrintNativeResult("native C Fp mul+add", iterations, bestTicks, bestAllocated);

            ulong* canonical = stackalloc ulong[6];
            blst_uint64_from_fp(canonical, &acc);
            Span<byte> bytes = stackalloc byte[Fp.ByteCount];
            expected.ToLittleEndian(bytes);
            if (!bytes.SequenceEqual(new ReadOnlySpan<byte>(canonical, Fp.ByteCount)))
            {
                throw new InvalidOperationException("Managed and native Fp mul+add results differ.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void RunFr(int iterations, FrNative* acc, FrNative* a, FrNative* b)
        {
            for (int i = 0; i < iterations; i++)
            {
                blst_fr_mul(acc, acc, a);
                blst_fr_add(acc, acc, b);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void RunFp(int iterations, FpNative* acc, FpNative* a, FpNative* b)
        {
            for (int i = 0; i < iterations; i++)
            {
                blst_fp_mul(acc, acc, a);
                blst_fp_add(acc, acc, b);
            }
        }

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fr_from_uint64(FrNative* ret, ulong* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blst_fr_mul(FrNative* ret, FrNative* left, FrNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blst_fr_add(FrNative* ret, FrNative* left, FrNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fp_from_uint64(FpNative* ret, ulong* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_uint64_from_fr(ulong* ret, FrNative* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_uint64_from_fp(ulong* ret, FpNative* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blst_fp_mul(FpNative* ret, FpNative* left, FpNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blst_fp_add(FpNative* ret, FpNative* left, FpNative* right);

        private struct FrNative
        {
            public fixed ulong Limbs[4];
        }

        private struct FpNative
        {
            public fixed ulong Limbs[6];
        }
    }
}
