// Ported from blst ct_inverse_mod_256-x86_64.pl and no_asm.h,
// copyright Supranational LLC, Apache-2.0. See docs/design.md.
using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class InverseBatch
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ZeroMask(ulong x) => System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported
        ? 0UL - (x == 0 ? 1UL : 0UL)
        : ((x | (0UL - x)) >> 63) - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Select(ulong set, ulong clear, ulong mask) => clear ^ ((set ^ clear) & mask);

    // Pornin's two biased 32-bit coefficients packed into each 64-bit row.
    // The absolute row sum is <= 2^31, so neither biased half overflows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Steps31(ulong a, ulong b, out long f0, out long g0, out long f1, out long g1)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            Steps31Arm64(a, b, out f0, out g0, out f1, out g1);
            return;
        }
        const ulong bias = 0x7fffffff7fffffff;
        ulong row0 = 0x7fffffff80000000, row1 = 0x800000007fffffff;
        for (int i = 0; i < 31; i++)
        {
            ulong oldA = a, oldB = b, old0 = row0, old1 = row1;
            ulong swap = 0UL - (a < b ? 1UL : 0UL);
            ulong aa = Select(b, a, swap), bb = Select(a, b, swap);
            ulong next0 = Select(row1, row0, swap), next1 = Select(row0, row1, swap);
            ulong difference = aa - bb, nextRow = next0 - next1 + bias;
            ulong odd = 0UL - (oldA & 1);
            ulong finalA = Select(difference, oldA, odd);
            ulong finalB = Select(bb, oldB, odd);
            ulong final0 = Select(nextRow, old0, odd);
            ulong final1 = Select(next1, old1, odd);
            a = finalA >> 1; b = finalB; row0 = final0; row1 = 2 * final1 - bias;
        }
        f0 = (long)(uint)row0 - 0x7fffffff;
        g0 = (long)(row0 >> 32) - 0x7fffffff;
        f1 = (long)(uint)row1 - 0x7fffffff;
        g1 = (long)(row1 >> 32) - 0x7fffffff;
    }

    // Gate the subtraction by parity before deciding to swap. An even a
    // cannot borrow, so its state and coefficient row need no rollback selects.
    // This retains the existing 31-bit schedule and packed-coefficient bounds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Steps31Arm64(ulong a, ulong b, out long f0, out long g0, out long f1, out long g1)
    {
        const ulong bias = 0x7fffffff7fffffff;
        ulong row0 = 0x7fffffff80000000, row1 = 0x800000007fffffff;
        for (int i = 0; i < 31; i++)
        {
            ulong odd = 0UL - (a & 1);
            ulong difference = a - (b & odd);
            ulong swap = 0UL - (difference > a ? 1UL : 0UL);
            ulong nextA = Select(b - a, difference, swap);
            ulong nextB = Select(a, b, swap);
            // Recover the other swapped row from their invariant sum. This
            // leaves three ordinary selects sharing the borrow flags instead
            // of mixing a CSE-shared XOR swap with two ordinary selects.
            ulong sum = row0 + row1;
            ulong next0 = Select(row1, row0, swap), next1 = sum - next0;
            row0 = next0 - (next1 & odd) + (bias & odd);
            row1 = 2 * next1 - bias;
            a = nextA >> 1;
            b = nextB;
        }
        f0 = (long)(uint)row0 - 0x7fffffff;
        g0 = (long)(row0 >> 32) - 0x7fffffff;
        f1 = (long)(uint)row1 - 0x7fffffff;
        g1 = (long)(row1 >> 32) - 0x7fffffff;
    }

    // After the last approximated batch, a,b fit in one word. The remaining
    // public count is 55 (Fp) or 47 (Fr); its rows fit signed 64-bit words.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Finish(ulong a, ulong b, int count, out long f, out long g)
    {
        ulong f0 = 1, g0 = 0, f1 = 0, g1 = 1;
        for (int i = 0; i < count; i++)
        {
            ulong odd = 0UL - (a & 1);
            ulong swap = 0UL - ((a < b ? 1UL : 0UL) & a & 1);
            ulong x = (a ^ b) & swap; a ^= x; b ^= x;
            x = (f0 ^ f1) & swap; f0 ^= x; f1 ^= x;
            x = (g0 ^ g1) & swap; g0 ^= x; g1 ^= x;
            a = (a - (b & odd)) >> 1;
            f0 -= f1 & odd; g0 -= g1 & odd;
            f1 <<= 1; g1 <<= 1;
        }
        f = (long)f1; g = (long)g1;
    }
}
