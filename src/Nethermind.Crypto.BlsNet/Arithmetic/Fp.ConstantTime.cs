using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    /// <summary>Square root of a secret value; returns all ones for a square, zero otherwise. Permits aliases.</summary>
    public static ulong SqrtConstantTime(out Fp result, in Fp value)
    {
        Fp input = value;
        Hashing.FpCurveRoots.ReciprocalSqrt(out Fp inverseRoot, input);
        MultiplyComposite(out result, inverseRoot, input);
        MultiplyComposite(out Fp check, result, result); return EqualMask(check, input);
    }
    /// <summary>RFC 9380 sign as an all-ones/zero mask. The value is secret.</summary>
    public static ulong SignMask(in Fp value)
    { MultiplyComposite(out Fp raw, value, RawOne); return 0UL - (raw._l0 & 1); }

    /// <summary>All ones for zero, otherwise zero. The value is secret.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong IsZeroMask(in Fp value) => ConstantTime.ZeroMask(value._l0 | value._l1 | value._l2 | value._l3 | value._l4 | value._l5);

    /// <summary>All ones for equal values, otherwise zero. Both values are secret.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong EqualMask(in Fp left, in Fp right) => ConstantTime.ZeroMask((left._l0 ^ right._l0) | (left._l1 ^ right._l1) | (left._l2 ^ right._l2) | (left._l3 ^ right._l3) | (left._l4 ^ right._l4) | (left._l5 ^ right._l5));

    /// <summary>Selects whenSet for an all-ones mask, whenClear for zero. All inputs may be secret; permits aliases.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Select(out Fp result, in Fp whenSet, in Fp whenClear, ulong mask)
        => result = new(Select(whenSet._l0, whenClear._l0, mask),
            Select(whenSet._l1, whenClear._l1, mask),
            Select(whenSet._l2, whenClear._l2, mask),
            Select(whenSet._l3, whenClear._l3, mask),
            Select(whenSet._l4, whenClear._l4, mask),
            Select(whenSet._l5, whenClear._l5, mask));

    /// <summary>Negates for an all-ones mask; leaves unchanged for zero. All inputs may be secret.</summary>
    public static void ConditionalNegate(out Fp result, in Fp value, ulong mask)
    { Negate(out Fp negative, value); Select(out result, negative, value, mask); }

    /// <summary>Swaps for an all-ones mask. Both values and mask may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref Fp left, ref Fp right, ulong mask)
    {
        Fp a = left, b = right;
        Select(out left, b, a, mask); Select(out right, a, b, mask);
    }
}
