using System.Numerics;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static unsafe partial class TowerTests
{
    private static void SquareRunTests()
    {
        Check(4 * P * P < P * R && P + 4 * P * P / R < 2 * P, "lazy square inductive bound");
        Check(8 * P * P < P * R, "ARM64 lazy Fp4 square reduction bound");
        BigInteger[] edges = [0, 1, 2, P / 2, P - 2, P - 1, P, P + 1, 2 * P - 2, 2 * P - 1];
        foreach (BigInteger edge in edges)
            foreach (int count in new[] { 0, 1, 2, 3, 5, 7, 12, 32, 63, 128, 380 })
                TestSquareRun(RawField(edge), Field(), count);
        for (int k = 0; k <= 8; k++)
            for (int delta = -1; delta <= 1; delta++)
            {
                BigInteger edge = k * P / 8 + delta;
                if (edge >= 0 && edge < P) TestSmallMultiple(RawField(edge));
            }
        for (int i = 0; i < 512; i++)
        {
            TestSquareRun(RawField(RandomInteger(48) % (2 * P)), Field(), i % 33);
            Fp2 x = Field2(), y = Field2();
            Fp12.SquareFp4Arm64(out Fp2 c0, out Fp2 c1, x, y);
            Fp2.Square(out Fp2 x2, x); Fp2.Square(out Fp2 y2, y);
            Fp2.MulByNonResidue(out y2, y2); Fp2.Add(out x2, x2, y2);
            Fp2.Multiply(out Fp2 xy, x, y); Fp2.Double(out xy, xy);
            Check(Fp2.AreEqual(c0, x2) && Fp2.AreEqual(c1, xy), "ARM64 lazy Fp4 square vs independent formula");
            Fp2 xa = x, ya = y;
            Fp12.SquareFp4Arm64(out xa, out ya, xa, ya);
            Check(Fp2.AreEqual(xa, c0) && Fp2.AreEqual(ya, c1), "ARM64 lazy Fp4 square input aliases");
            Fp a = Field(), b = Field();
            TestSmallMultiple(a);
            Fp.SubtractBiased(out Fp biased, a, b);
            Check(Raw(biased) == Raw(a) + P - Raw(b) && Raw(biased) < 2 * P, "biased subtraction bounds");
            Fp alias = a; Fp.SubtractBiased(out alias, alias, b);
            Check(Raw(alias) == Raw(biased), "biased subtraction left alias");
            alias = b; Fp.SubtractBiased(out alias, a, alias);
            Check(Raw(alias) == Raw(biased), "biased subtraction right alias");
        }
    }

    private static void TestSmallMultiple(Fp a)
    {
        Fp.MulBy8Fast(out Fp result, a);
        CheckReduced(result, Raw(a) * 8, "shift by eight vs BigInteger");
        Fp alias = a; Fp.MulBy8Fast(out alias, alias);
        Check(Fp.AreEqual(result, alias), "shift by eight alias");
        Fp.MulBy8(out Fp reference, a);
        Check(Fp.AreEqual(result, reference), "shift by eight vs canonical doubles");
        if (NativeAvailable)
        {
            KernelNative.blst_fp_add(out reference, a, a);
            KernelNative.blst_fp_add(out reference, reference, reference);
            KernelNative.blst_fp_add(out reference, reference, reference);
            Check(Fp.AreEqual(result, reference), "shift by eight native");
        }
    }

    private static void TestSquareRun(Fp a, Fp factor, int count)
    {
        Fp.SquareLazyArm64(out Fp lazy, a);
        Check(Raw(lazy) < 2 * P && Mod(Raw(lazy)) == Mod(Raw(a) * Raw(a) * RInverse), "ARM64 lazy square bound and value");
        Fp lazyAlias = a; Fp.SquareLazyArm64(out lazyAlias, lazyAlias);
        Check(Raw(lazyAlias) == Raw(lazy), "ARM64 lazy square alias");
        BigInteger expected = Mod(Raw(a) * RInverse);
        expected = BigInteger.ModPow(expected, BigInteger.One << count, P) * (R % P) % P;
        Fp.SquareN(out Fp power, a, count);
        CheckReduced(power, expected, "square run vs BigInteger");
        Fp alias = a; Fp.SquareN(out alias, alias, count);
        Check(Fp.AreEqual(alias, power), "square run alias");
        Fp.SquareNThenMultiply(out Fp product, a, count, factor);
        CheckReduced(product, expected * Raw(factor) * RInverse, "square run multiply vs BigInteger");
        alias = a; Fp.SquareNThenMultiply(out alias, alias, count, factor);
        Check(Fp.AreEqual(alias, product), "square run multiply input alias");
        alias = factor; Fp.SquareNThenMultiply(out alias, a, count, alias);
        Check(Fp.AreEqual(alias, product), "square run multiply factor alias");
        Fp.SquareNThenMultiply(out product, a, count, a);
        alias = a; Fp.SquareNThenMultiply(out alias, alias, count, alias);
        CheckReduced(alias, expected * Raw(a) * RInverse, "square run multiply all aliases");
        Check(Fp.AreEqual(alias, product), "square run multiply shared operands");
        Fp.SquareNThenMultiplyArm64(out Fp armProduct, a, count, factor);
        CheckReduced(armProduct, expected * Raw(factor) * RInverse, "ARM64 lazy square run vs BigInteger");
        alias = a; Fp.SquareNThenMultiplyArm64(out alias, alias, count, factor);
        Check(Fp.AreEqual(alias, armProduct), "ARM64 lazy square run input alias");
        alias = factor; Fp.SquareNThenMultiplyArm64(out alias, a, count, alias);
        Check(Fp.AreEqual(alias, armProduct), "ARM64 lazy square run factor alias");
        alias = a; Fp.SquareNThenMultiplyArm64(out alias, alias, count, alias);
        Check(Fp.AreEqual(alias, product), "ARM64 lazy square run all aliases");
        if (NativeAvailable)
        {
            // Normalize the deliberately lazy input before using the canonical native API.
            Fp native = RawField(Mod(Raw(a)));
            for (int i = 0; i < count; i++) KernelNative.blst_fp_sqr(out native, native);
            Check(Fp.AreEqual(native, power), "square run vs native");
            KernelNative.blst_fp_mul(out native, native, factor);
            Fp.SquareNThenMultiply(out product, a, count, factor);
            Check(Fp.AreEqual(native, product), "square run multiply vs native");
        }
    }
}
