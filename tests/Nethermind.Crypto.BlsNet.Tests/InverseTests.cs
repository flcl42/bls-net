using System.Numerics;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static class InverseTests
{
    private static int checks;
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException("Fixed inverse: " + message); }
    private static byte[] Bytes(BigInteger x, int size)
    { byte[] b = new byte[size]; Check(x.TryWriteBytes(b, out _, true), "encode"); return b; }
    private static T Raw<T>(BigInteger x) where T : unmanaged => MemoryMarshal.Read<T>(Bytes(x, Marshal.SizeOf<T>()));
    private static BigInteger Raw<T>(in T x) where T : unmanaged
        => new(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in x, 1)), true);

    internal static int Run(bool native)
    {
        BigInteger p = Raw(Fp.FromMontgomery(0xb9feffffffffaaab, 0x1eabfffeb153ffff, 0x6730d2a0f6b0f624, 0x64774b84f38512bf, 0x4b1ba7b6434bacd7, 0x1a0111ea397fe69a));
        BigInteger r = BigInteger.Parse("0" + "73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001", System.Globalization.NumberStyles.HexNumber);
        TestField(p, 384, native); TestField(r, 256, native);
        TestBatchEdges(); TestWide(p, 384); TestWide(r, 256); TestComplexEdges(native); TestAllocations();
        Console.WriteLine($"Fixed inverse: {checks:N0} checks, seed 0x494e56; native={native}.");
        return checks;
    }

    private static IEnumerable<BigInteger> Corpus(BigInteger m, int bits)
    {
        yield return 0; yield return 1; yield return 2; yield return m - 1; yield return m - 2;
        yield return (m - 1) / 2; yield return (BigInteger.One << bits) % m;
        BigInteger fibonacci = 1, previous = 1, alternating = 0;
        for (int i = 0; i < bits; i++)
        {
            BigInteger power = BigInteger.One << i;
            yield return power % m; yield return (power - 1) % m;
            yield return m - 1 - power % m;
            if ((i & 1) == 0) alternating |= power;
            (fibonacci, previous) = (fibonacci + previous, fibonacci);
            if (i % 17 == 0) yield return fibonacci % m;
        }
        yield return alternating % m; yield return (alternating >> 1) % m;
        Random random = new(0x494e56);
        byte[] bytes = new byte[bits / 8];
        for (int i = 0; i < 4096; i++)
        { random.NextBytes(bytes); yield return new BigInteger(bytes, true) % m; }
    }

    private static void TestField(BigInteger m, int bits, bool native)
    {
        foreach (BigInteger x in Corpus(m, bits))
        {
            BigInteger inverse = BigInteger.ModPow(x, m - 2, m);
            if (bits == 384)
            {
                Check(Fp.TryFromLittleEndian(Bytes(x, 48), out Fp a), "Fp import");
                Fp.InverseConstantTime(out Fp b, a);
                byte[] encoded = new byte[48]; b.ToLittleEndian(encoded);
                Check(new BigInteger(encoded, true) == inverse, "Fp BigInteger");
                Check(Raw(b) < m, "Fp canonical Montgomery");
                Fp alias = a; Fp.InverseConstantTime(out alias, alias); Check(alias == b, "Fp alias");
                Fp.Inverse(out Fp vt, a); Check(vt == b, "Fp variable unchanged");
                if (native) { KernelNative.blst_fp_inverse(out Fp reference, a); Check(reference == b, "Fp native"); }
                // Norm reference independent of the Fp2 implementation.
                BigInteger y = (x * x + 17) % m;
                if (x < 3) y = x;
                Check(Fp.TryFromLittleEndian(Bytes(y, 48), out Fp c), "Fp2 import");
                Fp2 z = new(a, c);
                Fp2.InverseConstantTime(out Fp2 zi, z);
                BigInteger normInverse = BigInteger.ModPow((x * x + y * y) % m, m - 2, m);
                zi.C0.ToLittleEndian(encoded); Check(new BigInteger(encoded, true) == x * normInverse % m, "Fp2 real BigInteger");
                zi.C1.ToLittleEndian(encoded); Check(new BigInteger(encoded, true) == ((m - y) * normInverse) % m, "Fp2 imaginary BigInteger");
                Check(Raw(zi.C0) < m && Raw(zi.C1) < m, "Fp2 canonical");
                Fp2 za = z; Fp2.InverseConstantTime(out za, za); Check(za == zi, "Fp2 alias");
                if (native) { KernelNative.blst_fp2_inverse(out Fp2 reference, z); Check(reference == zi, "Fp2 native"); }
                Model(Raw(a), m, bits);
            }
            else
            {
                Fr a = Fr.FromLittleEndianConstantTime(Bytes(x, 32));
                Fr.InverseConstantTime(out Fr b, a);
                byte[] encoded = new byte[32]; b.ToLittleEndianConstantTime(encoded);
                Check(new BigInteger(encoded, true) == inverse, "Fr BigInteger");
                Check(Raw(b) < m, "Fr canonical Montgomery");
                Fr alias = a; Fr.InverseConstantTime(out alias, alias); Check(Fr.EqualMask(alias, b) == ulong.MaxValue, "Fr alias");
                if (native) { KernelNative.blst_fr_inverse(out Fr reference, a); Check(Fr.EqualMask(reference, b) == ulong.MaxValue, "Fr native"); }
                Model(Raw(a), m, bits);
            }
        }
        // Drive the algorithm directly with raw boundary residues too. This
        // tests the schedule independently of the canonical-to-Montgomery map.
        foreach (BigInteger x in new BigInteger[] { 0, 1, 2, m - 1, m - 2, (m - 1) / 2 }) Model(x, m, bits);
        for (int bit = 0; bit < m.GetBitLength(); bit++)
        {
            BigInteger x = (BigInteger.One << bit) % m;
            Model(x, m, bits); Model(x - 1, m, bits);
        }
        BigInteger worst = BigInteger.One << ((int)m.GetBitLength() - 1), divisor = m;
        int steps = 0;
        while (!worst.IsZero)
        {
            if (!worst.IsEven)
            { if (worst < divisor) (worst, divisor) = (divisor, worst); worst -= divisor; }
            worst >>= 1; steps++;
        }
        Check(steps == 2 * m.GetBitLength() - 1, "classic worst-case reaches Pornin bound");
    }

    // Independent, branching arbitrary-precision transcription of blst's
    // Python reference. It also checks each production approximate and matrix
    // update against this model. BigInteger never enters production code.
    private static void Model(BigInteger input, BigInteger m, int bits)
    {
        BigInteger a = input, b = m, u = 1, v = 0;
        int batches = bits == 384 ? 24 : 15, elapsed = 0;
        for (int batch = 0; batch < batches; batch++)
        {
            long oldLength = a.GetBitLength() + b.GetBitLength();
            int length = (int)BigInteger.Max(a, b).GetBitLength();
            ulong aa = length < 64 ? (ulong)a : (ulong)(a & 0x7fffffff) | (ulong)(a >> (length - 33)) << 31;
            ulong bb = length < 64 ? (ulong)b : (ulong)(b & 0x7fffffff) | (ulong)(b >> (length - 33)) << 31;
            if (bits == 384) { Fp.InverseApproximate(Raw<Fp>(a), Raw<Fp>(b), out ulong pa, out ulong pb); Check(pa == aa && pb == bb, "Fp approximation"); }
            else { Fr.InverseApproximate(Raw<Fr>(a), Raw<Fr>(b), out ulong pa, out ulong pb); Check(pa == aa && pb == bb, "Fr approximation"); }
            Matrix(aa, bb, 31, out BigInteger f0, out BigInteger g0, out BigInteger f1, out BigInteger g1);
            InverseBatch.Steps31(aa, bb, out long pf0, out long pg0, out long pf1, out long pg1);
            Check(f0 == pf0 && g0 == pg0 && f1 == pf1 && g1 == pg1, "packed matrix");
            Check(BigInteger.Abs(f0) + BigInteger.Abs(g0) <= BigInteger.One << 31 && BigInteger.Abs(f1) + BigInteger.Abs(g1) <= BigInteger.One << 31, "row bounds");
            Check(BigInteger.Abs(f0 * g1 - f1 * g0) == BigInteger.One << 31, "determinant");
            BigInteger nextA = a * f0 + b * g0, nextB = a * f1 + b * g1;
            Check((nextA & 0x7fffffff) == 0 && (nextB & 0x7fffffff) == 0, "exact division");
            nextA >>= 31; nextB >>= 31;
            if (nextA.Sign < 0) { f0 = -f0; g0 = -g0; }
            if (nextB.Sign < 0) { f1 = -f1; g1 = -g1; }
            if (bits == 384)
            {
                Fp.InverseUpdate(out Fp pa, Raw<Fp>(a), Raw<Fp>(b), ref pf0, ref pg0);
                Fp.InverseUpdate(out Fp pb, Raw<Fp>(a), Raw<Fp>(b), ref pf1, ref pg1);
                Check(Raw(pa) == BigInteger.Abs(nextA) && Raw(pb) == BigInteger.Abs(nextB), "Fp full matrix");
            }
            else
            {
                Fr.InverseUpdate(out Fr pa, Raw<Fr>(a), Raw<Fr>(b), ref pf0, ref pg0);
                Fr.InverseUpdate(out Fr pb, Raw<Fr>(a), Raw<Fr>(b), ref pf1, ref pg1);
                Check(Raw(pa) == BigInteger.Abs(nextA) && Raw(pb) == BigInteger.Abs(nextB), "Fr full matrix");
            }
            Check(pf0 == f0 && pg0 == g0 && pf1 == f1 && pg1 == g1, "matrix sign correction");
            (a, b) = (BigInteger.Abs(nextA), BigInteger.Abs(nextB));
            Check(input.IsZero || a.GetBitLength() + b.GetBitLength() <= Math.Max(1, oldLength - 31), "Pornin batch contraction");
            (u, v) = (u * f0 + v * g0, u * f1 + v * g1);
            elapsed += 31;
            Check(BigInteger.Abs(u) <= BigInteger.One << elapsed && BigInteger.Abs(v) <= BigInteger.One << elapsed, "wide cofactor bounds");
            Check((u * input - (a << elapsed)) % m == 0 && (v * input - (b << elapsed)) % m == 0, "cofactor invariant");
        }
        Check(input.IsZero || a < BigInteger.One << 64 && b < BigInteger.One << 64, "last batch exact word bound");
        int count = 2 * bits - elapsed;
        Matrix(a, b, count, out BigInteger ff0, out BigInteger gg0, out BigInteger ff1, out BigInteger gg1);
        InverseBatch.Finish((ulong)(a & ulong.MaxValue), (ulong)(b & ulong.MaxValue), count, out long finalF, out long finalG);
        Check(ff1 == finalF && gg1 == finalG, "final matrix");
        Check((a * ff0 + b * gg0) >> count == 0, "termination within fixed bound");
        Check((a * ff1 + b * gg1) >> count == (input.IsZero ? m : 1), "gcd");
        v = u * ff1 + v * gg1;
        Check(BigInteger.Abs(v) <= BigInteger.One << (2 * bits), "final cofactor bound");
        BigInteger modx = m << (2 * bits - (int)m.GetBitLength());
        if (v.Sign < 0) v += modx;
        if (v.Sign < 0) v += modx;
        else if (v == BigInteger.One << (2 * bits)) v -= modx;
        Check(v.Sign >= 0 && v < BigInteger.One << (2 * bits), "modx correction bound");
        Check((v * input - (BigInteger.One << (2 * bits))) % m == 0 || input.IsZero && v.IsZero, "final Montgomery invariant");
    }

    private static void Matrix(BigInteger a, BigInteger b, int count, out BigInteger f0, out BigInteger g0, out BigInteger f1, out BigInteger g1)
    {
        f0 = 1; g0 = 0; f1 = 0; g1 = 1;
        for (int i = 0; i < count; i++)
        {
            if (!a.IsEven)
            {
                if (a < b) { (a, b) = (b, a); (f0, f1) = (f1, f0); (g0, g1) = (g1, g0); }
                a -= b; f0 -= f1; g0 -= g1;
            }
            a >>= 1; f1 <<= 1; g1 <<= 1;
        }
    }

    private static void TestBatchEdges()
    {
        ulong[] edges = [0, 1, 2, 3, 0x7fffffff, 0x80000000, 0xffffffff, 1UL << 63, ulong.MaxValue];
        foreach (ulong a in edges) foreach (ulong b in edges)
        {
            Matrix(a, b, 31, out BigInteger f0, out BigInteger g0, out BigInteger f1, out BigInteger g1);
            InverseBatch.Steps31(a, b, out long pf0, out long pg0, out long pf1, out long pg1);
            Check(f0 == pf0 && g0 == pg0 && f1 == pf1 && g1 == pg1, "batch boundary coefficients");
        }
    }

    private static void TestAllocations()
    {
        Fp a = Fp.FromUInt64(17); Fr b = Fr.FromUInt64(19); Fp2 c = new(a, Fp.One);
        for (int i = 0; i < 100; i++) { Fp.InverseConstantTime(out a, a); Fr.InverseConstantTime(out b, b); Fp2.InverseConstantTime(out c, c); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Fp.InverseConstantTime(out a, a); Fr.InverseConstantTime(out b, b); Fp2.InverseConstantTime(out c, c); }
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "warmed allocations");
        GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(c);
    }

    private static void TestComplexEdges(bool native)
    {
        Fp.Negate(out Fp minusOne, Fp.One);
        foreach (Fp a in new[] { Fp.Zero, Fp.One, minusOne, Fp.FromUInt64(2) })
            foreach (Fp b in new[] { Fp.Zero, Fp.One, minusOne, Fp.FromUInt64(2) })
            {
                Fp2 input = new(a, b), alias = input;
                Fp2.InverseConstantTime(out Fp2 result, input);
                Fp2.Inverse(out Fp2 expected, input); Check(result == expected, "Fp2 special coefficients");
                Fp2.InverseConstantTime(out alias, alias); Check(alias == result, "Fp2 special alias");
                Fp2.Multiply(out Fp2 product, input, result); Check(product == (input.IsZero ? Fp2.Zero : Fp2.One), "Fp2 special identity");
                if (native) { KernelNative.blst_fp2_inverse(out expected, input); Check(result == expected, "Fp2 special native"); }
            }
    }

    private static void TestWide(BigInteger m, int bits)
    {
        BigInteger limit = BigInteger.One << (2 * bits), mask = limit - 1;
        // Cover both corrections, positive overflow, a high half above modx,
        // and every trial-subtraction threshold, independently of reachable GCDs.
        List<BigInteger> values = [-limit, -limit + 1, -1, 0, 1, limit - 1, limit];
        for (int multiple = 1; multiple <= 9; multiple++)
            foreach (int delta in new[] { -1, 0, 1 })
            {
                BigInteger x = ((m * multiple) << bits) + delta;
                if (x >= 0 && x <= limit) { values.Add(x); values.Add(-x); }
            }
        foreach (BigInteger x in values)
        {
            BigInteger expected = (x % m + m) % m;
            if (bits == 384)
            {
                Fp.InverseCorrect(out Fp result, Raw<Fp.InverseWide>(x & mask), (long)(x >> (2 * bits)));
                Check(Raw(result) == expected, "Fp wide correction boundary");
            }
            else
            {
                Fr.InverseCorrect(out Fr result, Raw<Fr.InverseWide>(x & mask), (long)(x >> (2 * bits)));
                Check(Raw(result) == expected, "Fr wide correction boundary");
            }
        }
        Random random = new(0x57494445);
        byte[] bytes = new byte[bits / 4];
        for (int i = 0; i < 256; i++)
        {
            random.NextBytes(bytes); BigInteger u = new(bytes, false);
            random.NextBytes(bytes); BigInteger v = new(bytes, false);
            long f = i % 3 == 0 ? 1L << 31 : random.NextInt64(-(1L << 31), 1L << 31);
            long g = i % 3 == 0 ? -(1L << 31) : random.NextInt64(-(1L << 31), 1L << 31);
            BigInteger expected = u * f + v * g;
            if (bits == 384)
            {
                long top = Fp.InverseCoefficient(out Fp.InverseWide result, Raw<Fp.InverseWide>(u & mask), Raw<Fp.InverseWide>(v & mask), f, g);
                Check(Raw(result) + ((BigInteger)top << (2 * bits)) == expected, "Fp signed wide matrix");
            }
            else
            {
                long top = Fr.InverseCoefficient(out Fr.InverseWide result, Raw<Fr.InverseWide>(u & mask), Raw<Fr.InverseWide>(v & mask), f, g);
                Check(Raw(result) + ((BigInteger)top << (2 * bits)) == expected, "Fr signed wide matrix");
            }
        }
    }
}
