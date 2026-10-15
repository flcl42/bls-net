namespace Nethermind.Crypto.BlsNet.Arithmetic;

public partial struct Fp2
{
    /// <summary>Square root of a secret value; returns all ones for a square, zero otherwise. Permits aliases.</summary>
    public static ulong SqrtConstantTime(out Fp2 result, in Fp2 value) => Hashing.SecretRoots.Sqrt(out result, value);
    /// <summary>All ones for zero, otherwise zero. Both coefficients are secret.</summary>
    public static ulong IsZeroMask(in Fp2 value) => Fp.IsZeroMask(value.C0) & Fp.IsZeroMask(value.C1);
    /// <summary>All ones for equality, otherwise zero. Both values are secret.</summary>
    public static ulong EqualMask(in Fp2 a, in Fp2 b) => Fp.EqualMask(a.C0, b.C0) & Fp.EqualMask(a.C1, b.C1);
    /// <summary>Selects whenSet for an all-ones mask, whenClear for zero. All inputs may be secret; permits aliases.</summary>
    public static void Select(out Fp2 result, in Fp2 whenSet, in Fp2 whenClear, ulong mask)
    {
        Fp.Select(out Fp c0, whenSet.C0, whenClear.C0, mask);
        Fp.Select(out Fp c1, whenSet.C1, whenClear.C1, mask);
        result = new(c0, c1);
    }
    /// <summary>Negates for an all-ones mask. Value and mask may be secret; permits aliases.</summary>
    public static void ConditionalNegate(out Fp2 result, in Fp2 value, ulong mask)
    { Negate(out Fp2 negative, value); Select(out result, negative, value, mask); }
    /// <summary>Swaps for an all-ones mask. All inputs may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref Fp2 left, ref Fp2 right, ulong mask)
    { Fp2 a = left, b = right; Select(out left, b, a, mask); Select(out right, a, b, mask); }
    /// <summary>Secret field inverse using the norm and fixed Fp binary GCD. Zero maps to zero; permits aliases.</summary>
    public static void InverseConstantTime(out Fp2 result, in Fp2 value)
    {
        Fp.MultiplyComposite(out Fp norm, value.C0, value.C0);
        Fp.MultiplyComposite(out Fp t, value.C1, value.C1); Fp.Add(out norm, norm, t);
        Fp.InverseConstantTime(out norm, norm);
        Fp.MultiplyComposite(out Fp x, value.C0, norm); Fp.MultiplyComposite(out Fp y, value.C1, norm);
        Fp.Negate(out y, y); result = new(x, y);
    }
    /// <summary>RFC 9380 sign as an all-ones/zero mask; both coefficients are secret.</summary>
    public static ulong SignMask(in Fp2 value) => Fp.SignMask(value.C0) | (Fp.IsZeroMask(value.C0) & Fp.SignMask(value.C1));
}
