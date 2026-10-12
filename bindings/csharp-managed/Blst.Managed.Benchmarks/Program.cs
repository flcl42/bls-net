using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Blst.Managed.Arithmetic;

internal static class Program
{
    private const int Iterations = 1_000_000;
    private const int MeasurementRounds = 7;

    public static void Main()
    {
        Console.WriteLine($"Iterations: {Iterations:N0}, rounds: {MeasurementRounds}");
        BenchmarkManagedFr();
        BenchmarkManagedFp();
        BenchmarkNativeIfAvailable();
    }

    private static void BenchmarkManagedFr()
    {
        Fr a = Fr.FromUInt64(0x1234_5678_9abc_def0UL);
        Fr b = Fr.FromUInt64(0xfedc_ba98_7654_3210UL);
        Fr acc = Fr.One;

        Measure("managed Fr mul+add", Iterations, () =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                Fr.Multiply(out acc, acc, a);
                Fr.Add(out acc, acc, b);
            }
        });

        GC.KeepAlive(acc);
    }

    private static void BenchmarkManagedFp()
    {
        Fp a = Fp.FromUInt64(0x1234_5678_9abc_def0UL);
        Fp b = Fp.FromUInt64(0xfedc_ba98_7654_3210UL);
        Fp acc = Fp.One;

        Measure("managed Fp mul+add", Iterations, () =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                Fp.Multiply(out acc, acc, a);
                Fp.Add(out acc, acc, b);
            }
        });

        GC.KeepAlive(acc);
    }

    private static void BenchmarkNativeIfAvailable()
    {
        try
        {
            NativeBlst.Touch();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Console.WriteLine("native BLST benchmark skipped: run bindings/csharp-managed/build-native-blst.ps1 to build blst.dll.");
            return;
        }

        NativeBlst.BenchmarkFr(Iterations);
        NativeBlst.BenchmarkFp(Iterations);
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
        static NativeBlst()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ResolveBlst);
        }

        public static void Touch()
        {
            FrNative ret = default;
            ulong* one = stackalloc ulong[4];
            one[0] = 1;
            blst_fr_from_uint64(&ret, one);
        }

        public static void BenchmarkFr(int iterations)
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
        }

        public static void BenchmarkFp(int iterations)
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
        }

        private static void RunFr(int iterations, FrNative* acc, FrNative* a, FrNative* b)
        {
            for (int i = 0; i < iterations; i++)
            {
                blst_fr_mul(acc, acc, a);
                blst_fr_add(acc, acc, b);
            }
        }

        private static void RunFp(int iterations, FpNative* acc, FpNative* a, FpNative* b)
        {
            for (int i = 0; i < iterations; i++)
            {
                blst_fp_mul(acc, acc, a);
                blst_fp_add(acc, acc, b);
            }
        }

        private static IntPtr ResolveBlst(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, "blst.dll", StringComparison.OrdinalIgnoreCase))
            {
                return IntPtr.Zero;
            }

            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "blst.dll");
                if (File.Exists(candidate))
                {
                    return NativeLibrary.Load(candidate);
                }
            }

            return IntPtr.Zero;
        }

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fr_from_uint64(FrNative* ret, ulong* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fr_mul(FrNative* ret, FrNative* left, FrNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fr_add(FrNative* ret, FrNative* left, FrNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fp_from_uint64(FpNative* ret, ulong* value);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void blst_fp_mul(FpNative* ret, FpNative* left, FpNative* right);

        [DllImport("blst.dll", CallingConvention = CallingConvention.Cdecl)]
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
