using static TowerInterop;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static unsafe partial class TowerTests
{
    private static readonly BigInteger R = BigInteger.One << 384;
    private static BigInteger RInverse;

    private static BigInteger Mod(BigInteger x) { x %= P; return x.Sign < 0 ? x + P : x; }
    private static BigInteger Raw(in Fp a) => new(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in a, 1)), isUnsigned: true);
    private static BigInteger Signed(in FpWide a) => new(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in a, 1)));
    private static FpWide Wide(BigInteger x)
    {
        Span<byte> bytes = stackalloc byte[96];
        byte[] source = x.ToByteArray();
        bytes.Fill(x.Sign < 0 ? (byte)255 : (byte)0);
        source.CopyTo(bytes);
        return MemoryMarshal.Read<FpWide>(bytes);
    }
    private static Fp RawField(BigInteger x)
    {
        Span<byte> bytes = stackalloc byte[48];
        bytes.Clear();
        x.TryWriteBytes(bytes, out _, isUnsigned: true);
        return MemoryMarshal.Read<Fp>(bytes);
    }
    private static BigInteger RandomInteger(int bytes)
    {
        byte[] x = new byte[bytes]; Random.NextBytes(x); return new BigInteger(x, isUnsigned: true);
    }
    private static void CheckReduced(in Fp result, BigInteger expected, string label)
    {
        Check(Raw(result) < P && Raw(result) == Mod(expected), label);
    }

    private static void KernelTests()
    {
        RInverse = BigInteger.ModPow(R % P, P - 2, P);
        Check(sizeof(FpWide) == 96 && sizeof(Fp2Wide) == 192 && sizeof(Fp6Wide) == 576, "wide layouts");
        SquareRunTests();
        BigInteger wideBound = 22 * P * P;
        Check(9 * P * P < P * R && wideBound < 3 * P * R && 4 * P < R / 2, "reduction bounds");
        BigInteger[] edges = [0, 1, -1, P * P, -P * P, P * R - 1, -P * R + 1,
            3 * P * R - 1, -3 * P * R + 1, wideBound - 1, -wideBound + 1];
        foreach (BigInteger edge in edges)
        {
            FpWide x = Wide(edge);
            Fp.ReduceWide(out Fp reduced, x);
            CheckReduced(reduced, edge * RInverse, "wide reducer signed bound edge");
            FpWide aliasWide = x;
            Fp.ReduceWide(out Unsafe.As<FpWide, Fp>(ref aliasWide), aliasWide);
            CheckReduced(Unsafe.As<FpWide, Fp>(ref aliasWide), edge * RInverse, "wide reducer overlapping output");
            if (BigInteger.Abs(edge) < P * R)
            {
                Fp.ReduceWideSmall(out reduced, x);
                CheckReduced(reduced, edge * RInverse, "small reducer signed bound edge");
                TestWideDifference(x, default, edge);
                if (edge.Sign >= 0)
                {
                    Fp.ReduceWidePositive(out reduced, x);
                    CheckReduced(reduced, edge * RInverse, "positive reducer bound edge");
                    aliasWide = x;
                    Fp.ReduceWidePositive(out Unsafe.As<FpWide, Fp>(ref aliasWide), aliasWide);
                    CheckReduced(Unsafe.As<FpWide, Fp>(ref aliasWide), edge * RInverse, "positive reducer overlapping output");
                }
            }
        }
        BigInteger[] rawEdges = [0, 1, 2, P / 2, P - 2, P - 1];
        foreach (BigInteger x in rawEdges) foreach (BigInteger y in rawEdges)
        {
            Fp a = RawField(x), b = RawField(y);
            TestKernelPair(a, b, x, y);
            foreach (BigInteger z in rawEdges) foreach (BigInteger w in rawEdges)
                TestFp2(new Fp2(a, b), new Fp2(RawField(z), RawField(w)));
        }
        for (int i = 0; i < 10000; i++)
        {
            Fp a = Field(), b = Field();
            TestKernelPair(a, b, Raw(a), Raw(b));
            Fp.AddUnreduced(out Fp twiceA, a, a);
            Fp.AddUnreduced(out Fp twiceB, b, b);
            Fp.MultiplyWide(out FpWide wideTwice, twiceA, twiceB);
            Check(Signed(wideTwice) == Raw(a) * Raw(b) * 4, "wide unreduced product vs BigInteger");
            Fp.MultiplyWideNoAlias(out FpWide directTwice, twiceA, twiceB);
            Check(Signed(directTwice) == Raw(a) * Raw(b) * 4, "disjoint unreduced product bound");
            Fp.ReduceWide(out Fp wideReduced, wideTwice);
            CheckReduced(wideReduced, Raw(a) * Raw(b) * 4 * RInverse, "wide unreduced product reduction");
            Fp.MultiplyComposite(out Fp product, twiceA, twiceB);
            CheckReduced(product, Raw(a) * Raw(b) * 4 * RInverse, "unreduced product bound");
            Fp c = Field(); Fp.SubtractSumComposite(out Fp difference, a, b, c);
            CheckReduced(difference, Raw(a) - Raw(b) - Raw(c), "fused subtraction vs BigInteger");
            Fp differenceAlias = a; Fp.SubtractSumComposite(out differenceAlias, differenceAlias, b, c);
            Check(Fp.AreEqual(differenceAlias, difference), "fused subtraction left alias");
            differenceAlias = b; Fp.SubtractSumComposite(out differenceAlias, a, differenceAlias, c);
            Check(Fp.AreEqual(differenceAlias, difference), "fused subtraction middle alias");
            differenceAlias = c; Fp.SubtractSumComposite(out differenceAlias, a, b, differenceAlias);
            Check(Fp.AreEqual(differenceAlias, difference), "fused subtraction right alias");
            BigInteger x = RandomInteger(96) % (wideBound / 2);
            BigInteger y = RandomInteger(96) % (wideBound / 2);
            if ((i & 1) != 0) x = -x;
            if ((i & 2) != 0) y = -y;
            FpWide wx = Wide(x), wy = Wide(y);
            Fp.AddWide(out FpWide sum, wx, wy); Check(Signed(sum) == x + y, "wide sum vs BigInteger");
            FpWide alias = wx; Fp.AddWide(out alias, alias, wy); Check(Signed(alias) == x + y, "wide sum left alias");
            alias = wy; Fp.AddWideInline(out alias, wx, alias); Check(Signed(alias) == x + y, "wide sum right alias inline");
            Fp.SubtractWide(out FpWide sub, wx, wy); Check(Signed(sub) == x - y, "wide subtraction vs BigInteger");
            alias = wy; Fp.SubtractWide(out alias, wx, alias); Check(Signed(alias) == x - y, "wide subtraction right alias");
            alias = wx; Fp.SubtractWideInline(out alias, alias, wy); Check(Signed(alias) == x - y, "wide subtraction left alias inline");
            Fp.ReduceWide(out Fp actual, sum); CheckReduced(actual, (x + y) * RInverse, "wide sum reduction");
            Fp.ReduceWide(out actual, sub); CheckReduced(actual, (x - y) * RInverse, "wide subtraction reduction");
            BigInteger middle = RandomInteger(96) % (P * R);
            if ((i & 1) != 0) middle = -middle;
            FpWide medium = Wide(middle); Fp.ReduceWideSmall(out actual, medium);
            CheckReduced(actual, middle * RInverse, "small reduction vs BigInteger");
            TestWideDifference(medium, wx, middle - x, checkBound: true);
            medium = Wide(BigInteger.Abs(middle));
            Fp.ReduceWidePositive(out actual, medium);
            CheckReduced(actual, BigInteger.Abs(middle) * RInverse, "positive reduction vs BigInteger");
            TestFp2(Field2(), Field2());
        }
        Console.WriteLine("Kernels: 10,000 random signed wide/small reductions, raw products, unreduced sums, fused subtraction and Fp2 cases passed.");
    }

    private static void TestWideDifference(in FpWide a, in FpWide b, BigInteger difference, bool checkBound = false)
    {
        if (checkBound && BigInteger.Abs(difference) >= P * R) return;
        Fp.ReduceWideDifference(out Fp actual, a, b);
        CheckReduced(actual, difference * RInverse, "wide difference reduction vs BigInteger");
        FpWide alias = a;
        Fp.ReduceWideDifference(out Unsafe.As<FpWide, Fp>(ref alias), alias, b);
        Check(Fp.AreEqual(Unsafe.As<FpWide, Fp>(ref alias), actual), "wide difference left overlap");
        alias = b;
        Fp.ReduceWideDifference(out Unsafe.As<FpWide, Fp>(ref alias), a, alias);
        Check(Fp.AreEqual(Unsafe.As<FpWide, Fp>(ref alias), actual), "wide difference right overlap");
    }
    private static void TestKernelPair(in Fp a, in Fp b, BigInteger x, BigInteger y)
    {
        Fp.MultiplyWide(out FpWide wide, a, b); Check(Signed(wide) == x * y, "wide product vs BigInteger");
        Fp.MultiplyWideNoAlias(out FpWide direct, a, b);
        Check(Signed(direct) == x * y, "disjoint wide product vs BigInteger");
        Check(Raw(a) == x && Raw(b) == y, "disjoint wide product inputs intact");
        FpWide overlap = default;
        Unsafe.As<FpWide, Fp>(ref overlap) = a;
        Fp.MultiplyWide(out overlap, Unsafe.As<FpWide, Fp>(ref overlap), b);
        Check(Signed(overlap) == x * y, "wide product left overlap");
        Unsafe.As<FpWide, Fp>(ref overlap) = b;
        Fp.MultiplyWide(out overlap, a, Unsafe.As<FpWide, Fp>(ref overlap));
        Check(Signed(overlap) == x * y, "wide product right overlap");
        Fp.MultiplyWide(out FpWide t1, b, b);
        Fp.MultiplyWide(out FpWide t2, a, a);
        FpWide t0 = wide;
        Fp.SubtractWideProducts(ref t0, t1, ref t2);
        Check(Signed(t0) == x * y - y * y, "wide Karatsuba real in place");
        Check(Signed(t2) == x * x - x * y - y * y, "wide Karatsuba imaginary in place");
        Check(Signed(t1) == y * y, "wide Karatsuba middle intact");
        t0 = wide;
        Fp.MultiplyWide(out t2, a, a);
        Fp.SubtractWideImaginary(ref t2, t0, t1);
        Check(Signed(t2) == x * x - x * y - y * y, "wide imaginary in place");
        Check(Signed(t0) == x * y && Signed(t1) == y * y, "wide imaginary inputs intact");
        TestWideDifference(t0, t1, x * y - y * y);
        Fp.ReduceWide(out Fp result, wide);
        CheckReduced(result, x * y * RInverse, "wide reducer vs BigInteger");
        if (NativeAvailable) { Native.blst_fp_mul(out Fp native, a, b); Check(Fp.AreEqual(result, native), "wide product and reducer vs native"); }
        Fp.MultiplyWide(out wide, a, a); Check(Signed(wide) == x * x, "wide square product");
        Fp.MultiplyWideNoAlias(out direct, a, a);
        Check(Signed(direct) == x * x, "disjoint product with identical inputs");
        Fp.AddUnreduced(out Fp sum, a, b); Check(Raw(sum) == x + y, "unreduced sum");
        Fp alias = a; Fp.AddUnreduced(out alias, alias, b); Check(Raw(alias) == x + y, "unreduced sum left alias");
        alias = b; Fp.AddUnreduced(out alias, a, alias); Check(Raw(alias) == x + y, "unreduced sum right alias");
        Fp.Square(out Fp square, a); CheckReduced(square, x * x * RInverse, "Fp square BigInteger");
        if (NativeAvailable) { KernelNative.blst_fp_sqr(out Fp native, a); Check(Fp.AreEqual(square, native), "Fp dedicated native square"); }
        alias = a; Fp.Square(out alias, alias); Check(Fp.AreEqual(alias, square), "Fp square alias");
        Fp.Halve(out Fp half, a); CheckReduced(half, (x + (x.IsEven ? 0 : P)) / 2, "Fp halve raw BigInteger");
        alias = a; Fp.Halve(out alias, alias); Check(Fp.AreEqual(alias, half), "Fp halve alias");
        Fp.Negate(out Fp negative, a); CheckReduced(negative, -x, "Fp negate raw BigInteger");
        if (NativeAvailable) { KernelNative.blst_fp_cneg(out Fp native, a, 1); Check(Fp.AreEqual(negative, native), "Fp negate native"); }
        alias = a; Fp.Negate(out alias, alias); Check(Fp.AreEqual(alias, negative), "Fp negate alias");
    }
    private static void TestFp2(in Fp2 a, in Fp2 b)
    {
        Fp2.Multiply(out Fp2 product, a, b); Fp2 native;
        if (NativeAvailable) { Native.blst_fp2_mul(out native, a, b); Check(Fp2.AreEqual(product, native), "Fp2 multiply native"); }
        CheckReduced(product.C0, (Raw(a.C0) * Raw(b.C0) - Raw(a.C1) * Raw(b.C1)) * RInverse, "Fp2 real BigInteger");
        CheckReduced(product.C1, (Raw(a.C0) * Raw(b.C1) + Raw(a.C1) * Raw(b.C0)) * RInverse, "Fp2 imaginary BigInteger");
        Fp2 alias = a; Fp2.Multiply(out alias, alias, b); Check(Fp2.AreEqual(alias, product), "Fp2 multiply left alias");
        alias = b; Fp2.Multiply(out alias, a, alias); Check(Fp2.AreEqual(alias, product), "Fp2 multiply right alias");
        Fp2.Square(out Fp2 square, a);
        if (NativeAvailable) { Native.blst_fp2_sqr(out native, a); Check(Fp2.AreEqual(square, native), "Fp2 square native"); }
        CheckReduced(square.C0, (Raw(a.C0) * Raw(a.C0) - Raw(a.C1) * Raw(a.C1)) * RInverse, "Fp2 square real BigInteger");
        CheckReduced(square.C1, 2 * Raw(a.C0) * Raw(a.C1) * RInverse, "Fp2 square imaginary BigInteger");
        alias = a; Fp2.Square(out alias, alias); Check(Fp2.AreEqual(alias, square), "Fp2 square alias");
        alias = a; Fp2.MultiplyByFp(out alias, alias, alias.C0); Fp2.MultiplyByFp(out native, a, a.C0); Check(Fp2.AreEqual(alias, native), "Fp2 scalar inside result C0 alias");
        alias = a; Fp2.MultiplyByFp(out alias, alias, alias.C1); Fp2.MultiplyByFp(out native, a, a.C1); Check(Fp2.AreEqual(alias, native), "Fp2 scalar inside result C1 alias");
        Fp2.Add(out Fp2 sum, a, b);
        CheckReduced(sum.C0, Raw(a.C0) + Raw(b.C0), "Fp2 add real BigInteger");
        CheckReduced(sum.C1, Raw(a.C1) + Raw(b.C1), "Fp2 add imaginary BigInteger");
        alias = a; Fp2.Add(out alias, alias, b); Check(Fp2.AreEqual(alias, sum), "Fp2 add left alias");
        alias = b; Fp2.Add(out alias, a, alias); Check(Fp2.AreEqual(alias, sum), "Fp2 add right alias");
        Fp2.Subtract(out Fp2 difference, a, b);
        CheckReduced(difference.C0, Raw(a.C0) - Raw(b.C0), "Fp2 subtraction real BigInteger");
        CheckReduced(difference.C1, Raw(a.C1) - Raw(b.C1), "Fp2 subtraction imaginary BigInteger");
        alias = a; Fp2.Subtract(out alias, alias, b); Check(Fp2.AreEqual(alias, difference), "Fp2 subtraction left alias");
        alias = b; Fp2.Subtract(out alias, a, alias); Check(Fp2.AreEqual(alias, difference), "Fp2 subtraction right alias");
        alias = a; Fp2.Negate(out alias, alias);
        CheckReduced(alias.C0, -Raw(a.C0), "Fp2 negate real alias");
        CheckReduced(alias.C1, -Raw(a.C1), "Fp2 negate imaginary alias");
        alias = a; Fp2.MulBy3(out alias, alias);
        CheckReduced(alias.C0, Raw(a.C0) * 3, "Fp2 triple real alias");
        CheckReduced(alias.C1, Raw(a.C1) * 3, "Fp2 triple imaginary alias");
        alias = a; Fp2.MulBy4(out alias, alias);
        CheckReduced(alias.C0, Raw(a.C0) * 4, "Fp2 quadruple real alias");
        CheckReduced(alias.C1, Raw(a.C1) * 4, "Fp2 quadruple imaginary alias");
        alias = a; Fp2.MulBy8(out alias, alias);
        CheckReduced(alias.C0, Raw(a.C0) * 8, "Fp2 octuple real alias");
        CheckReduced(alias.C1, Raw(a.C1) * 8, "Fp2 octuple imaginary alias");
        alias = a; Fp2.MulByNonResidue(out alias, alias);
        CheckReduced(alias.C0, Raw(a.C0) - Raw(a.C1), "Fp2 nonresidue real alias");
        CheckReduced(alias.C1, Raw(a.C0) + Raw(a.C1), "Fp2 nonresidue imaginary alias");
        WideTower.Multiply2(out Fp2Wide wide, a, b); WideTower.Reduce2(out Fp2 reduced, wide);
        Check(Fp2.AreEqual(reduced, product), "lazy Fp2 multiply native");
        Check(BigInteger.Abs(Signed(wide.C0)) < P * P && Signed(wide.C1) >= 0 && Signed(wide.C1) < 2 * P * P, "lazy Fp2 product bounds");
    }
}
