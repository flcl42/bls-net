using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

public partial struct G2Projective
{
    /// <summary>Secret subgroup point and 256-bit little-endian scalar multiplication. Reduces modulo r; length is public.</summary>
    public static void MultiplyConstantTime(in G2Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G2Projective r)
    {
        Jacobian<Fp2>.FromAffineConstantTime(out Jacobian<Fp2> basis, p.X, p.Y);
        SecretMultiply<Fp2>.Multiply(basis, scalarLittleEndian32, out r.Value);
    }
    /// <summary>Secret subgroup point and 256-bit little-endian scalar multiplication. Permits aliases; length is public.</summary>
    public static void MultiplyConstantTime(in G2Projective p, ReadOnlySpan<byte> scalarLittleEndian32, out G2Projective r)
        => SecretMultiply<Fp2>.Multiply(p.Value, scalarLittleEndian32, out r.Value);
    /// <summary>Converts a secret point with a fixed inversion; infinity maps to (0,0).</summary>
    public readonly G2Affine ToAffineConstantTime()
    { Value.ToAffineConstantTime(out Fp2 x, out Fp2 y); return new(x, y); }
    /// <summary>Converts a secret point with a fixed inversion; infinity maps to (0,0).</summary>
    public readonly void ToAffineConstantTime(out G2Affine r) => r = ToAffineConstantTime();
    /// <summary>All ones for infinity, zero otherwise. The point is secret.</summary>
    public static ulong IsInfinityMask(in G2Projective p) => Jacobian<Fp2>.InfinityMask(p.Value);
    /// <summary>All ones for equal points, zero otherwise. Both points are secret.</summary>
    public static ulong EqualMask(in G2Projective p, in G2Projective q) => Jacobian<Fp2>.EqualMask(p.Value, q.Value);
    /// <summary>Selects a for an all-ones mask, b for zero. All inputs may be secret; permits aliases.</summary>
    public static void Select(out G2Projective r, in G2Projective a, in G2Projective b, ulong mask)
        => Jacobian<Fp2>.Select(out r.Value, a.Value, b.Value, mask);
    /// <summary>Negates for an all-ones mask. All inputs may be secret; permits aliases.</summary>
    public static void ConditionalNegate(out G2Projective r, in G2Projective p, ulong mask)
        => Jacobian<Fp2>.ConditionalNegate(out r.Value, p.Value, mask);
    /// <summary>Negates a secret point; permits aliases.</summary>
    public static void NegateConstantTime(in G2Projective p, out G2Projective r)
        => Jacobian<Fp2>.Negate(out r.Value, p.Value);
    /// <summary>Swaps for an all-ones mask. All inputs may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref G2Projective a, ref G2Projective b, ulong mask)
    { G2Projective x = a, y = b; Select(out a, y, x, mask); Select(out b, x, y, mask); }
    /// <summary>Adds secret points, including doubling, inverse points and infinity; permits aliases.</summary>
    public static void AddConstantTime(in G2Projective p, in G2Projective q, out G2Projective r)
        => Jacobian<Fp2>.AddConstantTime(out r.Value, p.Value, q.Value);
    /// <summary>Mixed addition of secret points, including doubling and infinity; permits aliases.</summary>
    public static void AddAffineConstantTime(in G2Projective p, in G2Affine q, out G2Projective r)
        => Jacobian<Fp2>.AddAffineConstantTime(out r.Value, p.Value, q.X, q.Y);
}

public partial struct G2Affine
{
    /// <summary>All ones for affine infinity, zero otherwise. The point is secret.</summary>
    public static ulong IsInfinityMask(in G2Affine p) => Fp2.IsZeroMask(p.X) & Fp2.IsZeroMask(p.Y);
    /// <summary>All ones for equal affine points, zero otherwise. Both points are secret.</summary>
    public static ulong EqualMask(in G2Affine p, in G2Affine q) => Fp2.EqualMask(p.X, q.X) & Fp2.EqualMask(p.Y, q.Y);
    /// <summary>Selects a for an all-ones mask, b for zero. All inputs may be secret; permits aliases.</summary>
    public static void Select(out G2Affine r, in G2Affine a, in G2Affine b, ulong mask)
    { Fp2.Select(out Fp2 x, a.X, b.X, mask); Fp2.Select(out Fp2 y, a.Y, b.Y, mask); r = new(x, y); }
    /// <summary>Negates for an all-ones mask. All inputs may be secret; permits aliases.</summary>
    public static void ConditionalNegate(out G2Affine r, in G2Affine p, ulong mask)
    { Fp2.ConditionalNegate(out Fp2 y, p.Y, mask); r = new(p.X, y); }
    /// <summary>Swaps for an all-ones mask. All inputs may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref G2Affine a, ref G2Affine b, ulong mask)
    { G2Affine x = a, y = b; Select(out a, y, x, mask); Select(out b, x, y, mask); }
}
