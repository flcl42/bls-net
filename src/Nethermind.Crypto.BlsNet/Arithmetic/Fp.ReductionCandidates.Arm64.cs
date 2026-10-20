// Temporary managed scheduling comparison; remove losing variants after hardware measurement.
// ARM64 Montgomery scheduling adapted from blst's mul_mont_384-armv8.pl,
// copyright Supranational LLC, Apache-2.0. See NOTICE.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // For |value|<pR, adding pR to a negative input makes it nonnegative
    // and below pR. Normalize the high half after the low-half rows:
    // its sign is already available, so this carry chain can overlap the
    // multiply work instead of waiting for the quotient's high word.
    // Only the ordinary final p subtraction remains.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideSmallLateArm64(out Fp result, in FpWide value)
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
        ulong mask = 0UL - (value.L11 >> 63), carry = 0;
        ulong h5 = value.L11 + (m5 & mask);
        ulong h0 = AddCarry(value.L6, m0 & mask, ref carry);
        ulong h1 = AddCarry(value.L7, m1 & mask, ref carry);
        ulong h2 = AddCarry(value.L8, m2 & mask, ref carry);
        ulong h3 = AddCarry(value.L9, m3 & mask, ref carry);
        ulong h4 = AddCarry(value.L10, m4 & mask, ref carry);
        h5 += carry;
        carry = 0;
        h0 = AddCarry(t0, h0, ref carry); h1 = AddCarry(t1, h1, ref carry);
        h2 = AddCarry(t2, h2, ref carry); h3 = AddCarry(t3, h3, ref carry);
        h4 = AddCarry(t4, h4, ref carry); h5 = AddCarry(t5, h5, ref carry);
        ReduceArm64(out result, h0, h1, h2, h3, h4, h5, m0, m1, m2, m3, m4, m5);
    }

    // The same pR input normalization between the fifth and sixth rows.
    // Keep it isolated so its extra live values can be measured directly.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideSmallRow5Arm64(out Fp result, in FpWide value)
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
        ulong mask = 0UL - (value.L11 >> 63), carry = 0;
        ulong h5 = value.L11 + (m5 & mask);
        ulong h0 = AddCarry(value.L6, m0 & mask, ref carry);
        ulong h1 = AddCarry(value.L7, m1 & mask, ref carry);
        ulong h2 = AddCarry(value.L8, m2 & mask, ref carry);
        ulong h3 = AddCarry(value.L9, m3 & mask, ref carry);
        ulong h4 = AddCarry(value.L10, m4 & mask, ref carry);
        h5 += carry;
        ReduceStepArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        carry = 0;
        h0 = AddCarry(t0, h0, ref carry); h1 = AddCarry(t1, h1, ref carry);
        h2 = AddCarry(t2, h2, ref carry); h3 = AddCarry(t3, h3, ref carry);
        h4 = AddCarry(t4, h4, ref carry); h5 = AddCarry(t5, h5, ref carry);
        ReduceArm64(out result, h0, h1, h2, h3, h4, h5, m0, m1, m2, m3, m4, m5);
    }

    // For |value|<pR, adding pR to a negative input makes it nonnegative
    // and below pR. Normalize the high half after the low-half rows:
    // its sign is already available, so this carry chain can overlap the
    // multiply work instead of waiting for the quotient's high word.
    // Only the ordinary final p subtraction remains.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideSmallHoistedArm64(out Fp result, in FpWide value)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = m, m1 = Unsafe.Add(ref m, 1), m2 = Unsafe.Add(ref m, 2);
        ulong m3 = Unsafe.Add(ref m, 3), m4 = Unsafe.Add(ref m, 4), m5 = Unsafe.Add(ref m, 5);
        ulong t0 = value.L0, t1 = value.L1, t2 = value.L2;
        ulong t3 = value.L3, t4 = value.L4, t5 = value.L5;
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ulong mask = 0UL - (value.L11 >> 63), carry = 0;
        ulong h5 = value.L11 + (m5 & mask);
        ulong h0 = AddCarry(value.L6, m0 & mask, ref carry);
        ulong h1 = AddCarry(value.L7, m1 & mask, ref carry);
        ulong h2 = AddCarry(value.L8, m2 & mask, ref carry);
        ulong h3 = AddCarry(value.L9, m3 & mask, ref carry);
        ulong h4 = AddCarry(value.L10, m4 & mask, ref carry);
        h5 += carry;
        carry = 0;
        h0 = AddCarry(t0, h0, ref carry); h1 = AddCarry(t1, h1, ref carry);
        h2 = AddCarry(t2, h2, ref carry); h3 = AddCarry(t3, h3, ref carry);
        h4 = AddCarry(t4, h4, ref carry); h5 = AddCarry(t5, h5, ref carry);
        ReduceArm64(out result, h0, h1, h2, h3, h4, h5, m0, m1, m2, m3, m4, m5);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideSmallSignedHoistedArm64(out Fp result, in FpWide value)
    {
        ReduceWideCoreEarlyHighArm64(out Fp quotient, value);
        ReduceWideSmallCorrectArm64(out result, quotient);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideCoreEarlyHighArm64(out Fp result, in FpWide value)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = m, m1 = Unsafe.Add(ref m, 1), m2 = Unsafe.Add(ref m, 2);
        ulong m3 = Unsafe.Add(ref m, 3), m4 = Unsafe.Add(ref m, 4), m5 = Unsafe.Add(ref m, 5);
        ulong t0 = value.L0, t1 = value.L1, t2 = value.L2;
        ulong t3 = value.L3, t4 = value.L4, t5 = value.L5;
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ReduceStepEarlyHighArm64(m0, m1, m2, m3, m4, m5, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
        ulong carry = 0;
        ulong h0 = AddCarry(t0, value.L6, ref carry), h1 = AddCarry(t1, value.L7, ref carry);
        ulong h2 = AddCarry(t2, value.L8, ref carry), h3 = AddCarry(t3, value.L9, ref carry);
        ulong h4 = AddCarry(t4, value.L10, ref carry), h5 = AddCarry(t5, value.L11, ref carry);
        result = new Fp(h0, h1, h2, h3, h4, h5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceStepEarlyHighArm64(ulong m0, ulong m1, ulong m2, ulong m3, ulong m4, ulong m5,
        ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5)
    {
        ulong q = t0 * N0;
        ulong high0 = ProductHighArm64(q, m0);
        ulong seed = t0 + ulong.MaxValue;
        ulong carry = seed < t0 ? 1UL : 0UL;
        t1 = AddCarry(t1, q * m1, ref carry);
        t2 = AddCarry(t2, q * m2, ref carry);
        t3 = AddCarry(t3, q * m3, ref carry);
        t4 = AddCarry(t4, q * m4, ref carry);
        t5 = AddCarry(t5, q * m5, ref carry);
        ulong top = carry;
        carry = 0;
        t1 = AddCarry(t1, high0, ref carry);
        t2 = AddCarry(t2, ProductHighArm64(q, m1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(q, m2), ref carry);
        t4 = AddCarry(t4, ProductHighArm64(q, m3), ref carry);
        t5 = AddCarry(t5, ProductHighArm64(q, m4), ref carry);
        top += ProductHighArm64(q, m5) + carry;
        t0 = t1; t1 = t2; t2 = t3; t3 = t4; t4 = t5; t5 = top;
    }
}
