// Ported from blst ct_inverse_mod_256-x86_64.pl and no_asm.h,
// copyright Supranational LLC, Apache-2.0. See docs/design.md.
using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class InverseBatch
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ZeroMask(ulong x) => ((x | (0UL - x)) >> 63) - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Select(ulong set, ulong clear, ulong mask) => clear ^ ((set ^ clear) & mask);

    // Pornin's two biased 32-bit coefficients packed into each 64-bit row.
    // The absolute row sum is <= 2^31, so neither biased half overflows.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Steps31(ulong a, ulong b, out long f0, out long g0, out long f1, out long g1)
    {
        const ulong bias = 0x7fffffff7fffffff;
        ulong row0 = 0x7fffffff80000000, row1 = 0x800000007fffffff;
        for (int i = 0; i < 31; i++)
        {
            ulong odd = 0UL - (a & 1);
            // Keep the comparison as a 0/1 carry idiom: selecting -1 directly
            // causes stock RyuJIT to emit a secret-conditioned branch.
            ulong swap = (0UL - (a < b ? 1UL : 0UL)) & odd;
            ulong x = (a ^ b) & swap; a ^= x; b ^= x;
            x = (row0 ^ row1) & swap; row0 ^= x; row1 ^= x;
            a = (a - (b & odd)) >> 1;
            row0 = row0 - (row1 & odd) + (bias & odd);
            row1 = 2 * row1 - bias;
        }
        f0 = (long)(uint)row0 - 0x7fffffff;
        g0 = (long)(row0 >> 32) - 0x7fffffff;
        f1 = (long)(uint)row1 - 0x7fffffff;
        g1 = (long)(row1 >> 32) - 0x7fffffff;
    }

    // After the last approximated batch, a,b fit in one word. The remaining
    // public count is 24 (Fp) or 47 (Fr); its rows fit signed 64-bit words.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Finish(ulong a, ulong b, int count, out long f, out long g)
    {
        ulong f0 = 1, g0 = 0, f1 = 0, g1 = 1;
        for (int i = 0; i < count; i++)
        {
            ulong odd = 0UL - (a & 1);
            ulong swap = (0UL - (a < b ? 1UL : 0UL)) & odd;
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
