using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>An affine point on E1: y^2 = x^3 + 4 over Fp. The point at infinity is X = Y = 0, as in blst.</summary>
public partial struct G1Affine
{
    public Fp X;
    public Fp Y;

    public G1Affine(Fp x, Fp y)
    {
        X = x;
        Y = y;
    }

    public readonly bool IsInfinity => Fp.EqualsZero(X) && Fp.EqualsZero(Y);
}

/// <summary>An affine point on E2: y^2 = x^3 + 4(1 + u) over Fp2. The point at infinity is X = Y = 0, as in blst.</summary>
public partial struct G2Affine
{
    public Fp2 X;
    public Fp2 Y;

    public G2Affine(Fp2 x, Fp2 y)
    {
        X = x;
        Y = y;
    }

    public readonly bool IsInfinity => Fp2.EqualsZero(X) && Fp2.EqualsZero(Y);
}
