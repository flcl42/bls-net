using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

public partial struct G1Projective
{
    /// <summary>Secret subgroup point and 256-bit little-endian scalar multiplication. Reduces modulo r; length is public.</summary>
    public static void MultiplyConstantTime(in G1Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G1Projective r)
    {
        Jacobian<Fp>.FromAffineConstantTime(out Jacobian<Fp> basis, p.X, p.Y);
        SecretMultiply<Fp>.Multiply(basis, scalarLittleEndian32, out r.Value);
    }
    /// <summary>Secret subgroup point and 256-bit little-endian scalar multiplication. Permits aliases; length is public.</summary>
    public static void MultiplyConstantTime(in G1Projective p, ReadOnlySpan<byte> scalarLittleEndian32, out G1Projective r)
        => SecretMultiply<Fp>.Multiply(p.Value, scalarLittleEndian32, out r.Value);
    /// <summary>Converts a secret point with a fixed inversion; infinity maps to (0,0).</summary>
    public readonly G1Affine ToAffineConstantTime()
    { Value.ToAffineConstantTime(out Fp x, out Fp y); return new(x, y); }
    /// <summary>Converts a secret point with a fixed inversion; infinity maps to (0,0).</summary>
    public readonly void ToAffineConstantTime(out G1Affine r) => r = ToAffineConstantTime();
    /// <summary>All ones for infinity, zero otherwise. The point is secret.</summary>
    public static ulong IsInfinityMask(in G1Projective p) => Jacobian<Fp>.InfinityMask(p.Value);
    /// <summary>All ones for equal points, zero otherwise. Both points are secret.</summary>
    public static ulong EqualMask(in G1Projective p, in G1Projective q) => Jacobian<Fp>.EqualMask(p.Value, q.Value);
    /// <summary>Selects a for an all-ones mask, b for zero. All inputs may be secret; permits aliases.</summary>
    public static void Select(out G1Projective r, in G1Projective a, in G1Projective b, ulong mask)
        => Jacobian<Fp>.Select(out r.Value, a.Value, b.Value, mask);
    /// <summary>Negates for an all-ones mask. All inputs may be secret; permits aliases.</summary>
    public static void ConditionalNegate(out G1Projective r, in G1Projective p, ulong mask)
        => Jacobian<Fp>.ConditionalNegate(out r.Value, p.Value, mask);
    /// <summary>Negates a secret point; permits aliases.</summary>
    public static void NegateConstantTime(in G1Projective p, out G1Projective r)
        => Jacobian<Fp>.Negate(out r.Value, p.Value);
    /// <summary>Swaps for an all-ones mask. All inputs may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref G1Projective a, ref G1Projective b, ulong mask)
    { G1Projective x = a, y = b; Select(out a, y, x, mask); Select(out b, x, y, mask); }
    /// <summary>Adds secret points, including doubling, inverse points and infinity; permits aliases.</summary>
    public static void AddConstantTime(in G1Projective p, in G1Projective q, out G1Projective r)
        => Jacobian<Fp>.AddConstantTime(out r.Value, p.Value, q.Value);
    /// <summary>Mixed addition of secret points, including doubling and infinity; permits aliases.</summary>
    public static void AddAffineConstantTime(in G1Projective p, in G1Affine q, out G1Projective r)
        => Jacobian<Fp>.AddAffineConstantTime(out r.Value, p.Value, q.X, q.Y);
}

public partial struct G1Affine
{
    /// <summary>All ones for affine infinity, zero otherwise. The point is secret.</summary>
    public static ulong IsInfinityMask(in G1Affine p) => Fp.IsZeroMask(p.X) & Fp.IsZeroMask(p.Y);
    /// <summary>All ones for equal affine points, zero otherwise. Both points are secret.</summary>
    public static ulong EqualMask(in G1Affine p, in G1Affine q) => Fp.EqualMask(p.X, q.X) & Fp.EqualMask(p.Y, q.Y);
    /// <summary>Selects a for an all-ones mask, b for zero. All inputs may be secret; permits aliases.</summary>
    public static void Select(out G1Affine r, in G1Affine a, in G1Affine b, ulong mask)
    { Fp.Select(out Fp x, a.X, b.X, mask); Fp.Select(out Fp y, a.Y, b.Y, mask); r = new(x, y); }
    /// <summary>Negates for an all-ones mask. All inputs may be secret; permits aliases.</summary>
    public static void ConditionalNegate(out G1Affine r, in G1Affine p, ulong mask)
    { Fp.ConditionalNegate(out Fp y, p.Y, mask); r = new(p.X, y); }
    /// <summary>Swaps for an all-ones mask. All inputs may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref G1Affine a, ref G1Affine b, ulong mask)
    { G1Affine x = a, y = b; Select(out a, y, x, mask); Select(out b, x, y, mask); }
}
