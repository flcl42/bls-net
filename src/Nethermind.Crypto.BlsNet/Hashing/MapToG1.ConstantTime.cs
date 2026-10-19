// Secret-input SSWU ported from blst map_to_g1.c (Apache-2.0).
// Copyright Supranational LLC. Adapted for this C# rewrite; see NOTICE and LICENSE.
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using static Nethermind.Crypto.BlsNet.Hashing.MapConstants1;

namespace Nethermind.Crypto.BlsNet.Hashing;

public static partial class MapToG1
{
    internal static void MapConstantTime(in Fp u, out G1Projective r)
    {
        CurveField<Fp>.Square(out Fp uu, u); CurveField<Fp>.Multiply(out Fp zuu, Z, uu);
        CurveField<Fp>.Square(out Fp tv2, zuu); CurveField<Fp>.Add(out tv2, tv2, zuu);
        CurveField<Fp>.Add(out Fp x1, tv2, Fp.One); CurveField<Fp>.Multiply(out x1, x1, Bprime_E1);
        CurveField<Fp>.Multiply(out Fp x2, zuu, x1); CurveField<Fp>.Multiply(out Fp xd, minus_A, tv2);
        Fp.Select(out xd, ZxA, xd, Fp.IsZeroMask(xd));
        CurveField<Fp>.Square(out tv2, xd); CurveField<Fp>.Multiply(out Fp gxd, xd, tv2);
        CurveField<Fp>.Multiply(out tv2, tv2, Aprime_E1); CurveField<Fp>.Square(out Fp gx1, x1);
        CurveField<Fp>.Add(out gx1, gx1, tv2); CurveField<Fp>.Multiply(out gx1, gx1, x1);
        CurveField<Fp>.Multiply(out tv2, Bprime_E1, gxd); CurveField<Fp>.Add(out gx1, gx1, tv2);
        CurveField<Fp>.Square(out Fp tv4, gxd); CurveField<Fp>.Multiply(out tv2, gx1, gxd);
        CurveField<Fp>.Multiply(out tv4, tv4, tv2); ulong square = SecretRoots.ReciprocalSqrt(out Fp y1, tv4);
        CurveField<Fp>.Multiply(out y1, y1, tv2);
        Fp.Select(out Fp x, x1, x2, square);
        CurveField<Fp>.Multiply(out Fp alternativeY, y1, sqrt_minus_ZZZ);
        CurveField<Fp>.Multiply(out alternativeY, alternativeY, uu); CurveField<Fp>.Multiply(out alternativeY, alternativeY, u);
        Fp.Select(out Fp y, y1, alternativeY, square);
        Fp.ConditionalNegate(out y, y, Fp.SignMask(u) ^ Fp.SignMask(y));
        CurveField<Fp>.Multiply(out x, x, xd); CurveField<Fp>.Multiply(out y, y, gxd);
        Jacobian<Fp> p = new(x, y, xd);
        Isogeny<Fp>.Apply(out p, p, x_num, x_den, y_num, y_den);
        Jacobian<Fp>.TimesOneMinusZConstantTime(out Jacobian<Fp> result, p);
        r = new G1Projective(result);
    }
}
