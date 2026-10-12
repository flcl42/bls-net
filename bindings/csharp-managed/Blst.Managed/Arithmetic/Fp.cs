using System.Runtime.CompilerServices;

namespace Blst.Managed.Arithmetic;

/// <summary>BLS12-381 base field element stored in Montgomery form.</summary>
public readonly struct Fp : IEquatable<Fp>
{
    public const int ByteCount = 48;
    private const ulong N0 = 0x89f3fffcfffcfffdUL;
    private const ulong M0 = 0xb9feffffffffaaabUL;
    private const ulong M1 = 0x1eabfffeb153ffffUL;
    private const ulong M2 = 0x6730d2a0f6b0f624UL;
    private const ulong M3 = 0x64774b84f38512bfUL;
    private const ulong M4 = 0x4b1ba7b6434bacd7UL;
    private const ulong M5 = 0x1a0111ea397fe69aUL;

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
        ulong carry = AddCarry(left._l0, right._l0, out ulong r0);
        carry = AddCarry(left._l1, right._l1, carry, out ulong r1);
        carry = AddCarry(left._l2, right._l2, carry, out ulong r2);
        carry = AddCarry(left._l3, right._l3, carry, out ulong r3);
        carry = AddCarry(left._l4, right._l4, carry, out ulong r4);
        carry = AddCarry(left._l5, right._l5, carry, out ulong r5);

        ulong borrow = SubBorrow(r0, M0, 0, out ulong s0);
        borrow = SubBorrow(r1, M1, borrow, out ulong s1);
        borrow = SubBorrow(r2, M2, borrow, out ulong s2);
        borrow = SubBorrow(r3, M3, borrow, out ulong s3);
        borrow = SubBorrow(r4, M4, borrow, out ulong s4);
        borrow = SubBorrow(r5, M5, borrow, out ulong s5);

        ulong mask = ChoiceToMask(carry | (borrow ^ 1UL));
        result = new Fp(
            Select(s0, r0, mask),
            Select(s1, r1, mask),
            Select(s2, r2, mask),
            Select(s3, r3, mask),
            Select(s4, r4, mask),
            Select(s5, r5, mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(out Fp result, in Fp left, in Fp right)
    {
        ulong borrow = SubBorrow(left._l0, right._l0, 0, out ulong r0);
        borrow = SubBorrow(left._l1, right._l1, borrow, out ulong r1);
        borrow = SubBorrow(left._l2, right._l2, borrow, out ulong r2);
        borrow = SubBorrow(left._l3, right._l3, borrow, out ulong r3);
        borrow = SubBorrow(left._l4, right._l4, borrow, out ulong r4);
        borrow = SubBorrow(left._l5, right._l5, borrow, out ulong r5);

        ulong carry = AddCarry(r0, M0, out ulong s0);
        carry = AddCarry(r1, M1, carry, out ulong s1);
        carry = AddCarry(r2, M2, carry, out ulong s2);
        carry = AddCarry(r3, M3, carry, out ulong s3);
        carry = AddCarry(r4, M4, carry, out ulong s4);
        AddCarry(r5, M5, carry, out ulong s5);

        ulong mask = ChoiceToMask(borrow);
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
        ulong borrow = SubBorrow(M0, value._l0, 0, out ulong r0);
        borrow = SubBorrow(M1, value._l1, borrow, out ulong r1);
        borrow = SubBorrow(M2, value._l2, borrow, out ulong r2);
        borrow = SubBorrow(M3, value._l3, borrow, out ulong r3);
        borrow = SubBorrow(M4, value._l4, borrow, out ulong r4);
        SubBorrow(M5, value._l5, borrow, out ulong r5);

        ulong mask = ChoiceToMask(NonZeroChoice(value._l0 | value._l1 | value._l2 | value._l3 | value._l4 | value._l5));
        result = new Fp(r0 & mask, r1 & mask, r2 & mask, r3 & mask, r4 & mask, r5 & mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(out Fp result, in Fp left, in Fp right)
    {
        Span<ulong> l = stackalloc ulong[6];
        Span<ulong> r = stackalloc ulong[6];
        Span<ulong> o = stackalloc ulong[6];
        left.Store(l);
        right.Store(r);

        LimbMath.MontgomeryMultiply(o, l, r, Modulus, N0);
        result = new Fp(o);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Square(out Fp result, in Fp value)
    {
        Multiply(out result, value, value);
    }

    public bool Equals(Fp other)
    {
        Span<ulong> left = stackalloc ulong[6];
        Span<ulong> right = stackalloc ulong[6];
        Store(left);
        other.Store(right);
        return LimbMath.Equal(left, right);
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

    private static void FromCanonical(out Fp result, ReadOnlySpan<ulong> canonical)
    {
        Span<ulong> r2 = stackalloc ulong[6]
        {
            0xf4df1f341c341746UL,
            0x0a76e6a609d104f1UL,
            0x8de5476c4c95b6d5UL,
            0x67eb88a9939d83c0UL,
            0x9a793e85b519952dUL,
            0x11988fe592cae3aaUL,
        };
        Span<ulong> montgomery = stackalloc ulong[6];

        LimbMath.MontgomeryMultiply(montgomery, canonical, r2, Modulus, N0);
        result = new Fp(montgomery);
    }

    private readonly void ToCanonical(Span<ulong> canonical)
    {
        Span<ulong> value = stackalloc ulong[6];
        Span<ulong> one = stackalloc ulong[6];
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
        limbs[4] = _l4;
        limbs[5] = _l5;
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
