using System.Numerics;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static unsafe partial class Program
{
    private static void FusedTowerTests()
    {
        RInverse = BigInteger.ModPow(R % P, P - 2, P);
        BigInteger[] edges = [0, 1, P / 2, P / 2 + 1, P - 2, P - 1];
        foreach (BigInteger a0 in edges)
        foreach (BigInteger a1 in edges)
        foreach (BigInteger b0 in edges)
        foreach (BigInteger b1 in edges)
        {
            FusedSquareCase(new(RawField(a0), RawField(a1)), new(RawField(b0), RawField(b1)));
            FusedDifferenceCase(new(RawField(a0), RawField(a1)), new(RawField(b0), RawField(b1)), new(RawField(b1), RawField(a0)), new(RawField(a1), RawField(b0)));
        }
        for (int i = 0; i < 3000; i++) FusedSquareCase(Field2(), Field2());
        for (int i = 0; i < 3000; i++) FusedDifferenceCase(Field2(), Field2(), Field2(), Field2());
        for (int i = 0; i < 3000; i++) SparsePairCase(Field12(), Field6(), Field6());
        foreach (BigInteger edge in edges)
        {
            Fp2 coefficient = new(RawField(edge), RawField(edge));
            Fp6 line = new() { C0 = coefficient, C1 = coefficient, C2 = coefficient };
            SparsePairCase(new() { C0 = line, C1 = line }, line, line);
        }
        foreach (BigInteger edge in edges) TestRootChain(RawField(edge));
        for (int i = 0; i < 128; i++) TestRootChain(Field());
        Console.WriteLine("Fused tower: wide square bounds, native/algebraic formulas and aliases passed.");
    }

    private static void SparsePairCase(in Fp12 a, in Fp6 b, in Fp6 c)
    {
        Fp12.MultiplyBySparse(out Fp12 expected, a, b); Fp12.MultiplyBySparse(out expected, expected, c);
        SparseTower.MultiplyPair(out Fp12 actual, a, b, c); Equal(actual, expected, "paired sparse algebraic");
        if (NativeAvailable)
        {
            Native.blst_fp12_mul_by_xy00z0(out expected, a, b); Native.blst_fp12_mul_by_xy00z0(out expected, expected, c);
            Equal(actual, expected, "paired sparse native");
        }
        Fp12 alias = a; SparseTower.MultiplyPair(out alias, alias, b, c); Equal(alias, actual, "paired sparse accumulator alias");
        SparseTower.LineProduct(out Fp12 product, b, c);
        Check(product.C1.C0.IsZero, "paired line product shape");
        Fp12 sparse0 = new() { C0 = new() { C0 = b.C0, C1 = b.C1 }, C1 = new() { C1 = b.C2 } };
        Fp12 sparse1 = new() { C0 = new() { C0 = c.C0, C1 = c.C1 }, C1 = new() { C1 = c.C2 } };
        Fp12.Multiply(out expected, sparse0, sparse1); Equal(product, expected, "paired line product full identity");
        alias = new() { C0 = b, C1 = c }; SparseTower.LineProduct(out alias, alias.C0, alias.C1); Equal(alias, product, "paired line product aliases");
        alias = a; Fp12.MultiplyBySparse(out expected, a, a.C0); Fp12.MultiplyBySparse(out expected, expected, a.C1);
        SparseTower.MultiplyPair(out alias, alias, alias.C0, alias.C1); Equal(alias, expected, "paired sparse coefficient aliases");
    }

    private static void TestRootChain(in Fp input)
    {
        Nethermind.Crypto.BlsNet.Hashing.FpCurveRoots.ReciprocalSqrt(out Fp actual, input);
        Fp.PowPMinus3Div4(out Fp expected, input);
        Check(Fp.AreEqual(actual, expected), "fixed reciprocal root chain vs general exponent");
        Fp alias = input; Nethermind.Crypto.BlsNet.Hashing.FpCurveRoots.ReciprocalSqrt(out alias, alias);
        Check(Fp.AreEqual(alias, actual), "fixed reciprocal root chain alias");
    }

    private static void FusedDifferenceCase(in Fp2 a, in Fp2 b, in Fp2 c, in Fp2 d)
    {
        FusedSmallCase(a, b, c);
        FusedSumCase(a, b, c, d);
        FusedBaseCase(a.C0, b.C0, c.C0, d.C0);
        ConstantCase(a, c.C0);
        Fp2.Multiply(out Fp2 t0, a, b); Fp2.Multiply(out Fp2 t1, c, d);
        Fp2.Subtract(out Fp2 expected, t0, t1);
        Fp2Fused.MultiplyDifference(out Fp2 actual, a, b, c, d);
        Check(Fp2.AreEqual(actual, expected), "fused difference algebraic");
        if (NativeAvailable)
        {
            Native.blst_fp2_mul(out t0, a, b); Native.blst_fp2_mul(out t1, c, d);
            Fp2.Subtract(out expected, t0, t1);
            Check(Fp2.AreEqual(actual, expected), "fused difference native");
        }
        CheckReduced(actual.C0, (Raw(a.C0) * Raw(b.C0) - Raw(a.C1) * Raw(b.C1) - Raw(c.C0) * Raw(d.C0) + Raw(c.C1) * Raw(d.C1)) * RInverse, "fused difference real BigInteger");
        CheckReduced(actual.C1, (Raw(a.C0) * Raw(b.C1) + Raw(a.C1) * Raw(b.C0) - Raw(c.C0) * Raw(d.C1) - Raw(c.C1) * Raw(d.C0)) * RInverse, "fused difference imaginary BigInteger");
        Fp2 alias = a; Fp2Fused.MultiplyDifference(out alias, alias, b, c, d); Check(Fp2.AreEqual(alias, actual), "fused difference alias a");
        alias = b; Fp2Fused.MultiplyDifference(out alias, a, alias, c, d); Check(Fp2.AreEqual(alias, actual), "fused difference alias b");
        alias = c; Fp2Fused.MultiplyDifference(out alias, a, b, alias, d); Check(Fp2.AreEqual(alias, actual), "fused difference alias c");
        alias = d; Fp2Fused.MultiplyDifference(out alias, a, b, c, alias); Check(Fp2.AreEqual(alias, actual), "fused difference alias d");
        Fp2Fused.MultiplyDifference(out actual, a, b, a, b); Check(actual.IsZero, "fused difference cancellation");
    }

    private static void FusedBaseCase(in Fp a, in Fp b, in Fp c, in Fp d)
    {
        FpFused.Square(out Fp square, a); CheckReduced(square, Raw(a) * Raw(a) * RInverse, "isolated square BigInteger");
        Fp squareAlias = a; FpFused.Square(out squareAlias, squareAlias); Check(Fp.AreEqual(square, squareAlias), "isolated square alias");
        if (NativeAvailable) { Native.blst_fp_sqr(out Fp expectedSquare, a); Check(Fp.AreEqual(square, expectedSquare), "isolated square native"); }
        FpFused.MultiplyDifference(out Fp difference, a, b, c, d);
        FpFused.MultiplySum(out Fp sum, a, b, c, d);
        CheckReduced(difference, (Raw(a) * Raw(b) - Raw(c) * Raw(d)) * RInverse, "fused Fp difference BigInteger");
        CheckReduced(sum, (Raw(a) * Raw(b) + Raw(c) * Raw(d)) * RInverse, "fused Fp sum BigInteger");
        if (NativeAvailable)
        {
            Native.blst_fp_mul(out Fp t0, a, b); Native.blst_fp_mul(out Fp t1, c, d);
            Fp.Subtract(out Fp expected, t0, t1); Check(Fp.AreEqual(difference, expected), "fused Fp difference native");
            Fp.Add(out expected, t0, t1); Check(Fp.AreEqual(sum, expected), "fused Fp sum native");
        }
        Fp alias = a; FpFused.MultiplyDifference(out alias, alias, b, c, d); Check(Fp.AreEqual(alias, difference), "fused Fp difference alias a");
        alias = b; FpFused.MultiplyDifference(out alias, a, alias, c, d); Check(Fp.AreEqual(alias, difference), "fused Fp difference alias b");
        alias = c; FpFused.MultiplyDifference(out alias, a, b, alias, d); Check(Fp.AreEqual(alias, difference), "fused Fp difference alias c");
        alias = d; FpFused.MultiplyDifference(out alias, a, b, c, alias); Check(Fp.AreEqual(alias, difference), "fused Fp difference alias d");
        alias = a; FpFused.MultiplySum(out alias, alias, b, c, d); Check(Fp.AreEqual(alias, sum), "fused Fp sum alias a");
        alias = b; FpFused.MultiplySum(out alias, a, alias, c, d); Check(Fp.AreEqual(alias, sum), "fused Fp sum alias b");
        alias = c; FpFused.MultiplySum(out alias, a, b, alias, d); Check(Fp.AreEqual(alias, sum), "fused Fp sum alias c");
        alias = d; FpFused.MultiplySum(out alias, a, b, c, alias); Check(Fp.AreEqual(alias, sum), "fused Fp sum alias d");
    }

    private static void ConstantCase(in Fp2 a, in Fp c)
    {
        ExtensionConstants.MultiplySwuZ(out Fp2 actual, a);
        Fp2.Multiply(out Fp2 expected, a, Nethermind.Crypto.BlsNet.Hashing.MapConstants2.Z);
        Check(Fp2.AreEqual(actual, expected), "SSWU Z specialization algebraic");
        if (NativeAvailable) { Native.blst_fp2_mul(out expected, a, Nethermind.Crypto.BlsNet.Hashing.MapConstants2.Z); Check(Fp2.AreEqual(actual, expected), "SSWU Z specialization native"); }
        CheckReduced(actual.C0, Raw(a.C1) - 2 * Raw(a.C0), "SSWU Z real BigInteger");
        CheckReduced(actual.C1, -Raw(a.C0) - 2 * Raw(a.C1), "SSWU Z imaginary BigInteger");
        Fp2 alias = a; ExtensionConstants.MultiplySwuZ(out alias, alias); Check(Fp2.AreEqual(actual, alias), "SSWU Z specialization alias");
        ExtensionConstants.MultiplyEqualPair(out actual, a, c);
        Fp2.Multiply(out expected, a, new(c, c)); Check(Fp2.AreEqual(actual, expected), "equal coefficient algebraic");
        if (NativeAvailable) { Native.blst_fp2_mul(out expected, a, new(c, c)); Check(Fp2.AreEqual(actual, expected), "equal coefficient native"); }
        alias = a; ExtensionConstants.MultiplyEqualPair(out alias, alias, c); Check(Fp2.AreEqual(actual, alias), "equal coefficient alias");
        alias = a; ExtensionConstants.MultiplyEqualPair(out alias, alias, alias.C0); Fp2.Multiply(out expected, a, new(a.C0, a.C0)); Check(Fp2.AreEqual(alias, expected), "equal coefficient scalar alias");
        ExtensionConstants.MultiplyImaginary(out actual, a, c);
        Fp2.Multiply(out expected, a, new(Fp.Zero, c)); Check(Fp2.AreEqual(actual, expected), "imaginary coefficient algebraic");
        if (NativeAvailable) { Native.blst_fp2_mul(out expected, a, new(Fp.Zero, c)); Check(Fp2.AreEqual(actual, expected), "imaginary coefficient native"); }
        alias = a; ExtensionConstants.MultiplyImaginary(out alias, alias, c); Check(Fp2.AreEqual(actual, alias), "imaginary coefficient alias");
        alias = a; ExtensionConstants.MultiplyImaginary(out alias, alias, alias.C1); Fp2.Multiply(out expected, a, new(Fp.Zero, a.C1)); Check(Fp2.AreEqual(alias, expected), "imaginary coefficient scalar alias");
    }

    private static void FusedSmallCase(in Fp2 a, in Fp2 b, in Fp2 c)
    {
        Fp2Fused.Subtract(out Fp2 r, a, b);
        Fp2.Subtract(out Fp2 expected, a, b); Check(Fp2.AreEqual(r, expected), "fused subtract algebraic");
        if (NativeAvailable) { Native.blst_fp2_sub(out expected, a, b); Check(Fp2.AreEqual(r, expected), "fused subtract native"); }
        Fp2 alias = a; Fp2Fused.Subtract(out alias, alias, b); Check(Fp2.AreEqual(r, alias), "fused subtract alias a");
        alias = b; Fp2Fused.Subtract(out alias, a, alias); Check(Fp2.AreEqual(r, alias), "fused subtract alias b");
        Fp2Fused.SubtractSum(out r, a, b, c);
        Fp2.Subtract(out expected, a, b); Fp2.Subtract(out expected, expected, c);
        Check(Fp2.AreEqual(r, expected), "fused subtract sum algebraic");
        if (NativeAvailable) { Native.blst_fp2_sub(out expected, a, b); Native.blst_fp2_sub(out expected, expected, c); Check(Fp2.AreEqual(r, expected), "fused subtract sum native"); }
        CheckReduced(r.C0, Raw(a.C0) - Raw(b.C0) - Raw(c.C0), "fused subtract sum real BigInteger");
        CheckReduced(r.C1, Raw(a.C1) - Raw(b.C1) - Raw(c.C1), "fused subtract sum imaginary BigInteger");
        alias = a; Fp2Fused.SubtractSum(out alias, alias, b, c); Check(Fp2.AreEqual(r, alias), "fused subtract sum alias a");
        alias = b; Fp2Fused.SubtractSum(out alias, a, alias, c); Check(Fp2.AreEqual(r, alias), "fused subtract sum alias b");
        alias = c; Fp2Fused.SubtractSum(out alias, a, b, alias); Check(Fp2.AreEqual(r, alias), "fused subtract sum alias c");
        Fp2Fused.SubtractSum(out r, a, a, a); Fp2.Negate(out expected, a);
        Check(Fp2.AreEqual(r, expected), "fused subtract sum identical inputs");
    }

    private static void FusedSumCase(in Fp2 a, in Fp2 b, in Fp2 c, in Fp2 d)
    {
        Fp2.Multiply(out Fp2 t0, a, b); Fp2.Multiply(out Fp2 t1, c, d); Fp2.Add(out Fp2 expected, t0, t1);
        Fp2Fused.MultiplySum(out Fp2 actual, a, b, c, d);
        Check(Fp2.AreEqual(actual, expected), "fused product sum algebraic");
        if (NativeAvailable) { Native.blst_fp2_mul(out t0, a, b); Native.blst_fp2_mul(out t1, c, d); Fp2.Add(out expected, t0, t1); Check(Fp2.AreEqual(actual, expected), "fused product sum native"); }
        CheckReduced(actual.C0, (Raw(a.C0) * Raw(b.C0) - Raw(a.C1) * Raw(b.C1) + Raw(c.C0) * Raw(d.C0) - Raw(c.C1) * Raw(d.C1)) * RInverse, "fused product sum real BigInteger");
        CheckReduced(actual.C1, (Raw(a.C0) * Raw(b.C1) + Raw(a.C1) * Raw(b.C0) + Raw(c.C0) * Raw(d.C1) + Raw(c.C1) * Raw(d.C0)) * RInverse, "fused product sum imaginary BigInteger");
        Fp2 alias = a; Fp2Fused.MultiplySum(out alias, alias, b, c, d); Check(Fp2.AreEqual(alias, actual), "fused sum alias a");
        alias = b; Fp2Fused.MultiplySum(out alias, a, alias, c, d); Check(Fp2.AreEqual(alias, actual), "fused sum alias b");
        alias = c; Fp2Fused.MultiplySum(out alias, a, b, alias, d); Check(Fp2.AreEqual(alias, actual), "fused sum alias c");
        alias = d; Fp2Fused.MultiplySum(out alias, a, b, c, alias); Check(Fp2.AreEqual(alias, actual), "fused sum alias d");
    }

    private static void FusedSquareCase(in Fp2 a, in Fp2 b)
    {
        Fp2Fused.SquareWide(out Fp2Wide wide, a);
        Check(Signed(wide.C0) >= 0 && Signed(wide.C0) < 2 * P * P, "wide square real bound");
        Check(Signed(wide.C1) >= 0 && Signed(wide.C1) < 2 * P * P, "wide square imaginary bound");
        WideTower.Reduce2Small(out Fp2 square, wide);
        CheckReduced(square.C0, (Raw(a.C0) * Raw(a.C0) - Raw(a.C1) * Raw(a.C1)) * RInverse, "wide square real BigInteger");
        CheckReduced(square.C1, 2 * Raw(a.C0) * Raw(a.C1) * RInverse, "wide square imaginary BigInteger");
        Fp2.Square(out Fp2 expected, a);
        Check(Fp2.AreEqual(square, expected), "wide square narrow identity");
        if (NativeAvailable) { Native.blst_fp2_sqr(out expected, a); Check(Fp2.AreEqual(square, expected), "wide square native"); }
        Fp6 value = new() { C0 = a, C1 = b, C2 = new(a.C1, b.C0) };
        WideTower.Square6(out Fp6 actual6, value); Fp6.Multiply(out Fp6 expected6, value, value);
        Check(Fp6.AreEqual(actual6, expected6), "wide Fp6 square algebraic");
        if (NativeFp6Available) { Native.blst_fp6_sqr(out expected6, value); Check(Fp6.AreEqual(actual6, expected6), "wide Fp6 square native"); }
        WideTower.Square6(out value, value); Check(Fp6.AreEqual(actual6, value), "wide Fp6 square alias");
    }
}
