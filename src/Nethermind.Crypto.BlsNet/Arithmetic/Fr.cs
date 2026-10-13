using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>BLS12-381 scalar field element stored in Montgomery form.</summary>
public readonly struct Fr : IEquatable<Fr>
{
    public const int ByteCount = 32;
    private const ulong N0 = 0xfffffffeffffffffUL;
    private const ulong M0 = 0xffffffff00000001UL;
    private const ulong M1 = 0x53bda402fffe5bfeUL;
    private const ulong M2 = 0x3339d80809a1d805UL;
    private const ulong M3 = 0x73eda753299d7d48UL;

    // 2^256 - r.
    private const ulong NegM0 = 0x00000000ffffffffUL;
    private const ulong NegM1 = 0xac425bfd0001a401UL;
    private const ulong NegM2 = 0xccc627f7f65e27faUL;
    private const ulong NegM3 = 0x8c1258acd66282b7UL;

    private readonly ulong _l0;
    private readonly ulong _l1;
    private readonly ulong _l2;
    private readonly ulong _l3;

    private static readonly ulong[] ModulusLimbs =
    [
        0xffffffff00000001UL,
        0x53bda402fffe5bfeUL,
        0x3339d80809a1d805UL,
        0x73eda753299d7d48UL,
    ];

    private Fr(ulong l0, ulong l1, ulong l2, ulong l3)
    {
        _l0 = l0;
        _l1 = l1;
        _l2 = l2;
        _l3 = l3;
    }

    private Fr(ReadOnlySpan<ulong> limbs)
        : this(limbs[0], limbs[1], limbs[2], limbs[3])
    {
    }

    public static Fr Zero => default;

    public static Fr One => new(
        0x00000001fffffffeUL,
        0x5884b7fa00034802UL,
        0x998c4fefecbc4ff5UL,
        0x1824b159acc5056fUL);

    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_l0 | _l1 | _l2 | _l3) == 0;
    }

    public static Fr FromUInt64(ulong value)
    {
        Span<ulong> canonical = stackalloc ulong[4];
        canonical[0] = value;
        FromCanonical(out Fr result, canonical);
        return result;
    }

    public static bool TryFromBigEndian(ReadOnlySpan<byte> source, out Fr value)
    {
        value = default;
        if (source.Length != ByteCount)
        {
            return false;
        }

        Span<ulong> canonical = stackalloc ulong[4];
        LimbMath.ReadBigEndian(canonical, source);
        if (LimbMath.GreaterThanOrEqual(canonical, Modulus))
        {
            return false;
        }

        FromCanonical(out value, canonical);
        return true;
    }

    public static bool TryFromLittleEndian(ReadOnlySpan<byte> source, out Fr value)
    {
        value = default;
        if (source.Length != ByteCount)
        {
            return false;
        }

        Span<ulong> canonical = stackalloc ulong[4];
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
        Span<ulong> canonical = stackalloc ulong[4];
        ToCanonical(canonical);
        LimbMath.WriteBigEndian(canonical, destination);
    }

    public void ToLittleEndian(Span<byte> destination)
    {
        Span<ulong> canonical = stackalloc ulong[4];
        ToCanonical(canonical);
        LimbMath.WriteLittleEndian(canonical, destination);
    }

    public Fr Add(in Fr right)
    {
        Add(out Fr result, this, right);
        return result;
    }

    public Fr Subtract(in Fr right)
    {
        Subtract(out Fr result, this, right);
        return result;
    }

    public Fr Negate()
    {
        Negate(out Fr result, this);
        return result;
    }

    public Fr Multiply(in Fr right)
    {
        Multiply(out Fr result, this, right);
        return result;
    }

    public Fr Square()
    {
        Square(out Fr result, this);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Add(out Fr result, in Fr left, in Fr right)
    {
        // r < 2^255, so left + right < 2^256 never carries out of the top limb.
        ulong r0 = left._l0 + right._l0;
        ulong carry = r0 < left._l0 ? 1UL : 0UL;
        ulong r1 = AddCarry(left._l1, right._l1, ref carry);
        ulong r2 = AddCarry(left._l2, right._l2, ref carry);
        ulong r3 = AddCarry(left._l3, right._l3, ref carry);

        Reduce(out result, r0, r1, r2, r3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(out Fr result, in Fr left, in Fr right)
    {
        ulong r0 = left._l0 - right._l0;
        ulong borrow = r0 > left._l0 ? 1UL : 0UL;
        ulong r1 = SubBorrow(left._l1, right._l1, ref borrow);
        ulong r2 = SubBorrow(left._l2, right._l2, ref borrow);
        ulong r3 = SubBorrow(left._l3, right._l3, ref borrow);

        ulong s0 = r0 + M0;
        ulong carry = s0 < r0 ? 1UL : 0UL;
        ulong s1 = AddCarry(r1, M1, ref carry);
        ulong s2 = AddCarry(r2, M2, ref carry);
        ulong s3 = AddCarry(r3, M3, ref carry);

        ulong mask = ChoiceToMask(borrow);
        result = new Fr(
            Select(s0, r0, mask),
            Select(s1, r1, mask),
            Select(s2, r2, mask),
            Select(s3, r3, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Negate(out Fr result, in Fr value)
    {
        ulong r0 = M0 - value._l0;
        ulong borrow = r0 > M0 ? 1UL : 0UL;
        ulong r1 = SubBorrow(M1, value._l1, ref borrow);
        ulong r2 = SubBorrow(M2, value._l2, ref borrow);
        ulong r3 = SubBorrow(M3, value._l3, ref borrow);

        ulong mask = ChoiceToMask(NonZeroChoice(value._l0 | value._l1 | value._l2 | value._l3));
        result = new Fr(r0 & mask, r1 & mask, r2 & mask, r3 & mask);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(out Fr result, in Fr left, in Fr right)
    {
        ulong a0 = left._l0;
        ulong a1 = left._l1;
        ulong a2 = left._l2;
        ulong a3 = left._l3;
        ulong b0 = right._l0;
        ulong b1 = right._l1;
        ulong b2 = right._l2;
        ulong b3 = right._l3;

        ulong t0 = 0;
        ulong t1 = 0;
        ulong t2 = 0;
        ulong t3 = 0;
        ulong t4;

        ulong carry;
        ulong q;

        // Iteration 0 (b0).
        {
            carry = MulAdd(b0, a0, t0, 0, out t0);
            carry = MulAdd(b0, a1, t1, carry, out t1);
            carry = MulAdd(b0, a2, t2, carry, out t2);
            carry = MulAdd(b0, a3, t3, carry, out t3);
            t4 = carry;

            q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            t3 = t4 + carry;
        }

        // Iteration 1 (b1).
        {
            carry = MulAdd(b1, a0, t0, 0, out t0);
            carry = MulAdd(b1, a1, t1, carry, out t1);
            carry = MulAdd(b1, a2, t2, carry, out t2);
            carry = MulAdd(b1, a3, t3, carry, out t3);
            t4 = carry;

            q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            t3 = t4 + carry;
        }

        // Iteration 2 (b2).
        {
            carry = MulAdd(b2, a0, t0, 0, out t0);
            carry = MulAdd(b2, a1, t1, carry, out t1);
            carry = MulAdd(b2, a2, t2, carry, out t2);
            carry = MulAdd(b2, a3, t3, carry, out t3);
            t4 = carry;

            q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            t3 = t4 + carry;
        }

        // Iteration 3 (b3).
        {
            carry = MulAdd(b3, a0, t0, 0, out t0);
            carry = MulAdd(b3, a1, t1, carry, out t1);
            carry = MulAdd(b3, a2, t2, carry, out t2);
            carry = MulAdd(b3, a3, t3, carry, out t3);
            t4 = carry;

            q = t0 * N0;
            carry = MulAdd(q, M0, t0, 0, out _);
            carry = MulAdd(q, M1, t1, carry, out t0);
            carry = MulAdd(q, M2, t2, carry, out t1);
            carry = MulAdd(q, M3, t3, carry, out t2);
            t3 = t4 + carry;
        }

        // r < 2^255, so the Montgomery output is < 2r < 2^256 and fits in t0..t3.
        Reduce(out result, t0, t1, t2, t3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Square(out Fr result, in Fr value)
    {
        Multiply(out result, value, value);
    }

    public bool Equals(Fr other)
    {
        Span<ulong> left = stackalloc ulong[4];
        Span<ulong> right = stackalloc ulong[4];
        Store(left);
        other.Store(right);
        return LimbMath.Equal(left, right);
    }

    public override bool Equals(object? obj)
    {
        return obj is Fr other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_l0, _l1, _l2, _l3);
    }

    public static bool operator ==(Fr left, Fr right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Fr left, Fr right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        ToBigEndian(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void FromCanonical(out Fr result, ReadOnlySpan<ulong> canonical)
    {
        Span<ulong> r2 = stackalloc ulong[4]
        {
            0xc999e990f3f29c6dUL,
            0x2b6cedcb87925c23UL,
            0x05d314967254398fUL,
            0x0748d9d99f59ff11UL,
        };
        Span<ulong> montgomery = stackalloc ulong[4];

        LimbMath.MontgomeryMultiply(montgomery, canonical, r2, Modulus, N0);
        result = new Fr(montgomery);
    }

    private readonly void ToCanonical(Span<ulong> canonical)
    {
        Span<ulong> value = stackalloc ulong[4];
        Span<ulong> one = stackalloc ulong[4];
        one[0] = 1;
        Store(value);

        LimbMath.MontgomeryMultiply(canonical, value, one, Modulus, N0);
    }

    private readonly void Store(Span<ulong> limbs)
    {
        limbs[0] = _l0;
        limbs[1] = _l1;
        limbs[2] = _l2;
        limbs[3] = _l3;
    }

    // Subtracts the modulus from a value in [0, 2r) unless the value is already below it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reduce(out Fr result, ulong t0, ulong t1, ulong t2, ulong t3)
    {
        // Adding 2^256 - r carries out of the top limb exactly when t >= r.
        ulong s0 = t0 + NegM0;
        ulong carry = s0 < t0 ? 1UL : 0UL;
        ulong s1 = AddCarry(t1, NegM1, ref carry);
        ulong s2 = AddCarry(t2, NegM2, ref carry);
        ulong s3 = AddCarry(t3, NegM3, ref carry);

        ulong mask = ChoiceToMask(carry);
        result = new Fr(
            Select(s0, t0, mask),
            Select(s1, t1, mask),
            Select(s2, t2, mask),
            Select(s3, t3, mask));
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
