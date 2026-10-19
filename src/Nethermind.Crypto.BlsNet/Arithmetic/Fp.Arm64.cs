// ARM64 Montgomery scheduling adapted from blst's mul_mont_384-armv8.pl,
// copyright Supranational LLC, Apache-2.0. See NOTICE.
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.InteropServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
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
