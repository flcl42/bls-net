// ARM64 carry-row scheduling adapted from blst's Montgomery assembly,
// copyright Supranational LLC, Apache-2.0. See NOTICE.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fr
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplyArm64(out Fr result, in Fr left, in Fr right)
    {
        ulong a0 = left._l0, a1 = left._l1, a2 = left._l2, a3 = left._l3;
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ulong m0 = m, m1 = Unsafe.Add(ref m, 1), m2 = Unsafe.Add(ref m, 2), m3 = Unsafe.Add(ref m, 3);
        ulong t0 = 0, t1 = 0, t2 = 0, t3 = 0;
        MultiplyStepArm64(right._l0, a0, a1, a2, a3, m0, m1, m2, m3, ref t0, ref t1, ref t2, ref t3);
        MultiplyStepArm64(right._l1, a0, a1, a2, a3, m0, m1, m2, m3, ref t0, ref t1, ref t2, ref t3);
        MultiplyStepArm64(right._l2, a0, a1, a2, a3, m0, m1, m2, m3, ref t0, ref t1, ref t2, ref t3);
        MultiplyStepArm64(right._l3, a0, a1, a2, a3, m0, m1, m2, m3, ref t0, ref t1, ref t2, ref t3);
        // Canonical operands give a quotient below 2r < 2^256. All input
        // reads precede result assignment, including either input alias.
        ReduceArm64(out result, t0, t1, t2, t3, m0, m1, m2, m3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyStepArm64(ulong b, ulong a0, ulong a1, ulong a2, ulong a3,
        ulong m0, ulong m1, ulong m2, ulong m3, ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3)
    {
        ulong carry = 0;
        t0 = AddCarry(t0, b * a0, ref carry);
        t1 = AddCarry(t1, b * a1, ref carry);
        t2 = AddCarry(t2, b * a2, ref carry);
        t3 = AddCarry(t3, b * a3, ref carry);
        ulong top = AddCarry(ProductHighArm64(b, a3), 0, ref carry);
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(b, a0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(b, a1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(b, a2), ref carry);
        top = AddCarry(top, 0, ref carry);

        ulong q = t0 * N0;
        ulong seed = t0 + ulong.MaxValue;
        carry = seed < t0 ? 1UL : 0UL;
        t1 = AddCarry(t1, q * m1, ref carry);
        t2 = AddCarry(t2, q * m2, ref carry);
        t3 = AddCarry(t3, q * m3, ref carry);
        top = AddCarry(top, 0, ref carry);
        carry = 0;
        t1 = AddCarry(t1, ProductHighArm64(q, m0), ref carry);
        t2 = AddCarry(t2, ProductHighArm64(q, m1), ref carry);
        t3 = AddCarry(t3, ProductHighArm64(q, m2), ref carry);
        top = AddCarry(top, ProductHighArm64(q, m3), ref carry);
        t0 = t1; t1 = t2; t2 = t3; t3 = top;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ProductHighArm64(ulong a, ulong b)
    {
        if (ArmBase.Arm64.IsSupported) return ArmBase.Arm64.MultiplyHigh(a, b);
        return Math.BigMul(a, b, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceArm64(out Fr result, ulong t0, ulong t1, ulong t2, ulong t3)
    {
        ref ulong m = ref MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        ReduceArm64(out result, t0, t1, t2, t3, m, Unsafe.Add(ref m, 1), Unsafe.Add(ref m, 2), Unsafe.Add(ref m, 3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceArm64(out Fr result, ulong t0, ulong t1, ulong t2, ulong t3,
        ulong m0, ulong m1, ulong m2, ulong m3)
    {
        ulong borrow = 0;
        ulong d0 = SubBorrow(t0, m0, ref borrow), d1 = SubBorrow(t1, m1, ref borrow);
        ulong d2 = SubBorrow(t2, m2, ref borrow), d3 = SubBorrow(t3, m3, ref borrow);
        ulong mask = ChoiceToMask(borrow);
        result = new Fr(Select(t0, d0, mask), Select(t1, d1, mask), Select(t2, d2, mask), Select(t3, d3, mask));
    }
}
