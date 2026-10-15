using System.Diagnostics;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Hashing;

// Deliberately opt-in: randomized fixed-vs-random experiment, not a stable CI assertion.
internal static class TimingLeakTests
{
    private sealed class Moments
    {
        internal int Count;
        internal double Mean, M2;
        internal void Add(double value)
        { Count++; double delta = value - Mean; Mean += delta / Count; M2 += delta * (value - Mean); }
        internal double Variance => M2 / (Count - 1);
    }

    internal static void Run(int samples)
    {
        if (samples < 1000 || samples > 10_000_000) throw new ArgumentOutOfRangeException(nameof(samples));
        Console.WriteLine($"Timing experiment: {samples} samples per class, randomized balanced order; threshold |t|=4.5.");
        Console.WriteLine("operation,fixed_class,n_fixed,n_random,mean_fixed_ns,mean_random_ns,welch_t,trimmed_t,positive_control_detected");
        G1Affine p = G1Affine.Generator; G2Affine q = G2Affine.Generator;
        bool controlsPass = true;
        foreach (string fixedClass in new[] { "zero", "one", "low-weight" })
        {
            controlsPass &= Measure("G1 variable", fixedClass, samples, s => G1Projective.Multiply(p, s, out _), true);
            if (BenchmarkOptions.Matches("CT G1 multiply")) Measure("G1 constant", fixedClass, samples, s => G1Projective.MultiplyConstantTime(p, s, out _), false);
            controlsPass &= Measure("G2 variable", fixedClass, samples, s => G2Projective.Multiply(q, s, out _), true);
            if (BenchmarkOptions.Matches("CT G2 multiply")) Measure("G2 constant", fixedClass, samples, s => G2Projective.MultiplyConstantTime(q, s, out _), false);
            if (BenchmarkOptions.Matches("CT map G1"))
                Measure("map G1 constant", fixedClass, samples, FieldInput, a => MapToCurve.MapToG1ConstantTime(a, out _), false, 48);
            if (BenchmarkOptions.Matches("CT map G2"))
                Measure("map G2 constant", fixedClass, samples, ComplexInput, a => MapToCurve.MapToG2ConstantTime(a, out _), false, 96);
        }
        foreach (string fixedClass in new[] { "zero", "one", "two", "minus-one", "power-of-two", "low-weight" })
        {
            // This remains a mandatory positive control when testing inverses.
            controlsPass &= Measure("Fp inverse variable", fixedClass, samples, FieldInput, a => Fp.Inverse(out _, a), true, 48);
            if (BenchmarkOptions.Matches("CT Fp inverse"))
                Measure("Fp inverse constant", fixedClass, samples, FieldInput, a => Fp.InverseConstantTime(out _, a), false, 48);
            if (BenchmarkOptions.Matches("CT Fp2 inverse"))
                Measure("Fp2 inverse constant", fixedClass, samples, ComplexInput, a => Fp2.InverseConstantTime(out _, a), false, 96);
            if (BenchmarkOptions.Matches("CT Fr inverse"))
                Measure("Fr inverse constant", fixedClass, samples, bytes => Fr.FromLittleEndianConstantTime(bytes), a => Fr.InverseConstantTime(out _, a), false, 32);
            if (BenchmarkOptions.Matches("CT G1 to-affine"))
                Measure("G1 affine constant", fixedClass, samples, bytes => Rescale(FieldInput(bytes)), a => _ = a.ToAffineConstantTime(), false, 48);
            if (BenchmarkOptions.Matches("CT G2 to-affine"))
                Measure("G2 affine constant", fixedClass, samples, bytes => Rescale(ComplexInput(bytes)), a => _ = a.ToAffineConstantTime(), false, 96);
        }
        if (!controlsPass) throw new InvalidOperationException("Timing positive control did not detect leakage; the experiment is inconclusive. Increase samples or reduce host noise.");
    }

    private static bool Measure(string name, string fixedClass, int count, Action<byte[]> action, bool control)
        => Measure(name, fixedClass, count, bytes => bytes, action, control, 32);

    // Convert before timing, so parsing, canonicalization and allocation cannot affect a class.
    private static Fp FieldInput(byte[] bytes)
    {
        // Preserve canonical fixed inputs, including p-1. Invalid random inputs
        // are folded into [0,2^380) before timing.
        if (!Fp.TryFromLittleEndian(bytes, out Fp field))
        { bytes[^1] &= 0x0f; if (!Fp.TryFromLittleEndian(bytes, out field)) throw new InvalidOperationException(); }
        return field;
    }

