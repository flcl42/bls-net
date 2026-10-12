using System.Runtime.CompilerServices;

namespace Blst.Managed.Arithmetic;

/// <summary>BLS12-381 scalar field element stored in Montgomery form.</summary>
public readonly struct Fr : IEquatable<Fr>
{
    public const int ByteCount = 32;
    private const ulong N0 = 0xfffffffeffffffffUL;
    private const ulong M0 = 0xffffffff00000001UL;
    private const ulong M1 = 0x53bda402fffe5bfeUL;
    private const ulong M2 = 0x3339d80809a1d805UL;
    private const ulong M3 = 0x73eda753299d7d48UL;

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
        ulong carry = AddCarry(left._l0, right._l0, out ulong r0);
        carry = AddCarry(left._l1, right._l1, carry, out ulong r1);
        carry = AddCarry(left._l2, right._l2, carry, out ulong r2);
        carry = AddCarry(left._l3, right._l3, carry, out ulong r3);

        ulong borrow = SubBorrow(r0, M0, 0, out ulong s0);
        borrow = SubBorrow(r1, M1, borrow, out ulong s1);
        borrow = SubBorrow(r2, M2, borrow, out ulong s2);
        borrow = SubBorrow(r3, M3, borrow, out ulong s3);

        ulong mask = ChoiceToMask(carry | (borrow ^ 1UL));
        result = new Fr(
            Select(s0, r0, mask),
            Select(s1, r1, mask),
            Select(s2, r2, mask),
            Select(s3, r3, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(out Fr result, in Fr left, in Fr right)
    {
        ulong borrow = SubBorrow(left._l0, right._l0, 0, out ulong r0);
        borrow = SubBorrow(left._l1, right._l1, borrow, out ulong r1);
        borrow = SubBorrow(left._l2, right._l2, borrow, out ulong r2);
        borrow = SubBorrow(left._l3, right._l3, borrow, out ulong r3);

        ulong carry = AddCarry(r0, M0, out ulong s0);
        carry = AddCarry(r1, M1, carry, out ulong s1);
        carry = AddCarry(r2, M2, carry, out ulong s2);
        AddCarry(r3, M3, carry, out ulong s3);

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
        ulong borrow = SubBorrow(M0, value._l0, 0, out ulong r0);
        borrow = SubBorrow(M1, value._l1, borrow, out ulong r1);
        borrow = SubBorrow(M2, value._l2, borrow, out ulong r2);
        SubBorrow(M3, value._l3, borrow, out ulong r3);

        ulong mask = ChoiceToMask(NonZeroChoice(value._l0 | value._l1 | value._l2 | value._l3));
        result = new Fr(r0 & mask, r1 & mask, r2 & mask, r3 & mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(out Fr result, in Fr left, in Fr right)
    {
        Span<ulong> t = stackalloc ulong[5];
        t.Clear();

        MontgomeryStep(t, left, right._l0);
        MontgomeryStep(t, left, right._l1);
        MontgomeryStep(t, left, right._l2);
        MontgomeryStep(t, left, right._l3);

        ulong borrow = SubBorrow(t[0], M0, 0, out ulong s0);
        borrow = SubBorrow(t[1], M1, borrow, out ulong s1);
        borrow = SubBorrow(t[2], M2, borrow, out ulong s2);
        borrow = SubBorrow(t[3], M3, borrow, out ulong s3);

        ulong mask = ChoiceToMask(NonZeroChoice(t[4]) | (borrow ^ 1UL));
        result = new Fr(
            Select(s0, t[0], mask),
            Select(s1, t[1], mask),
            Select(s2, t[2], mask),
            Select(s3, t[3], mask));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MontgomeryStep(Span<ulong> t, in Fr left, ulong right)
    {
        ulong carry = MulAdd(t[0], left._l0, right, 0, out t[0]);
        carry = MulAdd(t[1], left._l1, right, carry, out t[1]);
        carry = MulAdd(t[2], left._l2, right, carry, out t[2]);
        carry = MulAdd(t[3], left._l3, right, carry, out t[3]);
        t[4] = carry;

        ulong q = t[0] * N0;
        carry = MulAdd(t[0], q, M0, 0, out _);
        carry = MulAdd(t[1], q, M1, carry, out t[0]);
        carry = MulAdd(t[2], q, M2, carry, out t[1]);
        carry = MulAdd(t[3], q, M3, carry, out t[2]);
        t[4] = AddCarry(t[4], carry, out t[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MulAdd(ulong addend, ulong left, ulong right, ulong carry, out ulong low)
    {
        ulong high = Math.BigMul(left, right, out ulong productLow);
        ulong sum = productLow + addend;
        high += sum < productLow ? 1UL : 0UL;
        low = sum + carry;
        high += low < sum ? 1UL : 0UL;
        return high;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong AddCarry(ulong left, ulong right, out ulong result)
    {
        result = left + right;
        return result < left ? 1UL : 0UL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong AddCarry(ulong left, ulong right, ulong carry, out ulong result)
    {
        ulong sum = left + right;
        ulong carry0 = sum < left ? 1UL : 0UL;
        result = sum + carry;
        return carry0 | (result < sum ? 1UL : 0UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SubBorrow(ulong left, ulong right, ulong borrow, out ulong result)
    {
        ulong subtrahend = right + borrow;
        ulong borrow0 = subtrahend < right ? 1UL : 0UL;
        result = left - subtrahend;
        return borrow0 | (left < subtrahend ? 1UL : 0UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ChoiceToMask(ulong choice)
    {
        return 0UL - (choice & 1UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Select(ulong whenSet, ulong whenClear, ulong mask)
    {
        return (whenSet & mask) | (whenClear & ~mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong NonZeroChoice(ulong value)
    {
        return (value | (0UL - value)) >> 63;
    }

    private static ReadOnlySpan<ulong> Modulus => ModulusLimbs;
}
