using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Pair = Nethermind.Crypto.BlsNet.Pairing.Pairing;

internal static unsafe partial class Program
{
    private static readonly Random Random = new(2537);
    private static readonly BigInteger P = BigInteger.Parse("1a0111ea397fe69a4b1ba7b6434bacd764774b84f38512bf6730d2a0f6b0f6241eabfffeb153ffffb9feffffffffaaab", System.Globalization.NumberStyles.HexNumber);
    private static readonly ulong[] PMinus2 = [0xb9feffffffffaaa9, 0x1eabfffeb153ffff, 0x6730d2a0f6b0f624, 0x64774b84f38512bf, 0x4b1ba7b6434bacd7, 0x1a0111ea397fe69a];
    private static int _checks;
    private static bool NativeAvailable;
    private static bool NativeFp6Available;
    private static Fp12 _sink;
    private static Fp _fpSink;
    private static bool _boolSink;

    private static void Main(string[] args)
    {
        // Set the switch before any field method initializes the kernel policy.
        if (args.Contains("--expanded-kernels")) AppContext.SetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", true);
        if (args.Contains("--compact-kernels")) AppContext.SetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", false);
        NativeBlstLoader.PinBenchmarkProcessor();
        NativeAvailable = NativeBlstLoader.TryLoad(out nint handle);
        NativeFp6Available = NativeAvailable && NativeLibrary.TryGetExport(handle, "blst_fp6_mul", out _)
            && NativeLibrary.TryGetExport(handle, "blst_fp6_sqr", out _);
        if (NativeAvailable) NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) => name == "blst" ? handle : 0);
        Console.WriteLine(RuntimeInformation.FrameworkDescription);
        if (args.Contains("--kernel-cold-start")) { ColdKernelStart(); return; }
        if (args.Contains("--square-cold-start")) { ColdSquareStart(); return; }
        Console.WriteLine($"Field kernels: {(Fp.UseExpandedKernels ? "expanded" : "compact")}");
        if (args.Contains("--kernel-policy")) { KernelPolicyDiagnostics(); return; }
        if (args.Contains("--kernel-timing")) { KernelTimings(); return; }
        if (args.Contains("--kernel-tests")) { KernelTests(); Console.WriteLine($"Passed {_checks:N0} kernel checks."); return; }
        if (args.Contains("--fused-tests")) { FusedTowerTests(); Console.WriteLine($"Passed {_checks:N0} fused checks."); return; }
        if (args.Contains("--timing") || args.Contains("--perf")) { Timings(args.Contains("--perf")); return; }
        Check(sizeof(Fp6) == 288 && sizeof(Fp12) == 576, "layout");
        KernelTests();
        FusedTowerTests();
        InversionTests();
        ManagedTowerTests();
        if (NativeAvailable) TowerTests();
        PairingTests();
        Console.WriteLine($"Passed {_checks:N0} checks.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ColdSquareStart()
    {
        // Construct raw Montgomery data without touching any Fp static first.
        Span<byte> bytes = stackalloc byte[Fp.ByteCount];
        bytes.Clear(); bytes[0] = 3;
        Fp value = MemoryMarshal.Read<Fp>(bytes);
        Fp.Square(out Fp result, value);
        RInverse = BigInteger.ModPow(R % P, P - 2, P);
        CheckReduced(result, 9 * RInverse, "cold Fp square");
        Fp.Square(out value, value);
        Check(Fp.AreEqual(value, result), "cold Fp square alias");
        if (NativeAvailable)
        {
            Fp original = MemoryMarshal.Read<Fp>(bytes);
            KernelNative.blst_fp_sqr(out Fp native, original);
            Check(Fp.AreEqual(result, native), "cold Fp square native");
        }
        Console.WriteLine($"Passed {_checks} cold square checks; field kernels: {(Fp.UseExpandedKernels ? "expanded" : "compact")}.");
    }

    // An ordinary tiered caller, for checking readonly-field folding in Tier1.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ReadKernelPolicy() => Fp.UseExpandedKernels;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MultiplyWithPolicy(out Fp2 result, in Fp2 left, in Fp2 right) => Fp2.Multiply(out result, left, right);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ColdKernelStart()
    {
        // Load raw Montgomery words without calling any Fp method or reading a
        // field static first. The dispatcher must initialize policy before the
        // chosen AggressiveOptimization kernel is compiled, even on this path.
        Span<byte> bytes = stackalloc byte[96];
        bytes.Clear();
        Span<ulong> words = MemoryMarshal.Cast<byte, ulong>(bytes);
        words[0] = 1; words[6] = 2;
        Fp2 a = MemoryMarshal.Read<Fp2>(bytes);
        Fp2.Multiply(out Fp2 product, a, a);
        RInverse = BigInteger.ModPow(R % P, P - 2, P);
        CheckReduced(product.C0, -3 * RInverse, "cold multiply real");
        CheckReduced(product.C1, 4 * RInverse, "cold multiply imaginary");
        Fp2.Square(out Fp2 square, a);
        Check(Fp2.AreEqual(square, product), "cold square");
        Console.WriteLine($"Field kernels: {(Fp.UseExpandedKernels ? "expanded" : "compact")}");
        Console.WriteLine($"Passed {_checks:N0} cold-start checks.");
    }

    private static void KernelPolicyDiagnostics()
    {
        bool expected = Fp.UseExpandedKernels;
        Fp2 zero = default;
        for (int round = 0; round < 4; round++)
        {
            for (int i = 0; i < 10000; i++)
            {
                MultiplyWithPolicy(out Fp2 product, zero, zero);
                Check(ReadKernelPolicy() == expected && Fp2.AreEqual(product, zero), "stable kernel policy");
            }
            Thread.Sleep(50); // Allow background Tier1 compilation before the next calls.
        }
        Console.WriteLine($"Passed {_checks:N0} policy checks.");
    }

    private static void Check(bool ok, string label)
    {
        _checks++;
        if (!ok) throw new InvalidOperationException(label);
    }
    private static Fp Field()
    {
        Span<byte> bytes = stackalloc byte[48];
        do { Random.NextBytes(bytes); bytes[0] &= 0x1f; } while (!Fp.TryFromBigEndian(bytes, out _));
        Fp.TryFromBigEndian(bytes, out Fp f); return f;
    }
    private static Fp FromInteger(BigInteger n)
    {
        Span<byte> bytes = stackalloc byte[48];
        byte[] minimal = n.ToByteArray(isUnsigned: true, isBigEndian: true);
        minimal.CopyTo(bytes[(48 - minimal.Length)..]);
        Check(Fp.TryFromBigEndian(bytes, out Fp f), "canonical test value"); return f;
    }
    private static Fp2 Field2() => new(Field(), Field());
    private static Fp6 Field6() => new() { C0 = Field2(), C1 = Field2(), C2 = Field2() };
    private static Fp12 Field12() => new() { C0 = Field6(), C1 = Field6() };
    private static void Equal(in Fp12 a, in Fp12 b, string label) => Check(Fp12.AreEqual(a, b), label);

    private static void TestInverse(in Fp f)
    {
        Fp.Inverse(out Fp actual, f);
        Fp.Pow(out Fp expected, f, PMinus2);
        Check(Fp.AreEqual(actual, expected), "Fp inverse vs Fermat");
        if (NativeAvailable) { Native.blst_fp_inverse(out Fp native, f); Check(Fp.AreEqual(actual, native), "Fp inverse vs native"); }
        Fp.Multiply(out Fp product, actual, f);
        Check(f.IsZero ? actual.IsZero : product.IsOne, "Fp inverse product");
        Fp alias = f; Fp.Inverse(out alias, alias);
        Check(Fp.AreEqual(alias, actual), "Fp inverse alias");
    }
    private static void InversionTests()
    {
        TestInverse(Fp.Zero); TestInverse(Fp.One); TestInverse(FromInteger(P - 1)); TestInverse(Fp.FromUInt64(2));
        for (int i = 0; i < 381; i++) { TestInverse(FromInteger(BigInteger.One << i)); TestInverse(FromInteger(P - (BigInteger.One << i))); }
        for (int i = 0; i < 128; i++) TestInverse(FromInteger(P - 1 - i));
        for (int i = 0; i < 10000; i++) TestInverse(Field());
        Span<byte> invalid = stackalloc byte[48];
        invalid[0] = 0x1f; invalid[1..].Fill(0xff);
        Check(!Fp.TryFromBigEndian(invalid, out _), "values near 2^381 are noncanonical");
        Console.WriteLine("Fp inversion: 10,000 random values, powers of two, near-modulus values and aliases passed.");
    }

    private static void TowerTests()
    {
        for (int i = 0; i < 2500; i++)
        {
            Fp12 a = Field12(), b = Field12();
            Fp12.Multiply(out Fp12 r, a, b); Native.blst_fp12_mul(out Fp12 n, a, b); Equal(r, n, "Fp12 multiply");
            Fp12 alias = a; Fp12.Multiply(out alias, alias, b); Equal(alias, n, "multiply left alias");
            alias = b; Fp12.Multiply(out alias, a, alias); Equal(alias, n, "multiply right alias");
            alias = a; Fp12.Multiply(out alias, alias, alias); Fp12.Square(out r, a); Equal(alias, r, "multiply both alias");
            Native.blst_fp12_sqr(out n, a); Equal(r, n, "Fp12 square");
            alias = a; Fp12.Square(out alias, alias); Equal(alias, n, "square alias");
            Fp12.CyclotomicSquare(out r, a); Native.blst_fp12_cyclotomic_sqr(out n, a); Equal(r, n, "cyclotomic formula on arbitrary input");
            alias = a; Fp12.CyclotomicSquare(out alias, alias); Equal(alias, n, "cyclotomic alias");
            Fp12.Inverse(out r, a); Native.blst_fp12_inverse(out n, a); Equal(r, n, "Fp12 inverse");
            alias = a; Fp12.Inverse(out alias, alias); Equal(alias, n, "Fp12 inverse alias");
            Fp12.Multiply(out r, a, r); Check(r.IsOne, "Fp12 inverse product");
            for (int k = 1; k <= 3; k++)
            {
                Fp12.FrobeniusMap(out r, a, k); Native.blst_fp12_frobenius_map(out n, a, (nuint)k); Equal(r, n, "Frobenius");
                alias = a; Fp12.FrobeniusMap(out alias, alias, k); Equal(alias, n, "Frobenius alias");
            }
            Fp6 packed = Field6();
            Fp12.MultiplyBySparse(out r, a, packed); Native.blst_fp12_mul_by_xy00z0(out n, a, packed); Equal(r, n, "sparse product");
            Fp12 sparse = new() { C0 = new Fp6 { C0 = packed.C0, C1 = packed.C1 }, C1 = new Fp6 { C1 = packed.C2 } };
            Fp12.Multiply(out n, a, sparse); Equal(r, n, "sparse vs full");
            alias = a; Fp12.MultiplyBySparse(out alias, alias, alias.C0); Native.blst_fp12_mul_by_xy00z0(out n, a, a.C0); Equal(alias, n, "sparse packed alias");
            TestFp6(a.C0, b.C0);
            Fp12.Add(out r, a, b); Fp12.Subtract(out r, r, b); Equal(r, a, "add/sub alias");
            Fp12.Negate(out r, a); Fp12.Add(out r, r, a); Check(r.IsZero, "negate");
            Fp12.Conjugate(out r, a); Fp12.Conjugate(out r, r); Equal(r, a, "conjugate");
        }
        Fp12.Inverse(out Fp12 zero, Fp12.Zero); Check(zero.IsZero, "zero Fp12 inverse");
        Console.WriteLine("Tower: 2,500 random Fp12/Fp6 cases, native comparisons, sparse products, aliases and identities passed.");
    }
    private static void TestFp6(in Fp6 a, in Fp6 b)
    {
        Fp12 aa = new() { C0 = a }, bb = new() { C0 = b };
        Fp6.Multiply(out Fp6 r, a, b); Native.blst_fp12_mul(out Fp12 n, aa, bb); Check(Fp6.AreEqual(r, n.C0), "Fp6 multiply");
        Fp6 alias = a; Fp6.Multiply(out alias, alias, b); Check(Fp6.AreEqual(alias, r), "Fp6 multiply alias");
        Fp6.Square(out r, a); Native.blst_fp12_sqr(out n, aa); Check(Fp6.AreEqual(r, n.C0), "Fp6 square");
        alias = a; Fp6.Square(out alias, alias); Check(Fp6.AreEqual(alias, r), "Fp6 square alias");
        Fp6.Inverse(out r, a); Native.blst_fp12_inverse(out n, aa); Check(Fp6.AreEqual(r, n.C0), "Fp6 inverse");
        Fp6.MultiplyBy0Y0(out r, a, b.C1); bb.C0 = new Fp6 { C1 = b.C1 }; Native.blst_fp12_mul(out n, aa, bb); Check(Fp6.AreEqual(r, n.C0), "Fp6 0y0");
        Fp6.MultiplyByXY0(out r, a, b); bb.C0 = new Fp6 { C0 = b.C0, C1 = b.C1 }; Native.blst_fp12_mul(out n, aa, bb); Check(Fp6.AreEqual(r, n.C0), "Fp6 xy0");
    }
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
    private static void PairingTests()
    {
        G1Affine[] ps = new G1Affine[20]; G2Affine[] qs = new G2Affine[20];
        if (NativeAvailable)
        {
        for (int i = 0; i < 2000; i++)
        {
            Points(out ps[0], out qs[0]);
            Pair.MillerLoop(ps.AsSpan(0, 1), qs.AsSpan(0, 1), out Fp12 r);
            Native.blst_miller_loop(out Fp12 n, qs[0], ps[0]); Equal(r, n, "single Miller loop");
            Pair.FinalExponentiation(r, out Fp12 e); Native.blst_final_exp(out n, r); Equal(e, n, "final exponentiation");
            Fp12 alias = r; Pair.FinalExponentiation(alias, out alias); Equal(alias, e, "final exponentiation alias");
            Fp12.CyclotomicSquare(out r, e); Fp12.Square(out n, e); Equal(r, n, "cyclotomic subgroup square");
        }
        for (int trial = 0; trial < 5; trial++)
        {
            Fp12 product = Fp12.One;
            for (int count = 1; count <= 20; count++)
            {
                Points(out ps[count - 1], out qs[count - 1]);
                Native.blst_miller_loop(out Fp12 term, qs[count - 1], ps[count - 1]); Fp12.Multiply(out product, product, term);
                Pair.MillerLoop(ps.AsSpan(0, count), qs.AsSpan(0, count), out Fp12 batch); Equal(batch, product, "batched Miller vs product");
                NativeBatch(out Fp12 n, ps.AsSpan(0, count), qs.AsSpan(0, count)); Equal(batch, n, "batched Miller vs native");
                Pair.FinalExponentiation(batch, out Fp12 e); Native.blst_final_exp(out n, product); Equal(e, n, "batched final exponentiation");
            }
        }
        }
        Points(out G1Affine p, out G2Affine q, 1, 1);
        ps[0] = p; ps[1] = p; Fp.Negate(out ps[1].Y, ps[1].Y); qs[0] = q; qs[1] = q;
        Check(Pair.PairingCheck(ps.AsSpan(0, 2), qs.AsSpan(0, 2)), "opposite pairs true");
        Check(!Pair.PairingCheck(ps.AsSpan(0, 1), qs.AsSpan(0, 1)), "single generator false");
        Check(Pair.PairingCheck([], []), "empty pairing true");
        ps[2] = default; qs[2] = q; ps[3] = p; qs[3] = default;
        Check(Pair.PairingCheck(ps.AsSpan(0, 4), qs.AsSpan(0, 4)), "mixed infinity");
        Array.Clear(ps); Check(Pair.PairingCheck(ps, qs), "all infinity");
        Pair.MillerLoop([p], [default], out Fp12 infinity); Check(infinity.IsOne, "single infinity");
        Pair.MillerLoop([p], [q], out Fp12 baseLoop); Pair.FinalExponentiation(baseLoop, out Fp12 basis);
        for (ulong a = 1; a <= 10; a++)
        {
            ulong b = a + 1; Points(out G1Affine ap, out G2Affine bq, a, b);
            Pair.MillerLoop([ap], [bq], out Fp12 f); Pair.FinalExponentiation(f, out Fp12 e);
            Pow(out Fp12 expected, basis, a * b); Equal(e, expected, "bilinearity");
        }
        bool threw = false;
        try { Pair.MillerLoop([p], [], out _); } catch (ArgumentException) { threw = true; }
        Check(threw, "mismatched lengths");
        G1Affine[] longP = new G1Affine[130]; G2Affine[] longQ = new G2Affine[130];
        Fp12 expectedLong = Fp12.One;
        for (int i = 0; i < longP.Length; i++)
        {
            longP[i] = p; longQ[i] = q;
            if (i % 11 == 0) longP[i] = default;
            else if (i % 7 == 0) longQ[i] = default;
            else
            {
                Fp12 term;
                if (NativeAvailable) Native.blst_miller_loop(out term, q, p);
                else Pair.MillerLoop([p], [q], out term);
                Fp12.Multiply(out expectedLong, expectedLong, term);
            }
            if (i >= 14 && i <= 34 || i == 63 || i == 64 || i == 129)
            {
                Pair.MillerLoop(longP.AsSpan(0, i + 1), longQ.AsSpan(0, i + 1), out Fp12 actual);
                Equal(actual, expectedLong, "long batches and skipped-infinity boundaries");
            }
        }
        for (ulong a = 1; a <= 10; a++)
        {
            Points(out G1Affine ap, out G2Affine bq, a, a + 1);
            Points(out G1Affine abp, out _, a * (a + 1), 1);
            Fp.Negate(out abp.Y, abp.Y);
            Check(Pair.PairingCheck([ap, abp], [bq, q]), "scaled bilinear pairing check");
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
        {
            Pair.MillerLoop(longP, longQ, out Fp12 allocatedLoop);
            Pair.FinalExponentiation(allocatedLoop, out _);
        }
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "pairing allocation-free");
        Pair.FinalExponentiation(Fp12.Zero, out Fp12 z); Check(z.IsZero, "final exponentiation zero");
        Console.WriteLine("Pairing: bilinearity, infinity, batches and checks passed (native comparisons when available).");
    }
    private static void Pow(out Fp12 result, in Fp12 a, ulong exponent)
    {
        Fp12 r = Fp12.One, x = a;
        while (exponent != 0) { if ((exponent & 1) != 0) Fp12.Multiply(out r, r, x); Fp12.Square(out x, x); exponent >>= 1; }
        result = r;
    }
    private static void NativeBatch(out Fp12 f, ReadOnlySpan<G1Affine> p, ReadOnlySpan<G2Affine> q)
    {
        G1Affine** pp = stackalloc G1Affine*[p.Length]; G2Affine** qq = stackalloc G2Affine*[q.Length];
        fixed (G1Affine* px = p) fixed (G2Affine* qx = q)
        {
            for (int i = 0; i < p.Length; i++) { pp[i] = px + i; qq[i] = qx + i; }
            Native.blst_miller_loop_n(out f, qq, pp, (nuint)p.Length);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct P1 { public Fp X, Y, Z; }
    [StructLayout(LayoutKind.Sequential)] private struct P2 { public Fp2 X, Y, Z; }
    private static class Native
    {
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_mul(out Fp r, in Fp a, in Fp b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_sqr(out Fp r, in Fp a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_mul(out Fp2 r, in Fp2 a, in Fp2 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_sub(out Fp2 r, in Fp2 a, in Fp2 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_sqr(out Fp2 r, in Fp2 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_inverse(out Fp r, in Fp a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_mul(out Fp12 r, in Fp12 a, in Fp12 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_sqr(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp6_mul(out Fp6 r, in Fp6 a, in Fp6 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp6_sqr(out Fp6 r, in Fp6 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_cyclotomic_sqr(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_inverse(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_frobenius_map(out Fp12 r, in Fp12 a, nuint n);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_mul_by_xy00z0(out Fp12 r, in Fp12 a, in Fp6 packed);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_miller_loop(out Fp12 r, in G2Affine q, in G1Affine p);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_miller_loop_n(out Fp12 r, G2Affine** q, G1Affine** p, nuint n);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_final_exp(out Fp12 r, in Fp12 f);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern P1* blst_p1_generator();
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern P2* blst_p2_generator();
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p1_mult(out P1 r, P1* p, byte* scalar, nuint bits);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p2_mult(out P2 r, P2* p, byte* scalar, nuint bits);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p1_to_affine(out G1Affine r, in P1 p);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p2_to_affine(out G2Affine r, in P2 p);
    }

    private static void Timings(bool quick)
    {
        Fp12 a = Field12(), b = Field12(); Fp fp = Field();
        G1Affine[] ps = new G1Affine[8]; G2Affine[] qs = new G2Affine[8];
        for (int i = 0; i < 8; i++) Points(out ps[i], out qs[i]);
        Pair.MillerLoop(ps.AsSpan(0, 1), qs.AsSpan(0, 1), out Fp12 f);
        Pair.FinalExponentiation(f, out Fp12 cyclo);
        string[] names = ["Fp inverse", "Fp inverse Fermat", "Fp12 multiply", "Fp12 square", "Cyclotomic square", "Miller 1", "Miller 8", "Final exponentiation", "Pairing check 1", "Pairing check 2", "Pairing check 8", "Fp6 multiply", "Fp6 square", "Fp12 sparse multiply", "Fp12 two sparse multiplies"];
        Console.WriteLine("Operation | managed ns | native ns | managed bytes");
        string[]? filters = Environment.GetEnvironmentVariable("BLST_BENCH_FILTER")?.Split('|');
        for (int op = quick ? 2 : 0; op < names.Length; op++)
        {
            if (filters is not null && !Array.Exists(filters, filter => names[op].Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
            int iterations = op <= 4 || op >= 11 ? (quick ? 5000 : 30000) : (quick ? 60 : 250);
            if (Environment.GetEnvironmentVariable("BLST_BENCH_ROUND_MS") is not null)
                iterations = op <= 4 || op >= 11 ? 1000 : 10;
            double m = Measure(op, false, iterations, fp, a, b, cyclo, f, ps, qs, out long allocated);
            double n = !NativeAvailable || op == 1 || op is 11 or 12 && !NativeFp6Available ? double.NaN : Measure(op, true, iterations, fp, a, b, cyclo, f, ps, qs, out _);
            Console.WriteLine($"{names[op]} | {m:F2} | {n:F2} | {allocated}");
        }
        foreach (bool native in NativeAvailable ? new[] { false, true } : new[] { false })
        {
            long best = long.MaxValue;
            for (int round = 0; round < 9; round++)
            {
                long start = Stopwatch.GetTimestamp();
                KernelLoop(native, 1000000, fp, a.C0.C0.C0);
                if (round >= 2) best = Math.Min(best, Stopwatch.GetTimestamp() - start);
            }
            Console.WriteLine($"Fp multiply {(native ? "native" : "inline")} | {best * 1e3 / Stopwatch.Frequency:F2} ns");
        }
        GC.KeepAlive(_sink); GC.KeepAlive(_fpSink); GC.KeepAlive(_boolSink);
    }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void KernelLoop(bool native, int iterations, in Fp a, in Fp b)
    {
        Fp r = default;
        if (native) { for (int i = 0; i < iterations; i++) Native.blst_fp_mul(out r, a, b); }
        else { for (int i = 0; i < iterations; i++) Fp.Multiply(out r, a, b); }
        _fpSink = r;
    }
    private static double Measure(int op, bool native, int iterations, in Fp fp, in Fp12 a, in Fp12 b, in Fp12 cyclo, in Fp12 f, G1Affine[] ps, G2Affine[] qs, out long allocated)
    {
        long warmupStart = Stopwatch.GetTimestamp();
        do { Run(op, native, iterations, fp, a, b, cyclo, f, ps, qs); }
        while (Stopwatch.GetTimestamp() - warmupStart < Stopwatch.Frequency / 2);
        long best = long.MaxValue; allocated = 0;
        for (int round = 0; round < (Environment.GetEnvironmentVariable("BLST_BENCH_ROUND_MS") is null ? 7 : 21); round++)
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
                    case 11: for (int i = 0; i < iterations; i++) Native.blst_fp6_mul(out r.C0, a.C0, b.C0); break;
                    case 12: for (int i = 0; i < iterations; i++) Native.blst_fp6_sqr(out r.C0, a.C0); break;
                    case 13: for (int i = 0; i < iterations; i++) Native.blst_fp12_mul_by_xy00z0(out r, a, b.C0); break;
                    case 14: for (int i = 0; i < iterations; i++) { Native.blst_fp12_mul_by_xy00z0(out r, a, b.C0); Native.blst_fp12_mul_by_xy00z0(out r, r, b.C1); } break;
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
                    case 11: for (int i = 0; i < iterations; i++) Fp6.Multiply(out r.C0, a.C0, b.C0); break;
                    case 12: for (int i = 0; i < iterations; i++) Fp6.Square(out r.C0, a.C0); break;
                    case 13: for (int i = 0; i < iterations; i++) Fp12.MultiplyBySparse(out r, a, b.C0); break;
                    case 14: for (int i = 0; i < iterations; i++) SparseTower.MultiplyPair(out r, a, b.C0, b.C1); break;
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
