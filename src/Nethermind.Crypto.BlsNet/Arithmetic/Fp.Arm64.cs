// ARM64 Montgomery scheduling adapted from blst's mul_mont_384-armv8.pl,
// copyright Supranational LLC, Apache-2.0. See NOTICE.
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.InteropServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // 4p followed by 2p, for the signed |value| < 3pR correction.
    private static class Arm64WideCorrection
    {
        internal static readonly ulong[] Limbs =
        [
            0xe7fbfffffffeaaac, 0x7aaffffac54ffffe, 0x9cc34a83dac3d890,
            0x91dd2e13ce144afd, 0x2c6e9ed90d2eb35d, 0x680447a8e5ff9a69,
            0x73fdffffffff5556, 0x3d57fffd62a7ffff, 0xce61a541ed61ec48,
            0xc8ee9709e70a257e, 0x96374f6c869759ae, 0x340223d472ffcd34,
        ];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideCorrectArm64(out Fp result, in Fp quotient)
    {
        ref ulong c = ref MemoryMarshal.GetArrayDataReference(Arm64WideCorrection.Limbs);
        ulong mask = 0UL - (quotient._l5 >> 63), carry = 0;
        ulong h0 = AddCarry(quotient._l0, c & mask, ref carry);
        ulong h1 = AddCarry(quotient._l1, Unsafe.Add(ref c, 1) & mask, ref carry);
        ulong h2 = AddCarry(quotient._l2, Unsafe.Add(ref c, 2) & mask, ref carry);
        ulong h3 = AddCarry(quotient._l3, Unsafe.Add(ref c, 3) & mask, ref carry);
        ulong h4 = AddCarry(quotient._l4, Unsafe.Add(ref c, 4) & mask, ref carry);
        ulong h5 = AddCarry(quotient._l5, Unsafe.Add(ref c, 5) & mask, ref carry);
        ulong borrow = 0;
        ulong d0 = SubBorrow(h0, Unsafe.Add(ref c, 6), ref borrow);
        ulong d1 = SubBorrow(h1, Unsafe.Add(ref c, 7), ref borrow);
        ulong d2 = SubBorrow(h2, Unsafe.Add(ref c, 8), ref borrow);
        ulong d3 = SubBorrow(h3, Unsafe.Add(ref c, 9), ref borrow);
        ulong d4 = SubBorrow(h4, Unsafe.Add(ref c, 10), ref borrow);
        ulong d5 = SubBorrow(h5, Unsafe.Add(ref c, 11), ref borrow);
        mask = ChoiceToMask(borrow);
        ReduceArm64(out result, Select(h0, d0, mask), Select(h1, d1, mask), Select(h2, d2, mask),
            Select(h3, d3, mask), Select(h4, d4, mask), Select(h5, d5, mask));
    }

    // sa*sb - a0*b0 - a1*b1 is nonnegative and below 2p^2 < pR.
    // Keeping both subtractions local removes a wide scratch round trip.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideImaginaryArm64(out Fp result, in FpWide sum, in FpWide first, in FpWide second)
    {
        SubtractWideInline(out FpWide difference, sum, first);
        SubtractWideInline(out difference, difference, second);
        ReduceWideCoreArm64(out Fp quotient, difference);
        ReduceArm64(out result, quotient._l0, quotient._l1, quotient._l2, quotient._l3, quotient._l4, quotient._l5);
    }

    // Three distinct product buffers. Forming the sum in registers avoids
    // storing and reloading the intermediate imaginary difference. Arithmetic
    // is modulo 2^768, so this rearrangement preserves the signed-wide result.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractWideProductsArm64(ref FpWide first, in FpWide second, ref FpWide sumProduct)
    {
        AddWideInline(out FpWide sum, first, second);
        SubtractWideInline(out sumProduct, sumProduct, sum);
        SubtractWideInline(out first, first, second);
    }

    // Canonical inputs. The shift replaces a whole-vector doubling carry
    // chain: 3*t+2*a = 2*(t+a)+t, and 5p<R prevents overflow.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TripleAddDoubleArm64(out Fp result, in Fp t, in Fp a)
    {
        ulong carry = 0;
        ulong s0 = AddCarry(t._l0, a._l0, ref carry), s1 = AddCarry(t._l1, a._l1, ref carry);
        ulong s2 = AddCarry(t._l2, a._l2, ref carry), s3 = AddCarry(t._l3, a._l3, ref carry);
        ulong s4 = AddCarry(t._l4, a._l4, ref carry), s5 = AddCarry(t._l5, a._l5, ref carry);
        carry = 0;
        ulong h0 = AddCarry(t._l0, s0 << 1, ref carry);
        ulong h1 = AddCarry(t._l1, (s1 << 1) | (s0 >> 63), ref carry);
        ulong h2 = AddCarry(t._l2, (s2 << 1) | (s1 >> 63), ref carry);
        ulong h3 = AddCarry(t._l3, (s3 << 1) | (s2 >> 63), ref carry);
        ulong h4 = AddCarry(t._l4, (s4 << 1) | (s3 >> 63), ref carry);
        ulong h5 = AddCarry(t._l5, (s5 << 1) | (s4 >> 63), ref carry);
        ref ulong c = ref MemoryMarshal.GetArrayDataReference(Arm64WideCorrection.Limbs);
        ReduceArm64(out Fp reduced, h0, h1, h2, h3, h4, h5,
            c, Unsafe.Add(ref c, 1), Unsafe.Add(ref c, 2), Unsafe.Add(ref c, 3), Unsafe.Add(ref c, 4), Unsafe.Add(ref c, 5));
        ReduceArm64(out reduced, reduced._l0, reduced._l1, reduced._l2, reduced._l3, reduced._l4, reduced._l5,
            Unsafe.Add(ref c, 6), Unsafe.Add(ref c, 7), Unsafe.Add(ref c, 8), Unsafe.Add(ref c, 9), Unsafe.Add(ref c, 10), Unsafe.Add(ref c, 11));
        ReduceArm64(out result, reduced._l0, reduced._l1, reduced._l2, reduced._l3, reduced._l4, reduced._l5);
    }

    // 3*t-2*a = 2*(t-a)+t lies in (-2p,3p). Adding 2p only when
    // negative leaves [0,3p); corrections by 2p and p canonicalize it.
    // All original inputs precede output.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TripleSubtractDoubleArm64(out Fp result, in Fp t, in Fp a)
    {
        ulong borrow = 0;
        ulong s0 = SubBorrow(t._l0, a._l0, ref borrow), s1 = SubBorrow(t._l1, a._l1, ref borrow);
        ulong s2 = SubBorrow(t._l2, a._l2, ref borrow), s3 = SubBorrow(t._l3, a._l3, ref borrow);
        ulong s4 = SubBorrow(t._l4, a._l4, ref borrow), s5 = SubBorrow(t._l5, a._l5, ref borrow);
        ulong carry = 0;
        ulong h0 = AddCarry(t._l0, s0 << 1, ref carry);
        ulong h1 = AddCarry(t._l1, (s1 << 1) | (s0 >> 63), ref carry);
        ulong h2 = AddCarry(t._l2, (s2 << 1) | (s1 >> 63), ref carry);
        ulong h3 = AddCarry(t._l3, (s3 << 1) | (s2 >> 63), ref carry);
        ulong h4 = AddCarry(t._l4, (s4 << 1) | (s3 >> 63), ref carry);
        ulong h5 = AddCarry(t._l5, (s5 << 1) | (s4 >> 63), ref carry);
        ref ulong c = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Arm64WideCorrection.Limbs), 6);
        ulong mask = 0UL - (h5 >> 63);
        carry = 0;
        h0 = AddCarry(h0, c & mask, ref carry);
        h1 = AddCarry(h1, Unsafe.Add(ref c, 1) & mask, ref carry);
        h2 = AddCarry(h2, Unsafe.Add(ref c, 2) & mask, ref carry);
        h3 = AddCarry(h3, Unsafe.Add(ref c, 3) & mask, ref carry);
        h4 = AddCarry(h4, Unsafe.Add(ref c, 4) & mask, ref carry);
        h5 = AddCarry(h5, Unsafe.Add(ref c, 5) & mask, ref carry);
        ReduceArm64(out Fp reduced, h0, h1, h2, h3, h4, h5,
            c, Unsafe.Add(ref c, 1), Unsafe.Add(ref c, 2), Unsafe.Add(ref c, 3), Unsafe.Add(ref c, 4), Unsafe.Add(ref c, 5));
        ReduceArm64(out result, reduced._l0, reduced._l1, reduced._l2, reduced._l3, reduced._l4, reduced._l5);
    }

    // An isolated leaf preserves carry/select recognition when the caller has
    // a loop or another large inlined arithmetic body. Both policies share it.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SquareArm64(out Fp result, in Fp value)
    {
        SquareLazyCoreArm64(out Fp quotient, value);
        ReduceArm64(out result, quotient._l0, quotient._l1, quotient._l2, quotient._l3, quotient._l4, quotient._l5);
    }

    // Inputs below 2p give a^2 < 4p^2 < pR and a quotient below 2p.
    // This bound is preserved across a public-count square run.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SquareLazyArm64(out Fp result, in Fp value) => SquareLazyCoreArm64(out result, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SquareLazyCoreArm64(out Fp result, in Fp value)
    {
        ulong a0 = value._l0, a1 = value._l1, a2 = value._l2;
        ulong a3 = value._l3, a4 = value._l4, a5 = value._l5;
        ulong r0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
        ulong r6 = 0, r7 = 0, r8 = 0, r9 = 0, r10 = 0, r11;
        // Accumulate the fifteen cross products in whole low/high carry rows.
        // Each row starts at 2i+1 and drains into a previously zero top word.
        AddProductRowArm64(a0, a1, a2, a3, a4, a5, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6);
        AddProductRowArm64(a1, a2, a3, a4, a5, ref r3, ref r4, ref r5, ref r6, ref r7);
        AddProductRowArm64(a2, a3, a4, a5, ref r5, ref r6, ref r7, ref r8);
        AddProductRowArm64(a3, a4, a5, ref r7, ref r8, ref r9);
        r10 = MulAdd(a4, a5, r9, 0, out r9);

        ulong carry = 0;
        r1 = AddCarry(r1, r1, ref carry);
        r2 = AddCarry(r2, r2, ref carry);
        r3 = AddCarry(r3, r3, ref carry);
        r4 = AddCarry(r4, r4, ref carry);
        r5 = AddCarry(r5, r5, ref carry);
        r6 = AddCarry(r6, r6, ref carry);
        r7 = AddCarry(r7, r7, ref carry);
        r8 = AddCarry(r8, r8, ref carry);
        r9 = AddCarry(r9, r9, ref carry);
        r10 = AddCarry(r10, r10, ref carry);
        carry = 0;
        ulong h0 = Math.BigMul(a0, a0, out r0);
        r1 = AddCarry(r1, h0, ref carry);
        ulong h1 = Math.BigMul(a1, a1, out ulong d1);
        r2 = AddCarry(r2, d1, ref carry);
        r3 = AddCarry(r3, h1, ref carry);
        ulong h2 = Math.BigMul(a2, a2, out ulong d2);
        r4 = AddCarry(r4, d2, ref carry);
        r5 = AddCarry(r5, h2, ref carry);
        ulong h3 = Math.BigMul(a3, a3, out ulong d3);
        r6 = AddCarry(r6, d3, ref carry);
        r7 = AddCarry(r7, h3, ref carry);
        ulong h4 = Math.BigMul(a4, a4, out ulong d4);
        r8 = AddCarry(r8, d4, ref carry);
        r9 = AddCarry(r9, h4, ref carry);
        ulong h5 = Math.BigMul(a5, a5, out ulong d5);
        r10 = AddCarry(r10, d5, ref carry);
        r11 = h5 + carry;
        FpWide wide = new()
        {
            L0 = r0, L1 = r1, L2 = r2, L3 = r3, L4 = r4, L5 = r5,
            L6 = r6, L7 = r7, L8 = r8, L9 = r9, L10 = r10, L11 = r11,
        };
        // Output is delayed until every input limb has been consumed.
        ReduceWideCoreArm64(out result, wide);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideSmallCorrectArm64(out Fp result, in Fp quotient)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = m, m1 = Unsafe.Add(ref m, 1), m2 = Unsafe.Add(ref m, 2);
        ulong m3 = Unsafe.Add(ref m, 3), m4 = Unsafe.Add(ref m, 4), m5 = Unsafe.Add(ref m, 5);
        ulong mask = 0UL - (quotient._l5 >> 63), carry = 0;
        ulong h0 = AddCarry(quotient._l0, m0 & mask, ref carry);
        ulong h1 = AddCarry(quotient._l1, m1 & mask, ref carry);
        ulong h2 = AddCarry(quotient._l2, m2 & mask, ref carry);
        ulong h3 = AddCarry(quotient._l3, m3 & mask, ref carry);
        ulong h4 = AddCarry(quotient._l4, m4 & mask, ref carry);
        ulong h5 = AddCarry(quotient._l5, m5 & mask, ref carry);
        ReduceArm64(out result, h0, h1, h2, h3, h4, h5, m0, m1, m2, m3, m4, m5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractArm64(out Fp result, in Fp left, in Fp right)
    {
        // Loading the modulus saves the MOVZ/MOVK sequences for six constants.
        // The final addition supplies exactly the flags needed for selection.
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        SubtractArm64(out result, left, right, m, Unsafe.Add(ref m, 1), Unsafe.Add(ref m, 2),
            Unsafe.Add(ref m, 3), Unsafe.Add(ref m, 4), Unsafe.Add(ref m, 5));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SubtractArm64(out Fp result, in Fp left, in Fp right,
        ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5)
    {
        ulong borrow = 0;
        ulong r0 = SubBorrow(left._l0, right._l0, ref borrow);
        ulong r1 = SubBorrow(left._l1, right._l1, ref borrow);
        ulong r2 = SubBorrow(left._l2, right._l2, ref borrow);
        ulong r3 = SubBorrow(left._l3, right._l3, ref borrow);
        ulong r4 = SubBorrow(left._l4, right._l4, ref borrow);
        ulong r5 = SubBorrow(left._l5, right._l5, ref borrow);
        ulong carry = 0;
        ulong s0 = AddCarry(r0, m0, ref carry);
        ulong s1 = AddCarry(r1, m1, ref carry);
        ulong s2 = AddCarry(r2, m2, ref carry);
        ulong s3 = AddCarry(r3, m3, ref carry);
        ulong s4 = AddCarry(r4, m4, ref carry);
        ulong s5 = AddCarry(r5, m5, ref carry);
        ulong mask = ChoiceToMask(carry);
        result = new Fp(Select(s0, r0, mask), Select(s1, r1, mask), Select(s2, r2, mask),
            Select(s3, r3, mask), Select(s4, r4, mask), Select(s5, r5, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyWideArm64(out FpWide result, in Fp left, in Fp right)
    {
        ulong a0 = left._l0, a1 = left._l1, a2 = left._l2;
        ulong a3 = left._l3, a4 = left._l4, a5 = left._l5;
        ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
        ulong r7 = 0, r8 = 0, r9 = 0, r10 = 0, r11 = 0;
        AddProductRowArm64(right._l0, a0, a1, a2, a3, a4, a5, ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6);
        AddProductRowArm64(right._l1, a0, a1, a2, a3, a4, a5, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        AddProductRowArm64(right._l2, a0, a1, a2, a3, a4, a5, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8);
        AddProductRowArm64(right._l3, a0, a1, a2, a3, a4, a5, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9);
        AddProductRowArm64(right._l4, a0, a1, a2, a3, a4, a5, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10);
        AddProductRowArm64(right._l5, a0, a1, a2, a3, a4, a5, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11);
        // Keep every completed word in a scalar until both inputs are consumed.
        // This also permits output overlap with either half of a wide input.
        result = new FpWide
        {
            L0 = r0, L1 = r1, L2 = r2, L3 = r3, L4 = r4, L5 = r5,
            L6 = r6, L7 = r7, L8 = r8, L9 = r9, L10 = r10, L11 = r11,
        };
    }

    // Only the disjoint-output entry point stores completed low limbs before
    // reading the next right-hand limb. Ordinary wide products preserve aliases.
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyWideNoAliasArm64(out FpWide result, in Fp left, in Fp right)
    {
        ulong a0 = left._l0, a1 = left._l1, a2 = left._l2;
        ulong a3 = left._l3, a4 = left._l4, a5 = left._l5;
        ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
        ulong r7 = 0, r8 = 0, r9 = 0, r10 = 0, r11 = 0;
        Unsafe.SkipInit(out result);
        AddProductRowArm64(right._l0, a0, a1, a2, a3, a4, a5, ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6);
        result.L0 = r0;
        AddProductRowArm64(right._l1, a0, a1, a2, a3, a4, a5, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        result.L1 = r1;
        AddProductRowArm64(right._l2, a0, a1, a2, a3, a4, a5, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8);
        result.L2 = r2;
        AddProductRowArm64(right._l3, a0, a1, a2, a3, a4, a5, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9);
        result.L3 = r3;
        AddProductRowArm64(right._l4, a0, a1, a2, a3, a4, a5, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10);
        result.L4 = r4;
        AddProductRowArm64(right._l5, a0, a1, a2, a3, a4, a5, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11);
        result.L5 = r5;
        result.L6 = r6; result.L7 = r7; result.L8 = r8;
        result.L9 = r9; result.L10 = r10; result.L11 = r11;
    }

    // Two whole-row carry chains: add all low halves, then the high halves
    // one limb above them. MUL/UMULH preserve NZCV, so products can be issued
    // between ADCS instructions without materializing a carry for every limb.
    // The portable high-half fallback lets the tests exercise this formulation
    // on x64 as well; the public dispatcher selects it only on ARM64.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyArm64(out Fp result, in Fp left, in Fp right)
    {
        ulong a0 = left._l0, a1 = left._l1, a2 = left._l2;
        ulong a3 = left._l3, a4 = left._l4, a5 = left._l5;
        ref ulong b = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in right));
        ref ulong modulus = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = modulus, m1 = Unsafe.Add(ref modulus, 1), m2 = Unsafe.Add(ref modulus, 2);
        ulong m3 = Unsafe.Add(ref modulus, 3), m4 = Unsafe.Add(ref modulus, 4), m5 = Unsafe.Add(ref modulus, 5);
        ulong t0 = 0, t1 = 0, t2 = 0, t3 = 0, t4 = 0, t5 = 0;
        MultiplyStepArm64(b, a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        MultiplyStepArm64(Unsafe.Add(ref b, 1), a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        MultiplyStepArm64(Unsafe.Add(ref b, 2), a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        MultiplyStepArm64(Unsafe.Add(ref b, 3), a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        MultiplyStepArm64(Unsafe.Add(ref b, 4), a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        MultiplyStepArm64(Unsafe.Add(ref b, 5), a0, a1, a2, a3, a4, a5, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        // left, right < 2p implies the REDC quotient is below 2p because
        // 4p^2 < pR. Thus the seven-word accumulator never overflows and
        // the final quotient fits six words. Delay all output to preserve aliases.
        ReduceArm64(out result, t0, t1, t2, t3, t4, t5, m0, m1, m2, m3, m4, m5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyStepArm64(ulong b, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5,
        ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5)
    {
        ulong top = 0;
        AddProductRowArm64(b, a0, a1, a2, a3, a4, a5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5, ref top);
        ReduceRowArm64(t0 * N0, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5, ref top);
        t0 = t1; t1 = t2; t2 = t3; t3 = t4; t4 = t5; t5 = top;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceArm64(out Fp result, ulong t0, ulong t1, ulong t2, ulong t3, ulong t4, ulong t5,
        ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5)
    {
        ulong borrow = 0;
        ulong d0 = SubBorrow(t0, m0, ref borrow), d1 = SubBorrow(t1, m1, ref borrow);
        ulong d2 = SubBorrow(t2, m2, ref borrow), d3 = SubBorrow(t3, m3, ref borrow);
        ulong d4 = SubBorrow(t4, m4, ref borrow), d5 = SubBorrow(t5, m5, ref borrow);
        ulong mask = ChoiceToMask(borrow);
        result = new Fp(Select(t0, d0, mask), Select(t1, d1, mask), Select(t2, d2, mask),
            Select(t3, d3, mask), Select(t4, d4, mask), Select(t5, d5, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceArm64(out Fp result, ulong t0, ulong t1, ulong t2, ulong t3, ulong t4, ulong t5)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ReduceArm64(out result, t0, t1, t2, t3, t4, t5,
            m, Unsafe.Add(ref m, 1), Unsafe.Add(ref m, 2), Unsafe.Add(ref m, 3), Unsafe.Add(ref m, 4), Unsafe.Add(ref m, 5));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideCoreArm64(out Fp result, in FpWide value)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = m, m1 = Unsafe.Add(ref m, 1), m2 = Unsafe.Add(ref m, 2);
        ulong m3 = Unsafe.Add(ref m, 3), m4 = Unsafe.Add(ref m, 4), m5 = Unsafe.Add(ref m, 5);
        ulong t0 = value.L0, t1 = value.L1, t2 = value.L2;
        ulong t3 = value.L3, t4 = value.L4, t5 = value.L5;
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ulong carry = 0;
        ulong h0 = AddCarry(t0, value.L6, ref carry), h1 = AddCarry(t1, value.L7, ref carry);
        ulong h2 = AddCarry(t2, value.L8, ref carry), h3 = AddCarry(t3, value.L9, ref carry);
        ulong h4 = AddCarry(t4, value.L10, ref carry), h5 = AddCarry(t5, value.L11, ref carry);
        result = new Fp(h0, h1, h2, h3, h4, h5);
    }

    // Full |value|<3pR reduction belongs to the variable-time Fp6/Fp12
    // formulas. Secret Fp/Fp2, curve and map paths use the branch-free
    // small/positive/difference reducers instead. Strict top-word bounds
    // prove canonicality without inspecting the lower words.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideVariableArm64(out Fp result, in FpWide value)
    {
        ReduceWideCoreArm64(out Fp quotient, value);
        ref ulong modulus = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong top = Unsafe.Add(ref modulus, 5);
        if (quotient._l5 < top)
        {
            result = quotient;
            return;
        }
        // M4 < 2^63, so doubling p adds no carry to its top word.
        if (quotient._l5 < (top << 1))
        {
            ReduceArm64(out result, quotient._l0, quotient._l1, quotient._l2,
                quotient._l3, quotient._l4, quotient._l5);
            return;
        }
        ReduceWideCorrectArm64(out result, quotient);
    }

    // Preserve the earlier branch-free signed correction while the independent
    // scheduling candidates are measured by the managed benchmark harness.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideSmallArm64(out Fp result, in FpWide value)
    {
        ReduceWideCoreArm64(out Fp quotient, value);
        ReduceWideSmallCorrectArm64(out result, quotient);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceStepArm64(ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5)
    {
        ulong top = 0;
        ReduceRowArm64(t0 * N0, m0, m1, m2, m3, m4, m5,
            ref t0, ref t1, ref t2, ref t3, ref t4, ref t5, ref top);
        t0 = t1; t1 = t2; t2 = t3; t3 = t4; t4 = t5; t5 = top;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ProductHighArm64(ulong a, ulong b)
    {
        if (ArmBase.Arm64.IsSupported) return ArmBase.Arm64.MultiplyHigh(a, b);
        return Math.BigMul(a, b, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddProductRowArm64(ulong m, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5, ref ulong t6)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, m * a0, ref carry);
        t1 = AddCarry(t1, m * a1, ref carry);
        t2 = AddCarry(t2, m * a2, ref carry);
        t3 = AddCarry(t3, m * a3, ref carry);
        t4 = AddCarry(t4, m * a4, ref carry);
        t5 = AddCarry(t5, m * a5, ref carry);
        ulong top = t6 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(m, a0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(m, a1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(m, a2), ref carry);
        t4 = AddCarry(t4, ProductHighArm64(m, a3), ref carry);
        t5 = AddCarry(t5, ProductHighArm64(m, a4), ref carry);
        t6 = top + ProductHighArm64(m, a5) + carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddProductRowArm64(ulong m, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, m * a0, ref carry);
        t1 = AddCarry(t1, m * a1, ref carry);
        t2 = AddCarry(t2, m * a2, ref carry);
        t3 = AddCarry(t3, m * a3, ref carry);
        t4 = AddCarry(t4, m * a4, ref carry);
        ulong top = t5 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(m, a0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(m, a1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(m, a2), ref carry);
        t4 = AddCarry(t4, ProductHighArm64(m, a3), ref carry);
        t5 = top + ProductHighArm64(m, a4) + carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddProductRowArm64(ulong m, ulong a0, ulong a1, ulong a2, ulong a3,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, m * a0, ref carry);
        t1 = AddCarry(t1, m * a1, ref carry);
        t2 = AddCarry(t2, m * a2, ref carry);
        t3 = AddCarry(t3, m * a3, ref carry);
        ulong top = t4 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(m, a0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(m, a1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(m, a2), ref carry);
        t4 = top + ProductHighArm64(m, a3) + carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddProductRowArm64(ulong m, ulong a0, ulong a1, ulong a2,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, m * a0, ref carry);
        t1 = AddCarry(t1, m * a1, ref carry);
        t2 = AddCarry(t2, m * a2, ref carry);
        ulong top = t3 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(m, a0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(m, a1), ref carry);
        t3 = top + ProductHighArm64(m, a2) + carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddProductRowArm64(ulong m, ulong a0, ulong a1, ref ulong t0, ref ulong t1, ref ulong t2)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, m * a0, ref carry);
        t1 = AddCarry(t1, m * a1, ref carry);
        ulong top = t2 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(m, a0), ref carry);
        t2 = top + ProductHighArm64(m, a1) + carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceRowArm64(ulong q, ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5, ref ulong t6)
    {
        // t0 + q*m0 is zero modulo 2^64. Its carry is exactly t0 != 0;
        // spell that as the carry of t0 + (2^64-1) to expose SUBS/CMP.
        // This avoids calculating q*m0's low half, as in blst's ARM64 REDC.
        ulong seed = t0 + ulong.MaxValue;
        ulong carry = seed < t0 ? 1UL : 0UL;
        t1 = AddCarry(t1, q * m1, ref carry);
        t2 = AddCarry(t2, q * m2, ref carry);
        t3 = AddCarry(t3, q * m3, ref carry);
        t4 = AddCarry(t4, q * m4, ref carry);
        t5 = AddCarry(t5, q * m5, ref carry);
        ulong top = t6 + carry;
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(q, m0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(q, m1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(q, m2), ref carry);
        t4 = AddCarry(t4, ProductHighArm64(q, m3), ref carry);
        t5 = AddCarry(t5, ProductHighArm64(q, m4), ref carry);
        t6 = top + ProductHighArm64(q, m5) + carry;
    }
}
