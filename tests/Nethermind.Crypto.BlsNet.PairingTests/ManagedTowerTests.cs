using Nethermind.Crypto.BlsNet.Arithmetic;

internal static unsafe partial class Program
{
    private static void ManagedTowerTests()
    {
        for (int i = 0; i < 2500; i++)
        {
            Fp12 a = Field12(), b = Field12();
            Fp12.Multiply(out Fp12 product, a, b);
            Fp12.Multiply(out Fp12 reverse, b, a); Equal(product, reverse, "tower commutativity");
            Fp12 alias = a; Fp12.Multiply(out alias, alias, b); Equal(alias, product, "tower multiply alias");
            Fp12.Square(out Fp12 square, a); Fp12.Multiply(out reverse, a, a); Equal(square, reverse, "tower square vs multiply");
            alias = a; Fp12.Square(out alias, alias); Equal(alias, square, "tower square alias");
            Fp12.Inverse(out Fp12 inverse, a); Fp12.Multiply(out reverse, a, inverse); Check(reverse.IsOne, "tower inverse product");
            Fp12.Conjugate(out reverse, a); Fp12.Conjugate(out reverse, reverse); Equal(reverse, a, "tower conjugation");
            Fp12.Add(out reverse, a, b); Fp12.Subtract(out reverse, reverse, b); Equal(reverse, a, "tower add/subtract");
            Fp6 packed = Field6();
            Fp12.MultiplyBySparse(out reverse, a, packed);
            Fp12 sparse = new() { C0 = new Fp6 { C0 = packed.C0, C1 = packed.C1 }, C1 = new Fp6 { C1 = packed.C2 } };
            Fp12.Multiply(out product, a, sparse); Equal(reverse, product, "tower sparse vs full");
            Fp12.FrobeniusMap(out reverse, a, 1); Fp12.FrobeniusMap(out reverse, reverse, 1);
            Fp12.FrobeniusMap(out product, a, 2); Equal(reverse, product, "tower Frobenius composition");
        }
        Fp12.Inverse(out Fp12 zero, Fp12.Zero); Check(zero.IsZero, "tower inverse zero");
        CompressedSquareTests();
        Console.WriteLine("Tower: 2,500 managed identities and aliases passed.");
    }

    private static void CompressedSquareTests()
    {
        for (int trial = 0; trial < 128; trial++)
        {
            Fp12 input = trial == 0 ? Fp12.One : trial == 1 ? Fp12.Zero : Field12();
            if (trial > 1)
            {
                // The easy part enters the entire cyclotomic subgroup, rather
                // than restricting this test to the prime-order target group.
                Fp12.Conjugate(out Fp12 conjugate, input);
                Fp12.Inverse(out Fp12 inverse, input);
                Fp12.Multiply(out input, conjugate, inverse);
                Fp12.FrobeniusMap(out inverse, input, 2);
                Fp12.Multiply(out input, input, inverse);
            }
            foreach (int count in new[] { 0, 1, 2, 9, 14, 15, 16, 32, 63, 128 })
            {
                Fp12 expected = input;
                for (int i = 0; i < count; i++) Fp12.Square(out expected, expected);
                Fp12.CyclotomicSquareRepeated(out Fp12 actual, input, count);
                Equal(actual, expected, "compressed square vs ordinary square");
                Fp12 alias = input;
                Fp12.CyclotomicSquareRepeated(out alias, alias, count);
                Equal(alias, expected, "compressed square alias");
                if (NativeAvailable)
                {
                    Fp12 native = input;
                    for (int i = 0; i < count; i++) Native.blst_fp12_cyclotomic_sqr(out native, native);
                    Equal(actual, native, "compressed square vs native");
                }
            }
        }
    }
}
