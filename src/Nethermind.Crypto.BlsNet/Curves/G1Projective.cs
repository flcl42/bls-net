using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>A G1 point in Jacobian coordinates. See individual methods for timing contracts.</summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct G1Projective
{
    internal Jacobian<Fp> Value;
    public Fp X { readonly get => Value.X; set => Value.X = value; }
    public Fp Y { readonly get => Value.Y; set => Value.Y = value; }
    public Fp Z { readonly get => Value.Z; set => Value.Z = value; }
    public readonly bool IsInfinity => Value.IsInfinity;
    public static G1Projective Generator => FromAffine(CurveConstants.Generator1);
    public G1Projective(Fp x, Fp y, Fp z) => Value = new(x, y, z);
    internal G1Projective(Jacobian<Fp> value) => Value = value;
    /// <summary>Creates a projective point from an affine point.</summary>
    public static G1Projective FromAffine(in G1Affine p)
    { Jacobian<Fp>.FromAffine(out Jacobian<Fp> r, p.X, p.Y); return new(r); }
    public static void FromAffine(in G1Affine p, out G1Projective r) => r = FromAffine(p);
    /// <summary>Converts with one field inversion.</summary>
    public readonly G1Affine ToAffine()
    { Value.ToAffine(out Fp x, out Fp y); return new(x, y); }
    public readonly void ToAffine(out G1Affine r) => r = ToAffine();
    /// <summary>Adds points, including infinity and doubling; variable-time.</summary>
    public static void Add(in G1Projective p, in G1Projective q, out G1Projective r)
        => Jacobian<Fp>.Add(out r.Value, p.Value, q.Value);
    /// <summary>Mixed addition, including infinity and doubling; variable-time.</summary>
    public static void AddAffine(in G1Projective p, in G1Affine q, out G1Projective r)
        => Jacobian<Fp>.AddAffine(out r.Value, p.Value, q.X, q.Y);
    /// <summary>Doubles a point; variable-time.</summary>
    public static void Double(in G1Projective p, out G1Projective r)
        => Jacobian<Fp>.Double(out r.Value, p.Value);
    /// <summary>Negates a point.</summary>
    public static void Negate(in G1Projective p, out G1Projective r)
        => Jacobian<Fp>.Negate(out r.Value, p.Value);
    /// <summary>Compares without inversion; variable-time.</summary>
    public static bool AreEqual(in G1Projective p, in G1Projective q)
        => Jacobian<Fp>.AreEqual(p.Value, q.Value);
    /// <summary>Multiplies a subgroup point by any 256-bit little-endian scalar; variable-time.</summary>
    public static void Multiply(in G1Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G1Projective r)
        => ScalarMultiplication.Multiply(p, scalarLittleEndian32, out r);
    /// <summary>MSM with consecutive 32-byte little-endian scalars; variable-time. Points must be in G1.</summary>
    public static void MultiScalarMultiply(ReadOnlySpan<G1Affine> points, ReadOnlySpan<byte> scalars, out G1Projective r)
        => MultiScalarMultiplication.MultiScalarMultiply(points, scalars, out r);
}

public partial struct G1Affine
{
    public static G1Affine Generator => CurveConstants.Generator1;
    /// <summary>Tests the curve equation; infinity is on the curve. Variable-time.</summary>
    public readonly bool IsOnCurve()
    {
        if (IsInfinity) return true;
        Fp.Square(out Fp lhs, Y); Fp.Square(out Fp rhs, X); Fp.Multiply(out rhs, rhs, X);
        Fp.MulBy4(out Fp b, Fp.One); Fp.Add(out rhs, rhs, b);
        return Fp.AreEqual(lhs, rhs);
    }
    /// <summary>Scott's subgroup test; infinity is in the subgroup. Variable-time. Assumes an on-curve point.</summary>
    public readonly bool IsInSubgroup()
    {
        if (IsInfinity) return true;
        Jacobian<Fp>.FromAffine(out Jacobian<Fp> p, X, Y);
        Jacobian<Fp>.TimesMinusZ(out Jacobian<Fp> t, p);
        Jacobian<Fp>.TimesMinusZ(out t, t); Jacobian<Fp>.Negate(out t, t);
        Fp.Multiply(out p.Z, p.Z, CurveConstants.Beta); Fp.Multiply(out p.Z, p.Z, CurveConstants.Beta);
        return Jacobian<Fp>.AreEqual(p, t);
    }
}
