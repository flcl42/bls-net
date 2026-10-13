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
        Console.WriteLine("Tower: 2,500 managed identities and aliases passed.");
    }
}
