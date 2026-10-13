using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // Each 31-step binary-GCD batch uses common top bits for comparisons and
    // exact bottom bits for parity. Signed update matrices then act on all limbs.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InverseBatched(out Fp result, in Fp value)
    {
        if (value.IsZero) { result = Zero; return; }
        Span<ulong> a = stackalloc ulong[6];
        Span<ulong> b = stackalloc ulong[6];
        Span<ulong> nextA = stackalloc ulong[6];
        Span<ulong> nextB = stackalloc ulong[6];
        value.Store(a);
        Modulus.CopyTo(b);
        // u*value = a*R^2 and v*value = b*R^2 (mod p). Thus when b=1,
        // v is already the Montgomery representation of the field inverse.
        Fp u = R2, v = Zero;
        while (!LimbMath.IsZero(a))
        {
            int top = 5;
            while ((a[top] | b[top]) == 0) top--;
            int bits = top * 64 + 64 - BitOperations.LeadingZeroCount(a[top] | b[top]);
            int steps = Math.Min(31, bits);
            ulong aa, bb;
            if (bits <= 62) { aa = a[0]; bb = b[0]; }
            else
            {
                int shift = bits - 31;
                aa = (ExtractTop(a, shift) << 31) | (a[0] & 0x7fffffff);
                bb = (ExtractTop(b, shift) << 31) | (b[0] & 0x7fffffff);
            }
            long f0 = 1, g0 = 0, f1 = 0, g1 = 1;
            for (int i = 0; i < steps; i++)
            {
                if ((aa & 1) != 0)
                {
                    if (aa < bb)
                    {
                        (aa, bb) = (bb, aa);
                        (f0, f1) = (f1, f0);
                        (g0, g1) = (g1, g0);
                    }
                    aa -= bb;
                    f0 -= f1; g0 -= g1;
                }
                aa >>= 1;
                f1 <<= 1; g1 <<= 1;
            }
            ApplyGcdMatrix(nextA, a, b, ref f0, ref g0, steps);
            ApplyGcdMatrix(nextB, a, b, ref f1, ref g1, steps);
            ApplyCoefficientMatrix(out Fp nextU, u, v, f0, g0, steps);
            ApplyCoefficientMatrix(out v, u, v, f1, g1, steps);
            u = nextU;
            nextA.CopyTo(a); nextB.CopyTo(b);
        }
        result = v;
    }

    private static ulong ExtractTop(ReadOnlySpan<ulong> a, int shift)
    {
        int word = shift >> 6, bit = shift & 63;
        ulong x = a[word] >> bit;
        if (bit != 0 && word < 5) x |= a[word + 1] << (64 - bit);
        return x;
    }

    private static void ApplyGcdMatrix(Span<ulong> result, ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b, ref long f, ref long g, int shift)
    {
        Span<ulong> words = stackalloc ulong[7];
        long carry = 0;
        for (int i = 0; i < 6; i++)
        {
            carry = SignedProductSum(out words[i], f, a[i], g, b[i], carry);
        }
        words[6] = (ulong)carry;
        for (int i = 0; i < 6; i++) result[i] = (words[i] >> shift) | (words[i + 1] << (64 - shift));
        if (carry < 0)
        {
            f = -f; g = -g;
            ulong c = 1;
            for (int i = 0; i < 6; i++) result[i] = AddCarry(~result[i], 0, ref c);
        }
    }

    private static void ApplyCoefficientMatrix(out Fp result, in Fp u, in Fp v, long f, long g, int shift)
    {
        Span<ulong> words = stackalloc ulong[7];
        ref ulong ur = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in u));
        ref ulong vr = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in v));
        ulong mask = (1UL << shift) - 1;
        // N0 is -1/p modulo 2^64. Adding q*p makes the numerator divisible by 2^shift.
        ulong q = ((ulong)f * u._l0 + (ulong)g * v._l0) * N0 & mask;
        long carry = 0;
        for (int i = 0; i < 6; i++)
        {
            carry = SignedProductSum(out ulong low, f, Unsafe.Add(ref ur, i), g, Unsafe.Add(ref vr, i), carry);
            carry += (long)MulAdd(q, Modulus[i], low, 0, out words[i]);
        }
        words[6] = (ulong)carry;
        ulong r0 = (words[0] >> shift) | (words[1] << (64 - shift));
        ulong r1 = (words[1] >> shift) | (words[2] << (64 - shift));
        ulong r2 = (words[2] >> shift) | (words[3] << (64 - shift));
        ulong r3 = (words[3] >> shift) | (words[4] << (64 - shift));
        ulong r4 = (words[4] >> shift) | (words[5] << (64 - shift));
        ulong r5 = (words[5] >> shift) | (words[6] << (64 - shift));
        // The row's absolute coefficient sum is at most 2^shift. After
        // division, adding q*p leaves a value in (-p, 2p), so one correction suffices.
        if (carry < 0)
        {
            ulong c = 0;
            r0 = AddCarry(r0, M0, ref c); r1 = AddCarry(r1, M1, ref c);
            r2 = AddCarry(r2, M2, ref c); r3 = AddCarry(r3, M3, ref c);
            r4 = AddCarry(r4, M4, ref c); r5 = AddCarry(r5, M5, ref c);
        }
        Reduce(out result, r0, r1, r2, r3, r4, r5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SignedProductSum(out ulong low, long f, ulong a, long g, ulong b, long carry)
    {
        // Interpret signed row multipliers through unsigned MULX and repair the high words.
        ulong high = MulAdd((ulong)f, a, (ulong)carry, 0, out low);
        high -= a & (ulong)(f >> 63);
        high += (ulong)(carry >> 63);
        high += MulAdd((ulong)g, b, low, 0, out low) - (b & (ulong)(g >> 63));
        return (long)high;
    }
}
