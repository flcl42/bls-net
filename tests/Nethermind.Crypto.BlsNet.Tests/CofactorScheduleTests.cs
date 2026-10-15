using System.Numerics;
using System.Globalization;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class CurveTests
{
    private static void CofactorScheduleTests()
    {
        BigInteger z = -BigInteger.Parse("0d201000000010000", NumberStyles.HexNumber);
        BigInteger r = BigInteger.Pow(z, 4) - z * z + 1;
        BigInteger h1 = BigInteger.Pow(z - 1, 2) / 3;
        BigInteger p = h1 * r + z;
        // BLS12-381 twist cofactor, independently tabulated in CFRG
        // pairing-friendly-curves-14 section 4.2.1 (see docs/cofactor-schedule.md).
        BigInteger h2 = BigInteger.Parse("05d543a95414e7f1091d50792876a202cd91de4547085abaa68a205b2e5a7ddfa628f1cb4d9e82ef21537e293a6691ae1616ec6e786f0c70cf1c38e31c7238e5", NumberStyles.HexNumber);
        Check(h2 == (BigInteger.Pow(z, 8) - 4 * BigInteger.Pow(z, 7) + 5 * BigInteger.Pow(z, 6) - 4 * BigInteger.Pow(z, 4) + 6 * BigInteger.Pow(z, 3) - 4 * z * z - 4 * z + 13) / 9, "twist cofactor polynomial");
        BigInteger n2 = r * h2, t = z + 1, k = 2;
        BigInteger n1 = h1 * r;
        int step = 0;
        foreach (int doubles in new[] { 2, 3, 9, 32, 16 })
        {
            Check(BigInteger.GreatestCommonDivisor(k - 1, n2).IsOne, "G2 fixed-chain addition cannot double");
            Check(BigInteger.GreatestCommonDivisor(k - 1, n1) == new[] { 1, 11, 1, 1, 3 }[step++], "G1 fixed-chain doubling exceptions");
            k = (k + 1) << doubles;
        }
        Check(k == -z, "fixed -z schedule");
        Check(BigInteger.GreatestCommonDivisor(-z - 1, h1 * r).IsOne && BigInteger.GreatestCommonDivisor(-z - 1, n2).IsOne, "1-z sum cannot double");
        TestFullBasePoint(default, z, n1);
        TestFullBasePoint(new Jacobian<Fp>(Fp.One, Fp.One, Fp.Zero), z, n1);
        bool saw3 = false, saw11 = false;
        for (int i = 0; i < 64; i++)
        {
            Fp x, y;
            do
            {
                x = RandomFp(); Fp.Square(out Fp rhs, x); Fp.Multiply(out rhs, rhs, x);
                Fp.Add(out rhs, rhs, Fp.FromUInt64(4));
                if (Fp.Sqrt(out y, rhs)) break;
            } while (true);
            Jacobian<Fp> q = new(x, y, Fp.One);
            TestFullBasePoint(q, z, n1);
            foreach (int prime in new[] { 3, 11 })
            {
                BigInteger rest = n1;
                while (rest % prime == 0) rest /= prime;
                Jacobian<Fp> torsion = NaiveScale(q, rest);
                if (torsion.IsInfinity) continue;
                while (!NaiveScale(torsion, prime).IsInfinity) torsion = NaiveScale(torsion, prime);
                TestFullBasePoint(torsion, z, n1);
                BigInteger exceptionalK = prime == 11 ? 12 : BigInteger.Parse("0d20100000000", NumberStyles.HexNumber);
                Check(Jacobian<Fp>.AreEqual(NaiveScale(torsion, exceptionalK), torsion), "G1 retained addition actually doubles on torsion");
                if (prime == 3) saw3 = true; else saw11 = true;
            }
        }
        Check(saw3 && saw11, "G1 cofactor tests cover 3- and 11-torsion");
        BigInteger Norm(BigInteger a, BigInteger b) => a * a + a * b * t + b * b * p;
        Check(BigInteger.GreatestCommonDivisor(Norm(1 - 2 * p, 2 * t), n2).IsOne, "first cofactor sum cannot double");
        Check(BigInteger.GreatestCommonDivisor(Norm(-2 * p - 1, 2 * t + 1), n2) == 13, "second cofactor sum retains doubling handling");
        Check(BigInteger.GreatestCommonDivisor(Norm(1 - z, 1), n2).IsOne, "middle cofactor sum cannot double");
        Check(BigInteger.GreatestCommonDivisor(Norm(-2 * p - 1 - z * z + z, 2 * t - 1 - z), n2).IsOne, "last cofactor sum cannot double");
        BigInteger effective = BigInteger.Parse("0bc69f08f2ee75b3584c6a0ea91b352888e2a8e9145ad7689986ff031508ffe1329c2f178731db956d82bf015d1212b02ec0ec69d7477c1ae954cbc06689f6a359894c0adebbf6b4e8020005aaa95551", NumberStyles.HexNumber);
        TestFullTwistPoint(default, effective, z, p, t, n2);
        TestFullTwistPoint(new Jacobian<Fp2>(Fp2.One, Fp2.One, Fp2.Zero), effective, z, p, t, n2);
        bool saw13 = false, sawExceptional = false;
        for (int i = 0; i < 64; i++)
        {
            Fp2 x, y;
            do
            {
                x = RandomFp2(); Fp2.Square(out Fp2 rhs, x); Fp2.Multiply(out rhs, rhs, x);
                Fp2.Add(out rhs, rhs, new Fp2(Fp.FromUInt64(4), Fp.FromUInt64(4)));
                if (FieldRoots.Sqrt(out y, rhs)) break;
            } while (true);
            Jacobian<Fp2> q = new(x, y, Fp2.One);
            TestFullTwistPoint(q, effective, z, p, t, n2);
            // The full group order contains 13^2. Project onto the 13-primary
            // part, then onto its order-13 subgroup if that part is cyclic.
            Jacobian<Fp2> torsion = NaiveScale(q, n2 / (13 * 13));
            if (!NaiveScale(torsion, 13).IsInfinity) torsion = NaiveScale(torsion, 13);
            Check(NaiveScale(torsion, 13).IsInfinity, "13-primary projection");
            // psi has eigenvalues 7 and 11 modulo 13. The second addition can
            // double on the eigenvalue-7 line; (4+2*psi) projects onto that line.
            G2Projective.Psi(out Jacobian<Fp2> psi13, torsion);
            Jacobian<Fp2> projected = NaiveScale(psi13, 2);
            Jacobian<Fp2>.Add(out torsion, NaiveScale(torsion, 4), projected);
            if (!torsion.IsInfinity)
            {
                saw13 = true; TestFullTwistPoint(torsion, effective, z, p, t, n2);
                Jacobian<Fp2>.Double(out Jacobian<Fp2> twicePsi2, torsion);
                G2Projective.Psi(out twicePsi2, twicePsi2); G2Projective.Psi(out twicePsi2, twicePsi2);
                Jacobian<Fp2>.Negate(out Jacobian<Fp2> negative, torsion);
                Jacobian<Fp2>.Add(out Jacobian<Fp2> left, twicePsi2, negative);
                G2Projective.Psi(out Jacobian<Fp2> right, negative);
                sawExceptional |= Jacobian<Fp2>.AreEqual(left, right);
            }
        }
        Check(saw13 && sawExceptional, "cofactor tests exercise actual 13-torsion doubling");
    }

    // Independent binary group law, valid on the full curve rather than just its prime subgroup.
    private static Jacobian<T> NaiveScale<T>(Jacobian<T> p, BigInteger scalar) where T : struct, IField<T>
    {
        if (scalar.Sign < 0) { Jacobian<T>.Negate(out p, p); scalar = -scalar; }
        Jacobian<T> result = default;
        while (scalar > 0)
        {
            if (!scalar.IsEven) Jacobian<T>.Add(out result, result, p);
            Jacobian<T>.Double(out p, p); scalar >>= 1;
        }
        return result;
    }

    private static void TestFullBasePoint(Jacobian<Fp> q, BigInteger z, BigInteger n1)
    {
        Check(NaiveScale(q, n1).IsInfinity, "full base curve order");
        Jacobian<Fp>.TimesMinusZConstantTime(out Jacobian<Fp> actual, q);
        Check(Jacobian<Fp>.AreEqual(actual, NaiveScale(q, -z)), "fixed -z on full base curve");
        Jacobian<Fp> alias = q; Jacobian<Fp>.TimesMinusZConstantTime(out alias, alias);
        Check(Jacobian<Fp>.AreEqual(actual, alias), "G1 fixed -z point alias");
        Jacobian<Fp>.TimesOneMinusZConstantTime(out actual, q);
        Check(Jacobian<Fp>.AreEqual(actual, NaiveScale(q, 1 - z)), "cofactor on full base curve");
        alias = q; Jacobian<Fp>.TimesOneMinusZConstantTime(out alias, alias);
        Check(Jacobian<Fp>.AreEqual(actual, alias), "G1 cofactor point alias");
    }

    private static void TestFullTwistPoint(Jacobian<Fp2> q, BigInteger effective, BigInteger z, BigInteger p, BigInteger t, BigInteger n2)
    {
        Check(NaiveScale(q, n2).IsInfinity, "tabulated full twist order");
        G2Projective.Psi(out Jacobian<Fp2> psi, q); G2Projective.Psi(out Jacobian<Fp2> psi2, psi);
        Jacobian<Fp2> term = NaiveScale(psi, -t), baseTerm = NaiveScale(q, p);
        Jacobian<Fp2>.Add(out term, term, psi2); Jacobian<Fp2>.Add(out term, term, baseTerm);
        Check(term.IsInfinity, "psi characteristic polynomial on full twist");
        Jacobian<Fp2>.TimesMinusZConstantTime(out Jacobian<Fp2> actual, q);
        Check(Jacobian<Fp2>.AreEqual(actual, NaiveScale(q, -z)), "fixed -z on full twist");
        MapToG2.ClearCofactorConstantTime(out actual, q);
        Jacobian<Fp2> expected = NaiveScale(q, effective);
        Check(Jacobian<Fp2>.AreEqual(actual, expected), "cofactor on full twist vs effective scalar");
        Jacobian<Fp2> alias = q; MapToG2.ClearCofactorConstantTime(out alias, alias);
        Check(Jacobian<Fp2>.AreEqual(actual, alias), "cofactor point alias");
        if (NativeAvailable)
        {
            G2Projective native = default, power = new(q);
            for (BigInteger scalar = effective; scalar > 0; scalar >>= 1)
            {
                if (!scalar.IsEven) CurveNative.blst_p2_add_or_double(out native, native, power);
                CurveNative.blst_p2_double(out power, power);
            }
            Check(Jacobian<Fp2>.AreEqual(actual, native.Value), "full twist cofactor native group law");
        }
    }
}
