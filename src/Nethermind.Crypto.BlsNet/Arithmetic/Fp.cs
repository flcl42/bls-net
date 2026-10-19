using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>BLS12-381 base field element stored in Montgomery form.</summary>
public readonly partial struct Fp : IEquatable<Fp>, IField<Fp>
{
    public const int ByteCount = 48;
    private const ulong N0 = 0x89f3fffcfffcfffdUL;
    private const ulong M0 = 0xb9feffffffffaaabUL;
    private const ulong M1 = 0x1eabfffeb153ffffUL;
    private const ulong M2 = 0x6730d2a0f6b0f624UL;
    private const ulong M3 = 0x64774b84f38512bfUL;
    private const ulong M4 = 0x4b1ba7b6434bacd7UL;
    private const ulong M5 = 0x1a0111ea397fe69aUL;

    // 2^384 - p.
    private const ulong NegM0 = 0x4601000000005555UL;
    private const ulong NegM1 = 0xe15400014eac0000UL;
    private const ulong NegM2 = 0x98cf2d5f094f09dbUL;
    private const ulong NegM3 = 0x9b88b47b0c7aed40UL;
    private const ulong NegM4 = 0xb4e45849bcb45328UL;
    private const ulong NegM5 = 0xe5feee15c6801965UL;

    private readonly ulong _l0;
    private readonly ulong _l1;
    private readonly ulong _l2;
    private readonly ulong _l3;
    private readonly ulong _l4;
    private readonly ulong _l5;

    private static readonly ulong[] ModulusLimbs =
    [
        0xb9feffffffffaaabUL,
        0x1eabfffeb153ffffUL,
        0x6730d2a0f6b0f624UL,
        0x64774b84f38512bfUL,
        0x4b1ba7b6434bacd7UL,
        0x1a0111ea397fe69aUL,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Fp(ulong l0, ulong l1, ulong l2, ulong l3, ulong l4, ulong l5)
    {
        _l0 = l0;
        _l1 = l1;
        _l2 = l2;
        _l3 = l3;
        _l4 = l4;
        _l5 = l5;
    }

    private Fp(ReadOnlySpan<ulong> limbs)
        : this(limbs[0], limbs[1], limbs[2], limbs[3], limbs[4], limbs[5])
    {
    }

    /// <summary>Creates an element from Montgomery-form limbs, least significant first, as in blst's TO_LIMB_T tables.</summary>
    internal static Fp FromMontgomery(ulong l0, ulong l1, ulong l2, ulong l3, ulong l4, ulong l5) => new(l0, l1, l2, l3, l4, l5);

    internal ulong L0 => _l0;
    internal ulong L1 => _l1;
    internal ulong L2 => _l2;
    internal ulong L3 => _l3;
    internal ulong L4 => _l4;
    internal ulong L5 => _l5;

    public static Fp Zero => default;

    public static Fp One => new(
        0x760900000002fffdUL,
        0xebf4000bc40c0002UL,
        0x5f48985753c758baUL,
        0x77ce585370525745UL,
        0x5c071a97a256ec6dUL,
        0x15f65ec3fa80e493UL);

    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_l0 | _l1 | _l2 | _l3 | _l4 | _l5) == 0;
    }

    public static Fp FromUInt64(ulong value)
    {
        Span<ulong> canonical = stackalloc ulong[6];
        canonical[0] = value;
        FromCanonical(out Fp result, canonical);
        return result;
    }

    public static bool TryFromBigEndian(ReadOnlySpan<byte> source, out Fp value)
    {
        value = default;
        if (source.Length != ByteCount)
        {
            return false;
        }

        Span<ulong> canonical = stackalloc ulong[6];
        LimbMath.ReadBigEndian(canonical, source);
        if (LimbMath.GreaterThanOrEqual(canonical, Modulus))
        {
            return false;
        }

        FromCanonical(out value, canonical);
        return true;
    }

    public static bool TryFromLittleEndian(ReadOnlySpan<byte> source, out Fp value)
    {
        value = default;
        if (source.Length != ByteCount)
        {
            return false;
        }

        Span<ulong> canonical = stackalloc ulong[6];
        LimbMath.ReadLittleEndian(canonical, source);
        if (LimbMath.GreaterThanOrEqual(canonical, Modulus))
        {
            return false;
        }

        FromCanonical(out value, canonical);
        return true;
    }

    public void ToBigEndian(Span<byte> destination)
    {
        Span<ulong> canonical = stackalloc ulong[6];
        ToCanonical(canonical);
        LimbMath.WriteBigEndian(canonical, destination);
    }

    public void ToLittleEndian(Span<byte> destination)
    {
        Span<ulong> canonical = stackalloc ulong[6];
        ToCanonical(canonical);
        LimbMath.WriteLittleEndian(canonical, destination);
    }

    public Fp Add(in Fp right)
    {
        Add(out Fp result, this, right);
        return result;
    }

    public Fp Subtract(in Fp right)
    {
        Subtract(out Fp result, this, right);
        return result;
    }

    public Fp Negate()
    {
        Negate(out Fp result, this);
        return result;
    }

    public Fp Multiply(in Fp right)
    {
        Multiply(out Fp result, this, right);
        return result;
    }

    public Fp Square()
    {
        Square(out Fp result, this);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Add(out Fp result, in Fp left, in Fp right)
    {
        // p < 2^381, so left + right < 2^382 never carries out of the top limb.
        ulong carry = 0;
        ulong r0 = AddCarry(left._l0, right._l0, ref carry);
        ulong r1 = AddCarry(left._l1, right._l1, ref carry);
        ulong r2 = AddCarry(left._l2, right._l2, ref carry);
        ulong r3 = AddCarry(left._l3, right._l3, ref carry);
        ulong r4 = AddCarry(left._l4, right._l4, ref carry);
        ulong r5 = AddCarry(left._l5, right._l5, ref carry);

        Reduce(out result, r0, r1, r2, r3, r4, r5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(out Fp result, in Fp left, in Fp right)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            SubtractArm64(out result, left, right);
            return;
        }
        ulong borrow = 0;
        ulong r0 = SubBorrow(left._l0, right._l0, ref borrow);
        ulong r1 = SubBorrow(left._l1, right._l1, ref borrow);
        ulong r2 = SubBorrow(left._l2, right._l2, ref borrow);
        ulong r3 = SubBorrow(left._l3, right._l3, ref borrow);
        ulong r4 = SubBorrow(left._l4, right._l4, ref borrow);
        ulong r5 = SubBorrow(left._l5, right._l5, ref borrow);

        ulong s0 = r0 + M0;
        ulong carry = s0 < r0 ? 1UL : 0UL;
        ulong s1 = AddCarry(r1, M1, ref carry);
        ulong s2 = AddCarry(r2, M2, ref carry);
        ulong s3 = AddCarry(r3, M3, ref carry);
        ulong s4 = AddCarry(r4, M4, ref carry);
        ulong s5 = AddCarry(r5, M5, ref carry);

        // r is in (-p,p). Adding p carries out exactly when r is
        // negative; select from these flags instead of retaining the borrow.
        ulong mask = ChoiceToMask(carry);
        result = new Fp(
            Select(s0, r0, mask),
            Select(s1, r1, mask),
            Select(s2, r2, mask),
            Select(s3, r3, mask),
            Select(s4, r4, mask),
            Select(s5, r5, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Negate(out Fp result, in Fp value)
    {
        // p - a = p + ~a + 1 modulo 2^384. Express this as an add
        // chain so constant-left subtractions cannot become NEG/ADD plus
        // per-limb comparisons before the carry-aware lowering runs.
        ulong r0 = (M0 + 1) + ~value._l0;
        ulong carry = r0 < (M0 + 1) ? 1UL : 0UL;
        ulong r1 = AddCarry(M1, ~value._l1, ref carry);
        ulong r2 = AddCarry(M2, ~value._l2, ref carry);
        ulong r3 = AddCarry(M3, ~value._l3, ref carry);
        ulong r4 = AddCarry(M4, ~value._l4, ref carry);
        ulong r5 = AddCarry(M5, ~value._l5, ref carry);

        ulong mask = ChoiceToMask(NonZeroChoice(value._l0 | value._l1 | value._l2 | value._l3 | value._l4 | value._l5));
        result = new Fp(r0 & mask, r1 & mask, r2 & mask, r3 & mask, r4 & mask, r5 & mask);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(out Fp result, in Fp left, in Fp right)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            MultiplyArm64(out result, left, right);
            return;
        }
        ulong a0 = left._l0;
        ulong a1 = left._l1;
        ulong a2 = left._l2;
        ulong a3 = left._l3;
        ulong a4 = left._l4;
        ulong a5 = left._l5;

        // Keep a reference to right's limbs so the outer loop stays small.
        // The JIT lowers Unsafe.Add with a loop index to a single lea/mov (no bounds checks).
        ref ulong bRef = ref Unsafe.As<Fp, ulong>(ref Unsafe.AsRef(in right));

        ulong t0 = 0;
        ulong t1 = 0;
        ulong t2 = 0;
        ulong t3 = 0;
        ulong t4 = 0;
        ulong t5 = 0;
        ulong t6 = 0;

        // Two rows per iteration: the twelve live words leave few spare registers, and
        // every iteration of the loop costs register shuffles at its back edge.
        for (int i = 0; i < 6; i += 2)
        {
            ulong b = Unsafe.Add(ref bRef, i);

            ulong carry = MulAdd(b, a0, t0, 0, out t0);
            carry = MulAdd(b, a1, t1, carry, out t1);
            carry = MulAdd(b, a2, t2, carry, out t2);
            carry = MulAdd(b, a3, t3, carry, out t3);
            carry = MulAdd(b, a4, t4, carry, out t4);
            carry = MulAdd(b, a5, t5, carry, out t5);
            t6 = carry;

            ulong q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            carry = MulAdd(q, M4, t4, carry, out t3);
            carry = MulAdd(q, M5, t5, carry, out t4);
            t5 = t6 + carry;

            b = Unsafe.Add(ref bRef, i + 1);

            carry = MulAdd(b, a0, t0, 0, out t0);
            carry = MulAdd(b, a1, t1, carry, out t1);
            carry = MulAdd(b, a2, t2, carry, out t2);
            carry = MulAdd(b, a3, t3, carry, out t3);
            carry = MulAdd(b, a4, t4, carry, out t4);
            carry = MulAdd(b, a5, t5, carry, out t5);
            t6 = carry;

            q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            carry = MulAdd(q, M4, t4, carry, out t3);
            carry = MulAdd(q, M5, t5, carry, out t4);
            t5 = t6 + carry;
        }

        // p < 2^381, so the Montgomery output is < 2p < 2^384 and fits in t0..t5.
        Reduce(out result, t0, t1, t2, t3, t4, t5);
    }

    // Internal tower sums may be <2p: their products still reduce below 2p
    // because 4p^2/2^384 < p/2. Do not expose these unreduced values to callers.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddUnreduced(out Fp result, in Fp a, in Fp b)
    {
        ulong carry = 0;
        ulong r0 = AddCarry(a._l0, b._l0, ref carry);
        ulong r1 = AddCarry(a._l1, b._l1, ref carry);
        ulong r2 = AddCarry(a._l2, b._l2, ref carry);
        ulong r3 = AddCarry(a._l3, b._l3, ref carry);
        ulong r4 = AddCarry(a._l4, b._l4, ref carry);
        ulong r5 = AddCarry(a._l5, b._l5, ref carry);
        result = new Fp(r0, r1, r2, r3, r4, r5);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SubtractComposite(out Fp result, in Fp a, in Fp b)
    {
        Subtract(out result, a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SubtractSumComposite(out Fp result, in Fp a, in Fp b, in Fp c)
    {
        // a-b-c lies in (-2p,p); adding 2p to negative values and reducing suffices.
        AddUnreduced(out Fp sum, b, c);
        ulong borrow = 0;
        ulong r0 = SubBorrow(a._l0, sum._l0, ref borrow);
        ulong r1 = SubBorrow(a._l1, sum._l1, ref borrow);
        ulong r2 = SubBorrow(a._l2, sum._l2, ref borrow);
        ulong r3 = SubBorrow(a._l3, sum._l3, ref borrow);
        ulong r4 = SubBorrow(a._l4, sum._l4, ref borrow);
        ulong r5 = SubBorrow(a._l5, sum._l5, ref borrow);
        // For r in (-2p,p), r + 2p carries out exactly when r is negative.
        // Select with this chain's flags rather than materializing the borrow.
        ulong s0 = r0 + 0x73fdffffffff5556UL;
        ulong carry = s0 < r0 ? 1UL : 0UL;
        ulong s1 = AddCarry(r1, 0x3d57fffd62a7ffffUL, ref carry);
        ulong s2 = AddCarry(r2, 0xce61a541ed61ec48UL, ref carry);
        ulong s3 = AddCarry(r3, 0xc8ee9709e70a257eUL, ref carry);
        ulong s4 = AddCarry(r4, 0x96374f6c869759aeUL, ref carry);
        ulong s5 = AddCarry(r5, 0x340223d472ffcd34UL, ref carry);
        ulong mask = ChoiceToMask(carry);
        r0 = Select(s0, r0, mask);
        r1 = Select(s1, r1, mask);
        r2 = Select(s2, r2, mask);
        r3 = Select(s3, r3, mask);
        r4 = Select(s4, r4, mask);
        r5 = Select(s5, r5, mask);
        Reduce(out result, r0, r1, r2, r3, r4, r5);
    }

    // Keep composite callers below the carry-folding IR budget.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyComposite(out Fp result, in Fp left, in Fp right)
    {
        Multiply(out result, left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Square(out Fp result, in Fp value)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            SquareArm64(out result, value);
            return;
        }
        if (UseExpandedKernels) SquareExpanded(out result, value);
        else Multiply(out result, value, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SquareExpanded(out Fp result, in Fp value)
    {
        ulong a0 = value._l0, a1 = value._l1, a2 = value._l2;
        ulong a3 = value._l3, a4 = value._l4, a5 = value._l5;
        ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
        ulong r6 = 0, r7 = 0, r8 = 0, r9 = 0, r10 = 0, r11 = 0;
        // Accumulate each of the fifteen off-diagonal products once.
        // Row i starts at word 2i+1 and drains into word i+6.
        ulong carry;
        carry = MulAdd(a0, a1, r1, 0, out r1);
        carry = MulAdd(a0, a2, r2, carry, out r2);
        carry = MulAdd(a0, a3, r3, carry, out r3);
        carry = MulAdd(a0, a4, r4, carry, out r4);
        carry = MulAdd(a0, a5, r5, carry, out r5);
        r6 = carry;
        carry = MulAdd(a1, a2, r3, 0, out r3);
        carry = MulAdd(a1, a3, r4, carry, out r4);
        carry = MulAdd(a1, a4, r5, carry, out r5);
        carry = MulAdd(a1, a5, r6, carry, out r6);
        r7 = carry;
        carry = MulAdd(a2, a3, r5, 0, out r5);
        carry = MulAdd(a2, a4, r6, carry, out r6);
        carry = MulAdd(a2, a5, r7, carry, out r7);
        r8 = carry;
        carry = MulAdd(a3, a4, r7, 0, out r7);
        carry = MulAdd(a3, a5, r8, carry, out r8);
        r9 = carry;
        carry = MulAdd(a4, a5, r9, 0, out r9);
        r10 = carry;

        // Double the cross terms, then add the six diagonal products.
        // Keep each diagonal high word next to its odd-word accumulation.
        carry = 0;
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
        // Cross products end at a4*a5, below bit 704 even after doubling.
        // The final diagonal addition supplies the top word and its carry.
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
        // a^2 < p^2 < pR. REDC is nonnegative and below 2p.
        // Inputs are fully read before the output, including in-place squares.
        ReduceWideCore(out Fp quotient, wide);
        Reduce(out result, quotient._l0, quotient._l1, quotient._l2, quotient._l3, quotient._l4, quotient._l5);
    }

    public bool IsOne => AreEqual(this, One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsZero(in Fp value)
    {
        return (value._l0 | value._l1 | value._l2 | value._l3 | value._l4 | value._l5) == 0;
    }

    // Elements are always fully reduced, so equal values have equal limbs.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqual(in Fp left, in Fp right)
    {
        return ((left._l0 ^ right._l0) | (left._l1 ^ right._l1) | (left._l2 ^ right._l2) |
                (left._l3 ^ right._l3) | (left._l4 ^ right._l4) | (left._l5 ^ right._l5)) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Double(out Fp result, in Fp value)
    {
        Add(out result, value, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MulBy3(out Fp result, in Fp value)
    {
        Add(out Fp twice, value, value);
        Add(out result, twice, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MulBy4(out Fp result, in Fp value)
    {
        Add(out Fp twice, value, value);
        Add(out result, twice, twice);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MulBy8(out Fp result, in Fp value)
    {
        Add(out Fp twice, value, value);
        Add(out Fp four, twice, twice);
        Add(out result, four, four);
    }

    /// <summary>value / 2: adds p to odd values (p &lt; 2^381, so nothing carries out), then shifts.</summary>
    public static void Halve(out Fp result, in Fp value)
    {
        ulong mask = 0UL - (value._l0 & 1UL);
        ulong r0 = value._l0 + (M0 & mask);
        ulong carry = r0 < value._l0 ? 1UL : 0UL;
        ulong r1 = AddCarry(value._l1, M1 & mask, ref carry);
        ulong r2 = AddCarry(value._l2, M2 & mask, ref carry);
        ulong r3 = AddCarry(value._l3, M3 & mask, ref carry);
        ulong r4 = AddCarry(value._l4, M4 & mask, ref carry);
        ulong r5 = AddCarry(value._l5, M5 & mask, ref carry);
        result = new Fp(
            (r0 >> 1) | (r1 << 63),
            (r1 >> 1) | (r2 << 63),
            (r2 >> 1) | (r3 << 63),
            (r3 >> 1) | (r4 << 63),
            (r4 >> 1) | (r5 << 63),
            r5 >> 1);
    }

    /// <summary>value^exponent for a public exponent given as little-endian limbs (variable time).</summary>
    public static void Pow(out Fp result, in Fp value, ReadOnlySpan<ulong> exponent)
    {
        // Fixed 4-bit windows: 15 precomputed powers, then 4 squarings and at most one multiply per window.
        Span<Fp> table = stackalloc Fp[16];
        table[0] = One;
        table[1] = value;
        for (int i = 2; i < 16; i++)
        {
            Multiply(out table[i], table[i - 1], value);
        }

        Fp acc = One;
        bool started = false;
        for (int limb = exponent.Length - 1; limb >= 0; limb--)
        {
            ulong word = exponent[limb];
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                if (started)
                {
                    Square(out acc, acc);
                    Square(out acc, acc);
                    Square(out acc, acc);
                    Square(out acc, acc);
                }

                int nibble = (int)((word >> shift) & 0xF);
                if (nibble != 0)
                {
                    if (started)
                    {
                        Multiply(out acc, acc, table[nibble]);
                    }
                    else
                    {
                        acc = table[nibble];
                        started = true;
                    }
                }
            }
        }

        result = acc;
    }

    private static ReadOnlySpan<ulong> PMinus3Div4 =>
    [
        0xee7fbfffffffeaaaUL, 0x07aaffffac54ffffUL, 0xd9cc34a83dac3d89UL,
        0xd91dd2e13ce144afUL, 0x92c6e9ed90d2eb35UL, 0x0680447a8e5ff9a6UL,
    ];

    /// <summary>1/value, and 0 for 0. Variable-time batched binary GCD for public inputs.</summary>
    public static void Inverse(out Fp result, in Fp value)
    {
        InverseBatched(out result, value);
    }

    /// <summary>value^((p-3)/4): 1/sqrt(value) when value is a non-zero square.</summary>
    public static void PowPMinus3Div4(out Fp result, in Fp value)
    {
        Pow(out result, value, PMinus3Div4);
    }

    /// <summary>A square root of value, if it exists (p = 3 mod 4).</summary>
    public static bool Sqrt(out Fp result, in Fp value)
    {
        PowPMinus3Div4(out Fp t, value);
        Multiply(out result, t, value);
        Square(out Fp check, result);
        return AreEqual(check, value);
    }

    /// <summary>sgn0 of RFC 9380: the parity of the canonical value.</summary>
    public static bool Sgn0(in Fp value)
    {
        Multiply(out Fp canonical, value, RawOne);
        return (canonical._l0 & 1) != 0;
    }

    public bool Equals(Fp other)
    {
        return AreEqual(this, other);
    }

    public override bool Equals(object? obj)
    {
        return obj is Fp other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_l0, _l1, _l2, _l3, _l4, _l5);
    }

    public static bool operator ==(Fp left, Fp right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Fp left, Fp right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        ToBigEndian(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // R^2 mod p: Montgomery-multiplying a canonical value by it yields the Montgomery form.
    private static readonly Fp R2 = new(
        0xf4df1f341c341746UL,
        0x0a76e6a609d104f1UL,
        0x8de5476c4c95b6d5UL,
        0x67eb88a9939d83c0UL,
        0x9a793e85b519952dUL,
        0x11988fe592cae3aaUL);

    // The integer 1: Montgomery-multiplying by it leaves the Montgomery form.
    private static readonly Fp RawOne = new(1, 0, 0, 0, 0, 0);

    private static void FromCanonical(out Fp result, ReadOnlySpan<ulong> canonical)
    {
        Multiply(out result, new Fp(canonical), R2);
    }

    private readonly void ToCanonical(Span<ulong> canonical)
    {
        Multiply(out Fp value, this, RawOne);
        value.Store(canonical);
    }

    private readonly void Store(Span<ulong> limbs)
    {
        limbs[0] = _l0;
        limbs[1] = _l1;
        limbs[2] = _l2;
        limbs[3] = _l3;
        limbs[4] = _l4;
        limbs[5] = _l5;
    }

    // Subtracts the modulus from a value in [0, 2p) unless the value is already below it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reduce(out Fp result, ulong t0, ulong t1, ulong t2, ulong t3, ulong t4, ulong t5)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            ReduceArm64(out result, t0, t1, t2, t3, t4, t5);
            return;
        }
        // Adding 2^384 - p carries out of the top limb exactly when t >= p.
        ulong s0 = t0 + NegM0;
        ulong carry = s0 < t0 ? 1UL : 0UL;
        ulong s1 = AddCarry(t1, NegM1, ref carry);
        ulong s2 = AddCarry(t2, NegM2, ref carry);
        ulong s3 = AddCarry(t3, NegM3, ref carry);
        ulong s4 = AddCarry(t4, NegM4, ref carry);
        ulong s5 = AddCarry(t5, NegM5, ref carry);

        ulong mask = ChoiceToMask(carry);
        result = new Fp(
            Select(s0, t0, mask),
            Select(s1, t1, mask),
            Select(s2, t2, mask),
            Select(s3, t3, mask),
            Select(s4, t4, mask),
            Select(s5, t5, mask));
    }

    // Returns the high word of multiplier * value + addend + carry and the low word in low; the sum
    // never overflows 128 bits. Pass the multiplier shared by a row of products first: MULX takes
    // it in RDX. The carry idioms let carry-aware JITs keep the carries in CF/OF (ADC, ADCX/ADOX).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MulAdd(ulong multiplier, ulong value, ulong addend, ulong carry, out ulong low)
    {
        ulong high = Math.BigMul(multiplier, value, out ulong productLow);
        productLow += addend;
        high += productLow < addend ? 1UL : 0UL;
        productLow += carry;
        high += productLow < carry ? 1UL : 0UL;
        low = productLow;
        return high;
    }

    // Returns left + right + carry and replaces carry (0 or 1) with the carry out.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong AddCarry(ulong left, ulong right, ref ulong carry)
    {
        ulong sum = left + right;
        ulong carry0 = sum < left ? 1UL : 0UL;
        ulong result = sum + carry;
        carry = carry0 + (result < sum ? 1UL : 0UL);
        return result;
    }

    // Returns left - right - borrow and replaces borrow (0 or 1) with the borrow out.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SubBorrow(ulong left, ulong right, ref ulong borrow)
    {
        ulong difference = left - right;
        ulong borrow0 = difference > left ? 1UL : 0UL;
        ulong result = difference - borrow;
        borrow = borrow0 + (result > difference ? 1UL : 0UL);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ChoiceToMask(ulong choice)
    {
        return 0UL - (choice & 1UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Select(ulong whenSet, ulong whenClear, ulong mask)
    {
        return whenClear ^ ((whenClear ^ whenSet) & mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong NonZeroChoice(ulong value)
    {
        return (value | (0UL - value)) >> 63;
    }

    private static ReadOnlySpan<ulong> Modulus => ModulusLimbs;
}
