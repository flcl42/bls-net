using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class Program
{
    private static void TestCancelledAffineBuckets()
    {
        foreach (int count in new[] { 512, 513, 1024 })
        {
            G1Affine[] points = new G1Affine[count]; byte[] scalars = new byte[count * 32];
            for (int i = 0; i < count; i += 2)
            {
                MapToCurve.MapToG1(RandomFp(), out G1Projective p); points[i] = p.ToAffine();
                if (i + 1 < count) { G1Projective.Negate(p, out p); points[i + 1] = p.ToAffine(); }
            }
            for (int pattern = 0; pattern < 2; pattern++)
            {
                for (int i = 0; i < count; i++) scalars[i * 32] = 1;
                G1Projective expected = count % 2 == 0 ? default : G1Projective.FromAffine(points[^1]);
                if (pattern == 1 && count % 2 != 0) { scalars[(count - 1) * 32] = 2; G1Projective.Double(expected, out expected); }
                G1Projective.MultiScalarMultiply(points, scalars, out G1Projective actual);
                Check(G1Projective.AreEqual(actual, expected) && actual.ToAffine().IsOnCurve(), "G1 cancelled affine bucket identity");
                if (NativeAvailable) { Native.Msm(points, scalars, out expected); Equal(actual, expected, "G1 cancelled affine bucket native"); }
            }
        }
        foreach (int count in new[] { 32, 33, 38, 64, 129 })
        {
            G2Affine[] points = new G2Affine[count]; byte[] scalars = new byte[count * 32];
            for (int i = 0; i < count; i += 2)
            {
                MapToCurve.MapToG2(RandomFp2(), out G2Projective p); points[i] = p.ToAffine();
                if (i + 1 < count) { G2Projective.Negate(p, out p); points[i + 1] = p.ToAffine(); }
            }
            for (int pattern = 0; pattern < 2; pattern++)
            {
                for (int i = 0; i < count; i++) scalars[i * 32] = 1;
                G2Projective expected = count % 2 == 0 ? default : G2Projective.FromAffine(points[^1]);
                if (pattern == 1 && count % 2 != 0) { scalars[(count - 1) * 32] = 2; G2Projective.Double(expected, out expected); }
                G2Projective.MultiScalarMultiply(points, scalars, out G2Projective actual);
                Check(G2Projective.AreEqual(actual, expected) && actual.ToAffine().IsOnCurve(), "G2 cancelled affine bucket identity");
                if (NativeAvailable) { Native.Msm(points, scalars, out expected); Equal(actual, expected, "G2 cancelled affine bucket native"); }
            }
        }
    }
}
