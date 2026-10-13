using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class LimbMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(ReadOnlySpan<ulong> value)
    {
        ulong acc = 0;
        for (int i = 0; i < value.Length; i++)
        {
            acc |= value[i];
        }

        return acc == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equal(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        ulong acc = 0;
        for (int i = 0; i < left.Length; i++)
        {
            acc |= left[i] ^ right[i];
        }

        return acc == 0;
    }

    public static bool GreaterThanOrEqual(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        for (int i = left.Length - 1; i >= 0; i--)
        {
            ulong l = left[i];
            ulong r = right[i];
            if (l > r)
            {
                return true;
            }

            if (l < r)
            {
                return false;
            }
        }

        return true;
    }

    public static void AddMod(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, ReadOnlySpan<ulong> modulus)
    {
        ulong carry = 0;
        for (int i = 0; i < result.Length; i++)
        {
            UInt128 sum = (UInt128)left[i] + right[i] + carry;
            result[i] = (ulong)sum;
            carry = (ulong)(sum >> 64);
        }

        Span<ulong> reduced = stackalloc ulong[result.Length];
        ulong borrow = Subtract(result, modulus, reduced);
        ulong useReduced = carry | (borrow ^ 1UL);
        Select(result, reduced, result, ChoiceToMask(useReduced));
    }

    public static void SubMod(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, ReadOnlySpan<ulong> modulus)
    {
        ulong borrow = Subtract(left, right, result);
        Span<ulong> corrected = stackalloc ulong[result.Length];
        Copy(result, corrected);
        AddInPlace(corrected, modulus);

        Select(result, corrected, result, ChoiceToMask(borrow));
    }

    public static void NegMod(Span<ulong> result, ReadOnlySpan<ulong> value, ReadOnlySpan<ulong> modulus)
    {
        Subtract(modulus, value, result);
        ulong nonZero = NonZeroChoice(value);
        ulong mask = ChoiceToMask(nonZero);
        for (int i = 0; i < result.Length; i++)
        {
            result[i] &= mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MontgomeryMultiply(
        Span<ulong> result,
        ReadOnlySpan<ulong> left,
        ReadOnlySpan<ulong> right,
        ReadOnlySpan<ulong> modulus,
        ulong montgomeryInv)
    {
        int n = modulus.Length;
        Span<ulong> t = stackalloc ulong[n + 1];
        t.Clear();

        for (int i = 0; i < n; i++)
        {
            ulong carry = 0;
            for (int j = 0; j < n; j++)
            {
                carry = MulAdd(t[j], left[j], right[i], carry, out t[j]);
            }

            t[n] = carry;

            ulong q = t[0] * montgomeryInv;
            carry = MulAdd(t[0], q, modulus[0], 0, out _);

            for (int j = 1; j < n; j++)
            {
                carry = MulAdd(t[j], q, modulus[j], carry, out t[j - 1]);
            }

            t[n] = AddCarry(t[n], carry, out t[n - 1]);
        }

        t[..n].CopyTo(result);

        Span<ulong> reduced = stackalloc ulong[n];
        ulong borrow = Subtract(result, modulus, reduced);
        ulong useReduced = NonZeroChoice(t[n]) | (borrow ^ 1UL);
        Select(result, reduced, result, ChoiceToMask(useReduced));
    }

    public static void ReadBigEndian(Span<ulong> limbs, ReadOnlySpan<byte> source)
    {
        int width = limbs.Length * sizeof(ulong);
        if (source.Length != width)
        {
            throw new ArgumentException($"Expected {width} bytes.", nameof(source));
        }

        for (int i = 0; i < limbs.Length; i++)
        {
            int offset = source.Length - ((i + 1) * sizeof(ulong));
            limbs[i] = BinaryPrimitives.ReadUInt64BigEndian(source[offset..(offset + sizeof(ulong))]);
        }
    }

    public static void ReadLittleEndian(Span<ulong> limbs, ReadOnlySpan<byte> source)
    {
        int width = limbs.Length * sizeof(ulong);
        if (source.Length != width)
        {
            throw new ArgumentException($"Expected {width} bytes.", nameof(source));
        }

        for (int i = 0; i < limbs.Length; i++)
        {
            int offset = i * sizeof(ulong);
            limbs[i] = BinaryPrimitives.ReadUInt64LittleEndian(source[offset..(offset + sizeof(ulong))]);
        }
    }

    public static void WriteBigEndian(ReadOnlySpan<ulong> limbs, Span<byte> destination)
    {
        int width = limbs.Length * sizeof(ulong);
        if (destination.Length != width)
        {
            throw new ArgumentException($"Expected {width} bytes.", nameof(destination));
        }

        for (int i = 0; i < limbs.Length; i++)
        {
            int offset = destination.Length - ((i + 1) * sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(destination[offset..(offset + sizeof(ulong))], limbs[i]);
        }
    }

    public static void WriteLittleEndian(ReadOnlySpan<ulong> limbs, Span<byte> destination)
    {
        int width = limbs.Length * sizeof(ulong);
        if (destination.Length != width)
        {
            throw new ArgumentException($"Expected {width} bytes.", nameof(destination));
        }

        for (int i = 0; i < limbs.Length; i++)
        {
            int offset = i * sizeof(ulong);
            BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..(offset + sizeof(ulong))], limbs[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(ReadOnlySpan<ulong> source, Span<ulong> destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void Select(Span<ulong> result, ReadOnlySpan<ulong> whenSet, ReadOnlySpan<ulong> whenClear, ulong mask)
    {
        ulong inverse = ~mask;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (whenSet[i] & mask) | (whenClear[i] & inverse);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ChoiceToMask(ulong choice)
    {
        return 0UL - (choice & 1UL);
    }

    private static ulong NonZeroChoice(ReadOnlySpan<ulong> value)
    {
        ulong acc = 0;
        for (int i = 0; i < value.Length; i++)
        {
            acc |= value[i];
        }

        return (acc | (0UL - acc)) >> 63;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong NonZeroChoice(ulong value)
    {
        return (value | (0UL - value)) >> 63;
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
    private static ulong AddCarry(ulong left, ulong right, out ulong low)
    {
        low = left + right;
        return low < left ? 1UL : 0UL;
    }

    private static ulong AddInPlace(Span<ulong> left, ReadOnlySpan<ulong> right)
    {
        ulong carry = 0;
        for (int i = 0; i < left.Length; i++)
        {
            UInt128 sum = (UInt128)left[i] + right[i] + carry;
            left[i] = (ulong)sum;
            carry = (ulong)(sum >> 64);
        }

        return carry;
    }

    private static ulong Subtract(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> result)
    {
        ulong borrow = 0;
        for (int i = 0; i < left.Length; i++)
        {
            ulong subtrahend = right[i] + borrow;
            bool subtrahendCarry = subtrahend < right[i];
            ulong current = left[i];

            result[i] = current - subtrahend;
            borrow = (current < subtrahend || subtrahendCarry) ? 1UL : 0UL;
        }

        return borrow;
    }
}
