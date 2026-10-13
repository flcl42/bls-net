using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>A G2 point in Jacobian coordinates. Arithmetic is variable-time.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct G2Projective
{
    internal Jacobian<Fp2> Value;
    public Fp2 X { readonly get => Value.X; set => Value.X = value; }
    public Fp2 Y { readonly get => Value.Y; set => Value.Y = value; }
    public Fp2 Z { readonly get => Value.Z; set => Value.Z = value; }
    public readonly bool IsInfinity => Value.IsInfinity;
    public static G2Projective Generator => FromAffine(CurveConstants.Generator2);
    public G2Projective(Fp2 x, Fp2 y, Fp2 z) => Value = new(x, y, z);
    internal G2Projective(Jacobian<Fp2> value) => Value = value;
    /// <summary>Creates a projective point from an affine point.</summary>
    public static G2Projective FromAffine(in G2Affine p)
    { Jacobian<Fp2>.FromAffine(out Jacobian<Fp2> r, p.X, p.Y); return new(r); }
    public static void FromAffine(in G2Affine p, out G2Projective r) => r = FromAffine(p);
    /// <summary>Converts with one field inversion.</summary>
    public readonly G2Affine ToAffine()
    { Value.ToAffine(out Fp2 x, out Fp2 y); return new(x, y); }
    public readonly void ToAffine(out G2Affine r) => r = ToAffine();
    /// <summary>Adds points, including infinity and doubling; variable-time.</summary>
    public static void Add(in G2Projective p, in G2Projective q, out G2Projective r)
        => Jacobian<Fp2>.Add(out r.Value, p.Value, q.Value);
    /// <summary>Mixed addition, including infinity and doubling; variable-time.</summary>
    public static void AddAffine(in G2Projective p, in G2Affine q, out G2Projective r)
        => Jacobian<Fp2>.AddAffine(out r.Value, p.Value, q.X, q.Y);
    /// <summary>Doubles a point; variable-time.</summary>
    public static void Double(in G2Projective p, out G2Projective r)
        => Jacobian<Fp2>.Double(out r.Value, p.Value);
    /// <summary>Negates a point.</summary>
    public static void Negate(in G2Projective p, out G2Projective r)
        => Jacobian<Fp2>.Negate(out r.Value, p.Value);
    /// <summary>Compares without inversion; variable-time.</summary>
    public static bool AreEqual(in G2Projective p, in G2Projective q)
        => Jacobian<Fp2>.AreEqual(p.Value, q.Value);
    /// <summary>Multiplies a subgroup point by any 256-bit little-endian scalar; variable-time.</summary>
    public static void Multiply(in G2Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G2Projective r)
        => ScalarMultiplication.Multiply(p, scalarLittleEndian32, out r);
    /// <summary>MSM with consecutive 32-byte little-endian scalars; variable-time. Points must be in G2.</summary>
    public static void MultiScalarMultiply(ReadOnlySpan<G2Affine> points, ReadOnlySpan<byte> scalars, out G2Projective r)
        => MultiScalarMultiplication.MultiScalarMultiply(points, scalars, out r);
    internal static void Psi(out Jacobian<Fp2> r, in Jacobian<Fp2> p)
    {
        Fp.MultiplyComposite(out Fp x0, p.X.C1, CurveConstants.frobenius_x.C1);
        Fp.MultiplyComposite(out Fp x1, p.X.C0, CurveConstants.frobenius_x.C1);
        Fp2 x = new(x0, x1);
        Fp2.Conjugate(out Fp2 y, p.Y); ExtensionConstants.MultiplyConjugatePair(out y, y, CurveConstants.frobenius_y.C0);
        Fp2.Conjugate(out Fp2 z, p.Z); r = new(x, y, z);
    }
}

public partial struct G2Affine
{
    public static G2Affine Generator => CurveConstants.Generator2;
    /// <summary>Tests the curve equation; infinity is on the curve. Variable-time.</summary>
    public readonly bool IsOnCurve()
    {
        if (IsInfinity) return true;
        Fp2.Square(out Fp2 lhs, Y); Fp2.Square(out Fp2 rhs, X); Fp2.Multiply(out rhs, rhs, X);
        Fp2.MulBy4(out Fp2 b, new Fp2(Fp.One, Fp.One)); Fp2.Add(out rhs, rhs, b);
        return Fp2.AreEqual(lhs, rhs);
    }
    /// <summary>Scott's subgroup test; infinity is in the subgroup. Variable-time. Assumes an on-curve point.</summary>
    public readonly bool IsInSubgroup()
    {
        if (IsInfinity) return true;
        Jacobian<Fp2>.FromAffine(out Jacobian<Fp2> p, X, Y);
        Jacobian<Fp2>.TimesMinusZ(out Jacobian<Fp2> t, p);
        Jacobian<Fp2>.Negate(out t, t); G2Projective.Psi(out p, p);
        return Jacobian<Fp2>.AreEqual(p, t);
    }
}
