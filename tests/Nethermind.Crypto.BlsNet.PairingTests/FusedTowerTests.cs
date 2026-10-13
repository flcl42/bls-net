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
        foreach (BigInteger edge in edges) TestRootChain(RawField(edge));
        for (int i = 0; i < 128; i++) TestRootChain(Field());
        Console.WriteLine("Fused tower: wide square bounds, native/algebraic formulas and aliases passed.");
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