    private static Fp2 ComplexInput(byte[] bytes)
        => new(FieldInput(bytes.AsSpan(0, 48).ToArray()), FieldInput(bytes.AsSpan(48).ToArray()));

    private static G1Projective Rescale(Fp z)
    {
        Fp.Square(out Fp z2, z); Fp.Multiply(out Fp z3, z2, z);
        Fp.Multiply(out Fp x, G1Affine.Generator.X, z2); Fp.Multiply(out Fp y, G1Affine.Generator.Y, z3);
        return new(x, y, z);
    }

    private static G2Projective Rescale(Fp2 z)
    {
        Fp2.Square(out Fp2 z2, z); Fp2.Multiply(out Fp2 z3, z2, z);
        Fp2.Multiply(out Fp2 x, G2Affine.Generator.X, z2); Fp2.Multiply(out Fp2 y, G2Affine.Generator.Y, z3);
        return new(x, y, z);
    }

    private static bool Measure<T>(string name, string fixedClass, int count, Func<byte[], T> prepare, Action<T> action, bool control, int byteCount)
    {
        Random random = new(0x4354);
        byte[] fixedScalar = new byte[byteCount];
        if (fixedClass == "one") fixedScalar[0] = 1;
        if (fixedClass == "two") fixedScalar[0] = 2;
        if (fixedClass == "power-of-two") fixedScalar[15] = 1;
        if (fixedClass == "low-weight") { fixedScalar[0] = 1; fixedScalar[15] = 1; fixedScalar[31] = 0x40; }
        if (fixedClass == "minus-one")
        {
            byte[] minusOne = Convert.FromHexString(byteCount == 32
                ? "73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000000"
                : "1a0111ea397fe69a4b1ba7b6434bacd764774b84f38512bf6730d2a0f6b0f6241eabfffeb153ffffb9feffffffffaaaa");
            Array.Reverse(minusOne); minusOne.CopyTo(fixedScalar, 0);
            if (byteCount == 96) minusOne.CopyTo(fixedScalar, 48);
        }
        T[] inputs = new T[count * 2];
        bool[] classes = new bool[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            classes[i] = i < count;
            // Both classes own distinct buffers, preventing class-correlated cache reuse.
            byte[] bytes = new byte[byteCount];
            if (i < count) fixedScalar.CopyTo(bytes, 0); else random.NextBytes(bytes);
            inputs[i] = prepare(bytes);
        }
        for (int i = inputs.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1); (inputs[i], inputs[j]) = (inputs[j], inputs[i]);
            (classes[i], classes[j]) = (classes[j], classes[i]);
        }
        for (int i = 0; i < 2000; i++) action(inputs[i % inputs.Length]);
        double[] times = new double[inputs.Length];
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        for (int i = 0; i < inputs.Length; i++)
        {
            T input = inputs[i];
            long start = Stopwatch.GetTimestamp(); action(input); long end = Stopwatch.GetTimestamp();
            times[i] = (end - start) * (1e9 / Stopwatch.Frequency);
        }
        Moments[] stats = [new(), new()], trimmed = [new(), new()];
        double[] sorted = (double[])times.Clone(); Array.Sort(sorted);
        // Common, class-independent upper cutoff; retain raw t as well. No class-specific trimming.
        double cutoff = sorted[(int)(sorted.Length * 0.99)];
        for (int i = 0; i < times.Length; i++)
        { int c = classes[i] ? 0 : 1; stats[c].Add(times[i]); if (times[i] <= cutoff) trimmed[c].Add(times[i]); }
        static double Statistic(Moments[] s) => (s[0].Mean - s[1].Mean) / Math.Sqrt(s[0].Variance / s[0].Count + s[1].Variance / s[1].Count);
        double t = Statistic(stats), tt = Statistic(trimmed);
        bool detected = Math.Abs(t) > 4.5 || Math.Abs(tt) > 4.5;
        Console.WriteLine($"{name},{fixedClass},{stats[0].Count},{stats[1].Count},{stats[0].Mean:F1},{stats[1].Mean:F1},{t:F3},{tt:F3},{(control ? detected.ToString() : "n/a")}");
        return detected;
    }
}
