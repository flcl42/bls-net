using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fr
{
    /// <summary>All ones for zero, otherwise zero. The value is secret.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong IsZeroMask(in Fr value) => ConstantTime.ZeroMask(value._l0 | value._l1 | value._l2 | value._l3);

    /// <summary>All ones for equal values, otherwise zero. Both values are secret.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong EqualMask(in Fr left, in Fr right) => ConstantTime.ZeroMask((left._l0 ^ right._l0) | (left._l1 ^ right._l1) | (left._l2 ^ right._l2) | (left._l3 ^ right._l3));

    /// <summary>Selects whenSet for an all-ones mask, whenClear for zero. All inputs may be secret; permits aliases.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Select(out Fr result, in Fr whenSet, in Fr whenClear, ulong mask)
        => result = new(Select(whenSet._l0, whenClear._l0, mask),
            Select(whenSet._l1, whenClear._l1, mask),
            Select(whenSet._l2, whenClear._l2, mask),
            Select(whenSet._l3, whenClear._l3, mask));

    /// <summary>Negates for an all-ones mask; leaves unchanged for zero. All inputs may be secret.</summary>
    public static void ConditionalNegate(out Fr result, in Fr value, ulong mask)
    { Negate(out Fr negative, value); Select(out result, negative, value, mask); }

    /// <summary>Swaps for an all-ones mask. Both values and mask may be secret; permits identical references.</summary>
    public static void ConditionalSwap(ref Fr left, ref Fr right, ulong mask)
    {
        Fr a = left, b = right;
        Select(out left, b, a, mask); Select(out right, a, b, mask);
    }
}
