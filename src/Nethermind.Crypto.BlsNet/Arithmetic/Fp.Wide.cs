using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

// Signed 768-bit intermediates, least-significant limb first. The tower
// formulas keep their magnitude below 22p^2 < 3p*2^384 < 2^767.
internal struct FpWide
{
    internal ulong L0;
    internal ulong L1;
    internal ulong L2;
    internal ulong L3;
    internal ulong L4;
    internal ulong L5;
    internal ulong L6;
    internal ulong L7;
    internal ulong L8;
    internal ulong L9;
    internal ulong L10;
    internal ulong L11;
}

public readonly partial struct Fp
{
    // Read policy before calling a full-optimization kernel. Even a cold first
    // call then initializes it before that kernel is compiled; Tier1 callers
    // fold this small dispatcher and call only the chosen implementation.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplyWide(out FpWide result, in Fp a, in Fp b)
    {
        if (UseExpandedKernels) MultiplyWideExpanded(out result, a, b);
        else MultiplyWideCompact(out result, a, b);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyWideCompact(out FpWide result, in Fp a, in Fp b)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            MultiplyWideArm64(out result, a, b);
            return;
        }
        Unsafe.SkipInit(out FpWide r);
        ref ulong br = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in b));
        ref ulong rr = ref r.L0;
        ulong a0 = a._l0;
        ulong a1 = a._l1;
        ulong a2 = a._l2;
        ulong a3 = a._l3;
        ulong a4 = a._l4;
        ulong a5 = a._l5;
        ulong t0 = 0;
        ulong t1 = 0;
        ulong t2 = 0;
        ulong t3 = 0;
        ulong t4 = 0;
        ulong t5 = 0;
        for (int i = 0; i < 6; i++)
        {
            ulong row = Unsafe.Add(ref br, i);
            ulong carry = MulAdd(row, a0, t0, 0, out ulong low);
            carry = MulAdd(row, a1, t1, carry, out t0);
            carry = MulAdd(row, a2, t2, carry, out t1);
            carry = MulAdd(row, a3, t3, carry, out t2);
            carry = MulAdd(row, a4, t4, carry, out t3);
            carry = MulAdd(row, a5, t5, carry, out t4);
            Unsafe.Add(ref rr, i) = low;
            t5 = carry;
        }

        r.L6 = t0;
        r.L7 = t1;
        r.L8 = t2;
        r.L9 = t3;
        r.L10 = t4;
        r.L11 = t5;
        result = r;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyWideExpanded(out FpWide result, in Fp a, in Fp b)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            MultiplyWideArm64(out result, a, b);
            return;
        }
        Unsafe.SkipInit(out FpWide r);
        ref ulong br = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in b));
        ref ulong rr = ref r.L0;
        ulong a0 = a._l0;
        ulong a1 = a._l1;
        ulong a2 = a._l2;
        ulong a3 = a._l3;
        ulong a4 = a._l4;
        ulong a5 = a._l5;
        ulong t0 = 0;
        ulong t1 = 0;
        ulong t2 = 0;
        ulong t3 = 0;
        ulong t4 = 0;
        ulong t5 = 0;
        ulong row = Unsafe.Add(ref br, 0);
        ulong carry = MulAdd(row, a0, t0, 0, out ulong low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 0) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 1);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 1) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 2);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 2) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 3);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 3) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 4);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 4) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 5);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 5) = low;
        t5 = carry;

        r.L6 = t0;
        r.L7 = t1;
        r.L8 = t2;
        r.L9 = t3;
        r.L10 = t4;
        r.L11 = t5;
        result = r;
    }

    // The output must be disjoint from both inputs. Composite products use
    // independent scratch buffers, so their low words can go directly there.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplyWideNoAlias(out FpWide result, in Fp a, in Fp b)
    {
        if (UseExpandedKernels) MultiplyWideNoAliasExpanded(out result, a, b);
        else MultiplyWideCompact(out result, a, b);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyWideNoAliasExpanded(out FpWide result, in Fp a, in Fp b)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            MultiplyWideNoAliasArm64(out result, a, b);
            return;
        }
        Unsafe.SkipInit(out result);
        ref ulong br = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in b));
        ref ulong rr = ref result.L0;
        ulong a0 = a._l0;
        ulong a1 = a._l1;
        ulong a2 = a._l2;
        ulong a3 = a._l3;
        ulong a4 = a._l4;
        ulong a5 = a._l5;
        ulong t0 = 0;
        ulong t1 = 0;
        ulong t2 = 0;
        ulong t3 = 0;
        ulong t4 = 0;
        ulong t5 = 0;
        ulong row = Unsafe.Add(ref br, 0);
        ulong carry = MulAdd(row, a0, t0, 0, out ulong low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 0) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 1);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 1) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 2);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 2) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 3);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 3) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 4);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 4) = low;
        t5 = carry;
        row = Unsafe.Add(ref br, 5);
        carry = MulAdd(row, a0, t0, 0, out low);
        carry = MulAdd(row, a1, t1, carry, out t0);
        carry = MulAdd(row, a2, t2, carry, out t1);
        carry = MulAdd(row, a3, t3, carry, out t2);
        carry = MulAdd(row, a4, t4, carry, out t3);
        carry = MulAdd(row, a5, t5, carry, out t4);
        Unsafe.Add(ref rr, 5) = low;
        t5 = carry;

        result.L6 = t0;
        result.L7 = t1;
        result.L8 = t2;
        result.L9 = t3;
        result.L10 = t4;
        result.L11 = t5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceWideCore(out Fp result, in FpWide value)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceWideCoreArm64(out result, value);
            return;
        }
        ulong t0 = value.L0;
        ulong t1 = value.L1;
        ulong t2 = value.L2;
        ulong t3 = value.L3;
        ulong t4 = value.L4;
        ulong t5 = value.L5;
        for (int i = 0; i < 6; i++)
        {
            ulong q = t0 * N0;
            ulong carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            carry = MulAdd(q, M4, t4, carry, out t3);
            carry = MulAdd(q, M5, t5, carry, out t4);
            t5 = carry;
        }
        // REDC(value) = REDC(low 384 bits) + signed high 384 bits.
        // Defer the high half until all six rows are finished: there is no
        // loop-carried carry bit to materialize at each row boundary.
        ulong h0 = t0 + value.L6;
        ulong addCarry = h0 < t0 ? 1UL : 0UL;
        ulong h1 = AddCarry(t1, value.L7, ref addCarry);
        ulong h2 = AddCarry(t2, value.L8, ref addCarry);
        ulong h3 = AddCarry(t3, value.L9, ref addCarry);
        ulong h4 = AddCarry(t4, value.L10, ref addCarry);
        ulong h5 = AddCarry(t5, value.L11, ref addCarry);
        result = new Fp(h0, h1, h2, h3, h4, h5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceWideCompactCore(out Fp result, in FpWide value)
    {
        ref ulong high = ref Unsafe.AsRef(in value.L6);
        ulong t0 = value.L0;
        ulong t1 = value.L1;
        ulong t2 = value.L2;
        ulong t3 = value.L3;
        ulong t4 = value.L4;
        ulong t5 = value.L5;
        ulong topCarry = 0;
        for (int i = 0; i < 6; i++)
        {
            ulong q = t0 * N0;
            ulong carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            carry = MulAdd(q, M4, t4, carry, out t3);
            carry = MulAdd(q, M5, t5, carry, out t4);
            t5 = AddCarry(Unsafe.Add(ref high, i), carry, ref topCarry);
        }
        result = new Fp(t0, t1, t2, t3, t4, t5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWide(out Fp result, in FpWide value)
    {
        if (UseExpandedKernels) ReduceWideExpanded(out result, value);
        else ReduceWideCompact(out result, value);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReduceWideCompact(out Fp result, in FpWide value)
    {
        ReduceWideCompactCore(out Fp quotient, value);
        ReduceWideCorrect(out result, quotient);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReduceWideExpanded(out Fp result, in FpWide value)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceWideVariableArm64(out result, value);
            return;
        }
        ReduceWideCore(out Fp quotient, value);
        ReduceWideCorrect(out result, quotient);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceWideCorrect(out Fp result, in Fp quotient)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceWideCorrectArm64(out result, quotient);
            return;
        }
        ulong t0 = quotient._l0, t1 = quotient._l1, t2 = quotient._l2;
        ulong t3 = quotient._l3, t4 = quotient._l4, t5 = quotient._l5;
        // |value|<3pR yields a quotient in (-3p,4p). Since 4p<2^383,
        // its sign bit suffices. Add 4p if negative, then reduce by 2p and p.
        ulong mask = 0 - (t5 >> 63);
        ulong correctionCarry = 0;
        ulong h0 = AddCarry(t0, 0xe7fbfffffffeaaacUL & mask, ref correctionCarry);
        ulong h1 = AddCarry(t1, 0x7aaffffac54ffffeUL & mask, ref correctionCarry);
        ulong h2 = AddCarry(t2, 0x9cc34a83dac3d890UL & mask, ref correctionCarry);
        ulong h3 = AddCarry(t3, 0x91dd2e13ce144afdUL & mask, ref correctionCarry);
        ulong h4 = AddCarry(t4, 0x2c6e9ed90d2eb35dUL & mask, ref correctionCarry);
        ulong h5 = AddCarry(t5, 0x680447a8e5ff9a69UL & mask, ref correctionCarry);
        ulong subtractCarry = 0;
        ulong d0 = AddCarry(h0, 0x8c0200000000aaaaUL, ref subtractCarry);
        ulong d1 = AddCarry(h1, 0xc2a800029d580000UL, ref subtractCarry);
        ulong d2 = AddCarry(h2, 0x319e5abe129e13b7UL, ref subtractCarry);
        ulong d3 = AddCarry(h3, 0x371168f618f5da81UL, ref subtractCarry);
        ulong d4 = AddCarry(h4, 0x69c8b0937968a651UL, ref subtractCarry);
        ulong d5 = AddCarry(h5, 0xcbfddc2b8d0032cbUL, ref subtractCarry);
        ulong select = ChoiceToMask(subtractCarry);
        h0 = Select(d0, h0, select);
        h1 = Select(d1, h1, select);
        h2 = Select(d2, h2, select);
        h3 = Select(d3, h3, select);
        h4 = Select(d4, h4, select);
        h5 = Select(d5, h5, select);
        Reduce(out result, h0, h1, h2, h3, h4, h5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWideSmall(out Fp result, in FpWide value)
    {
        if (UseExpandedKernels) ReduceWideSmallExpanded(out result, value);
        else ReduceWideSmallCompact(out result, value);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReduceWideSmallCompact(out Fp result, in FpWide value)
    {
        ReduceWideCompactCore(out Fp quotient, value);
        ReduceWideSmallCorrect(out result, quotient);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReduceWideSmallExpanded(out Fp result, in FpWide value)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceWideSmallArm64(out result, value);
            return;
        }
        ReduceWideCore(out Fp quotient, value);
        ReduceWideSmallCorrect(out result, quotient);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceWideSmallCorrect(out Fp result, in Fp quotient)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceWideSmallCorrectArm64(out result, quotient);
            return;
        }
        ulong t0 = quotient._l0, t1 = quotient._l1, t2 = quotient._l2;
        ulong t3 = quotient._l3, t4 = quotient._l4, t5 = quotient._l5;
        // |value|<pR gives a quotient in (-p,2p); only one correction is needed.
        ulong mask = 0 - (t5 >> 63);
        ulong correctionCarry = 0;
        ulong h0 = AddCarry(t0, M0 & mask, ref correctionCarry);
        ulong h1 = AddCarry(t1, M1 & mask, ref correctionCarry);
        ulong h2 = AddCarry(t2, M2 & mask, ref correctionCarry);
        ulong h3 = AddCarry(t3, M3 & mask, ref correctionCarry);
        ulong h4 = AddCarry(t4, M4 & mask, ref correctionCarry);
        ulong h5 = AddCarry(t5, M5 & mask, ref correctionCarry);
        Reduce(out result, h0, h1, h2, h3, h4, h5);
    }

    // Requires |a-b| < pR. Keeping the difference local to this reducer avoids
    // writing and rereading the real component's twelve-word scratch buffer.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ReduceWideDifference(out Fp result, in FpWide a, in FpWide b)
    {
        SubtractWideInline(out FpWide difference, a, b);
        ReduceWideCore(out Fp quotient, difference);
        ReduceWideSmallCorrect(out result, quotient);
    }

    // Nonnegative values below pR need no signed correction in expanded mode.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReduceWidePositive(out Fp result, in FpWide value)
    {
        if (UseExpandedKernels) ReduceWidePositiveExpanded(out result, value);
        else ReduceWideSmallCompact(out result, value);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReduceWidePositiveExpanded(out Fp result, in FpWide value)
    {
        ReduceWideCore(out Fp quotient, value);
        Reduce(out result, quotient._l0, quotient._l1, quotient._l2,
            quotient._l3, quotient._l4, quotient._l5);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    // Transform three distinct product buffers into the real and imaginary
    // Karatsuba components, keeping t1 intact. The ref buffers are modified in place.
    internal static void SubtractWideProducts(ref FpWide t0, in FpWide t1, ref FpWide t2)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            SubtractWideProductsArm64(ref t0, t1, ref t2);
            return;
        }
        SubtractWideInline(out t2, t2, t0);
        SubtractWideInline(out t2, t2, t1);
        SubtractWideInline(out t0, t0, t1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    // Three distinct buffers; modify only t2. Real reduction reads t0 and t1.
    internal static void SubtractWideImaginary(ref FpWide t2, in FpWide t0, in FpWide t1)
    {
        SubtractWideInline(out t2, t2, t0);
        SubtractWideInline(out t2, t2, t1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void AddWide(out FpWide result, in FpWide a, in FpWide b)
    {
        ulong carry = 0;
        result.L0 = AddCarry(a.L0, b.L0, ref carry);
        result.L1 = AddCarry(a.L1, b.L1, ref carry);
        result.L2 = AddCarry(a.L2, b.L2, ref carry);
        result.L3 = AddCarry(a.L3, b.L3, ref carry);
        result.L4 = AddCarry(a.L4, b.L4, ref carry);
        result.L5 = AddCarry(a.L5, b.L5, ref carry);
        result.L6 = AddCarry(a.L6, b.L6, ref carry);
        result.L7 = AddCarry(a.L7, b.L7, ref carry);
        result.L8 = AddCarry(a.L8, b.L8, ref carry);
        result.L9 = AddCarry(a.L9, b.L9, ref carry);
        result.L10 = AddCarry(a.L10, b.L10, ref carry);
        result.L11 = AddCarry(a.L11, b.L11, ref carry);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddWideInline(out FpWide result, in FpWide a, in FpWide b)
    {
        ulong carry = 0;
        result.L0 = AddCarry(a.L0, b.L0, ref carry);
        result.L1 = AddCarry(a.L1, b.L1, ref carry);
        result.L2 = AddCarry(a.L2, b.L2, ref carry);
        result.L3 = AddCarry(a.L3, b.L3, ref carry);
        result.L4 = AddCarry(a.L4, b.L4, ref carry);
        result.L5 = AddCarry(a.L5, b.L5, ref carry);
        result.L6 = AddCarry(a.L6, b.L6, ref carry);
        result.L7 = AddCarry(a.L7, b.L7, ref carry);
        result.L8 = AddCarry(a.L8, b.L8, ref carry);
        result.L9 = AddCarry(a.L9, b.L9, ref carry);
        result.L10 = AddCarry(a.L10, b.L10, ref carry);
        result.L11 = AddCarry(a.L11, b.L11, ref carry);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SubtractWide(out FpWide result, in FpWide a, in FpWide b)
    {
        ulong borrow = 0;
        result.L0 = SubBorrow(a.L0, b.L0, ref borrow);
        result.L1 = SubBorrow(a.L1, b.L1, ref borrow);
        result.L2 = SubBorrow(a.L2, b.L2, ref borrow);
        result.L3 = SubBorrow(a.L3, b.L3, ref borrow);
        result.L4 = SubBorrow(a.L4, b.L4, ref borrow);
        result.L5 = SubBorrow(a.L5, b.L5, ref borrow);
        result.L6 = SubBorrow(a.L6, b.L6, ref borrow);
        result.L7 = SubBorrow(a.L7, b.L7, ref borrow);
        result.L8 = SubBorrow(a.L8, b.L8, ref borrow);
        result.L9 = SubBorrow(a.L9, b.L9, ref borrow);
        result.L10 = SubBorrow(a.L10, b.L10, ref borrow);
        result.L11 = SubBorrow(a.L11, b.L11, ref borrow);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractWideInline(out FpWide result, in FpWide a, in FpWide b)
    {
        ulong borrow = 0;
        result.L0 = SubBorrow(a.L0, b.L0, ref borrow);
        result.L1 = SubBorrow(a.L1, b.L1, ref borrow);
        result.L2 = SubBorrow(a.L2, b.L2, ref borrow);
        result.L3 = SubBorrow(a.L3, b.L3, ref borrow);
        result.L4 = SubBorrow(a.L4, b.L4, ref borrow);
        result.L5 = SubBorrow(a.L5, b.L5, ref borrow);
        result.L6 = SubBorrow(a.L6, b.L6, ref borrow);
        result.L7 = SubBorrow(a.L7, b.L7, ref borrow);
        result.L8 = SubBorrow(a.L8, b.L8, ref borrow);
        result.L9 = SubBorrow(a.L9, b.L9, ref borrow);
        result.L10 = SubBorrow(a.L10, b.L10, ref borrow);
        result.L11 = SubBorrow(a.L11, b.L11, ref borrow);
    }

}
