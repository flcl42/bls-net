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
        // p < 2^381, so left + right < 2^382 never carries out of the top limb.
        ulong r0 = left._l0 + right._l0;
        ulong carry = r0 < left._l0 ? 1UL : 0UL;
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
        ulong r0 = left._l0 - right._l0;
        ulong borrow = r0 > left._l0 ? 1UL : 0UL;
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
        ulong r0 = M0 - value._l0;
        ulong borrow = r0 > M0 ? 1UL : 0UL;
        ulong r1 = SubBorrow(M1, value._l1, ref borrow);
        ulong r2 = SubBorrow(M2, value._l2, ref borrow);
        ulong r3 = SubBorrow(M3, value._l3, ref borrow);
        ulong r4 = SubBorrow(M4, value._l4, ref borrow);
        ulong r5 = SubBorrow(M5, value._l5, ref borrow);

        ulong mask = ChoiceToMask(NonZeroChoice(value._l0 | value._l1 | value._l2 | value._l3 | value._l4 | value._l5));
        result = new Fp(r0 & mask, r1 & mask, r2 & mask, r3 & mask, r4 & mask, r5 & mask);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(out Fp result, in Fp left, in Fp right)
    {
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

        for (int i = 0; i < 6; i++)
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
        }

        // p < 2^381, so the Montgomery output is < 2p < 2^384 and fits in t0..t5.
        Reduce(out result, t0, t1, t2, t3, t4, t5);
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

    // Subtracts the modulus from a value in [0, 2p) unless the value is already below it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reduce(out Fp result, ulong t0, ulong t1, ulong t2, ulong t3, ulong t4, ulong t5)
    {
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
