using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class CurveTests
{
    private static void TestDoubleRuns()
    {
        Fp.Multiply(out Fp beta, CurveConstants.frobenius_x.C1, CurveConstants.frobenius_x.C1);
        Check(Fp.AreEqual(beta, CurveConstants.Beta), "psi squared X coefficient");
        Fp.Square(out Fp normY, CurveConstants.frobenius_y.C0); Fp.Double(out normY, normY);
        Fp.Negate(out Fp negativeOne, Fp.One);
        Check(Fp.AreEqual(normY, negativeOne), "psi squared Y coefficient");
        int[] counts = [0, 1, 2, 3, 9, 16, 32, 63];
        foreach (int count in counts)
        {
            TestDoubleRun(default(Jacobian<Fp>), count);
            TestDoubleRun(new Jacobian<Fp>(Fp.One, Fp.One, Fp.Zero), count);
            TestDoubleRun(new Jacobian<Fp>(Fp.One, Fp.Zero, Fp.One), count);
            TestDoubleRun(default(Jacobian<Fp2>), count);
            TestDoubleRun(new Jacobian<Fp2>(Fp2.One, Fp2.One, Fp2.Zero), count);
            TestDoubleRun(new Jacobian<Fp2>(Fp2.One, Fp2.Zero, Fp2.One), count);
        }
        for (int i = 0; i < 128; i++)
        {
            Fp z = RandomFp(); Fp2 z2 = RandomFp2();
            MapToCurve.MapToG1(z, out G1Projective p);
            MapToCurve.MapToG2(z2, out G2Projective q);
            Fp.Square(out Fp zz, z); Fp.Multiply(out Fp zzz, zz, z);
            Fp.Multiply(out p.Value.X, p.X, zz); Fp.Multiply(out p.Value.Y, p.Y, zzz); Fp.Multiply(out p.Value.Z, p.Z, z);
            Fp2.Square(out Fp2 zz2, z2); Fp2.Multiply(out Fp2 zzz2, zz2, z2);
            Fp2.Multiply(out q.Value.X, q.X, zz2); Fp2.Multiply(out q.Value.Y, q.Y, zzz2); Fp2.Multiply(out q.Value.Z, q.Z, z2);
            int count = counts[i % counts.Length];
            TestDoubleRun(p.Value, count); TestDoubleRun(q.Value, count);
            G2Projective.Psi(out Jacobian<Fp2> twicePsi, q.Value); G2Projective.Psi(out twicePsi, twicePsi);
            G2Projective.PsiSquared(out Jacobian<Fp2> directPsi2, q.Value);
            Check(Fp2.AreEqual(twicePsi.X, directPsi2.X) && Fp2.AreEqual(twicePsi.Y, directPsi2.Y) && Fp2.AreEqual(twicePsi.Z, directPsi2.Z), "psi squared exact coordinates");
            Jacobian<Fp2> psiAlias = q.Value; G2Projective.PsiSquared(out psiAlias, psiAlias);
            Check(Fp2.AreEqual(twicePsi.X, psiAlias.X) && Fp2.AreEqual(twicePsi.Y, psiAlias.Y) && Fp2.AreEqual(twicePsi.Z, psiAlias.Z), "psi squared alias");
            if (NativeAvailable)
            {
                G1Projective native1 = p; G2Projective native2 = q;
                for (int j = 0; j < count; j++)
                { CurveNative.blst_p1_double(out native1, native1); CurveNative.blst_p2_double(out native2, native2); }
                Jacobian<Fp>.DoubleN(out p.Value, p.Value, count);
                Jacobian<Fp2>.DoubleN(out q.Value, q.Value, count);
                Check(G1Projective.AreEqual(p, native1), "G1 doubling run native");
                Check(G2Projective.AreEqual(q, native2), "G2 doubling run native");
            }
        }
    }

    private static void TestDoubleRun<T>(Jacobian<T> p, int count) where T : struct, IField<T>
    {
        Jacobian<T> expected = p;
        for (int i = 0; i < count; i++) Jacobian<T>.Double(out expected, expected);
        Jacobian<T>.DoubleN(out Jacobian<T> actual, p, count);
        Check(T.AreEqual(expected.X, actual.X) && T.AreEqual(expected.Y, actual.Y) && T.AreEqual(expected.Z, actual.Z), "doubling run coordinates");
        Jacobian<T> alias = p; Jacobian<T>.DoubleN(out alias, alias, count);
        Check(T.AreEqual(expected.X, alias.X) && T.AreEqual(expected.Y, alias.Y) && T.AreEqual(expected.Z, alias.Z), "doubling run alias");
    }
}
