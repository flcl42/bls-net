using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class ConstantTime
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ZeroMask(ulong value) => ((value | (0UL - value)) >> 63) - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Select(ulong set, ulong clear, ulong mask) => clear ^ ((clear ^ set) & mask);

    // Borrow idioms deliberately avoid UInt128 comparisons (which can lower to branches).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Subtract(ulong a, ulong b, ref ulong borrow)
    {
        ulong difference = a - b;
        ulong b0 = difference > a ? 1UL : 0;
        ulong result = difference - borrow;
        borrow = b0 + (result > difference ? 1UL : 0);
        return result;
    }
}
