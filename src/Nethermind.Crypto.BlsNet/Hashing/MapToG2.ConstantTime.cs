// Secret-input SSWU ported from blst map_to_g2.c (Apache-2.0).
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using static Nethermind.Crypto.BlsNet.Hashing.MapConstants2;

namespace Nethermind.Crypto.BlsNet.Hashing;

public static partial class MapToG2
{
    internal static void MapConstantTime(in Fp2 u, out G2Projective r)
    {
        Fp2.Square(out Fp2 uu, u); ExtensionConstants.MultiplySwuZ(out Fp2 zuu, uu);
        Fp2.Square(out Fp2 tv2, zuu); Fp2.Add(out tv2, tv2, zuu);
        Fp2.Add(out Fp2 x1, tv2, Fp2.One); ExtensionConstants.MultiplyEqualPair(out x1, x1, Bprime_E2.C0);
        Fp2.Multiply(out Fp2 x2, zuu, x1); ExtensionConstants.MultiplyImaginary(out Fp2 xd, tv2, minus_A.C1);
        Fp2.Select(out xd, ZxA, xd, Fp2.IsZeroMask(xd));
        Fp2.Square(out tv2, xd); Fp2.Multiply(out Fp2 gxd, xd, tv2);
        ExtensionConstants.MultiplyImaginary(out tv2, tv2, Aprime_E2.C1); Fp2.Square(out Fp2 gx1, x1);
        Fp2.Add(out gx1, gx1, tv2); Fp2Fused.MultiplySum(out gx1, gx1, x1, Bprime_E2, gxd);
        Fp2.Square(out Fp2 tv4, gxd); Fp2.Multiply(out tv2, gx1, gxd);
        Fp2.Multiply(out tv4, tv4, tv2); ulong square = SecretRoots.ReciprocalSqrt(out Fp2 y1, tv4);
        Fp2.Multiply(out y1, y1, tv2);
        Fp2.Select(out Fp2 x, x1, x2, square);
        Fp2.Multiply(out Fp2 alternativeY, y1, uu); Fp2.Multiply(out alternativeY, alternativeY, u);
        Fp2.Select(out Fp2 y, y1, alternativeY, square);
        Fp2.ConditionalNegate(out y, y, Fp2.SignMask(u) ^ Fp2.SignMask(y));
        Fp2.Multiply(out x, x, xd); Fp2.Multiply(out y, y, gxd);
        Jacobian<Fp2> p = new(x, y, xd);
        Isogeny<Fp2>.Apply(out p, p, x_num, x_den, y_num, y_den);
        Jacobian<Fp2>.Double(out Jacobian<Fp2> result, p);
        G2Projective.Psi(out result, result); G2Projective.Psi(out result, result);
        Jacobian<Fp2>.Negate(out Jacobian<Fp2> t0, p); G2Projective.Psi(out Jacobian<Fp2> t1, t0);
        Jacobian<Fp2>.AddConstantTime(out result, result, t0); Jacobian<Fp2>.AddConstantTime(out result, result, t1);
        Jacobian<Fp2>.TimesMinusZConstantTime(out t0, p); Jacobian<Fp2>.AddConstantTime(out t0, t0, p);
        Jacobian<Fp2>.AddConstantTime(out t0, t0, t1); Jacobian<Fp2>.TimesMinusZConstantTime(out t1, t0);
        Jacobian<Fp2>.AddConstantTime(out result, result, t1);
        r = new G2Projective(result);
    }
}
