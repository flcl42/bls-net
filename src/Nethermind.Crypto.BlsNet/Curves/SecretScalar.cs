using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

internal static class SecretScalar
{
    private const ulong AbsZ = 0xd201000000010000;
    private static readonly UInt128 ZZ = (UInt128)AbsZ * AbsZ;

    internal static void Split(ReadOnlySpan<byte> scalar, Span<UInt128> digits, bool g2)
    {
        Fr reduced = Fr.FromLittleEndianConstantTime(scalar);
        Span<byte> bytes = stackalloc byte[32]; reduced.ToLittleEndianConstantTime(bytes);
        Span<ulong> words = stackalloc ulong[4], quotient = stackalloc ulong[4];
        for (int i = 0; i < 4; i++) words[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(i * 8)..]);
        if (g2)
        {
            for (int j = 0; j < 4; j++)
            {
                Divide(words, AbsZ, 0, quotient, out ulong low, out _);
                digits[j] = low; quotient.CopyTo(words);
            }
        }
        else
        {
            Divide(words, (ulong)ZZ, (ulong)(ZZ >> 64), quotient, out ulong low, out ulong high);
            digits[0] = ((UInt128)high << 64) | low;
            digits[1] = ((UInt128)quotient[1] << 64) | quotient[0];
        }
        CryptographicOperations.ZeroMemory(bytes);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(quotient));
    }

    // Fixed 256-step binary division, replacing blst's assembly reciprocal division.
    // No DIV/IDIV or UInt128 comparison; the remainder needs an explicit 129th bit.
    private static void Divide(ReadOnlySpan<ulong> words, ulong divisorLow, ulong divisorHigh,
        Span<ulong> quotient, out ulong low, out ulong high)
    {
        quotient.Clear(); low = high = 0;
        for (int bit = 255; bit >= 0; bit--)
        {
            ulong top = high >> 63;
            high = (high << 1) | (low >> 63);
            low = (low << 1) | ((words[bit >> 6] >> (bit & 63)) & 1);
            ulong borrow = 0;
            ulong dl = ConstantTime.Subtract(low, divisorLow, ref borrow);
            ulong dh = ConstantTime.Subtract(high, divisorHigh, ref borrow);
            ulong take = top | (borrow ^ 1), mask = 0UL - take;
            low = ConstantTime.Select(dl, low, mask); high = ConstantTime.Select(dh, high, mask);
            quotient[bit >> 6] |= take << (bit & 63);
        }
    }
}

// Width-five Booth recoding, full table scans and endomorphism tables, following blst ec_mult.h.
internal static class SecretMultiply<T> where T : unmanaged, IField<T>
{
    internal static void Multiply(in Jacobian<T> basis, ReadOnlySpan<byte> scalar, out Jacobian<T> r)
    {
        bool g2 = typeof(T) == typeof(Fp2);
        int components = g2 ? 4 : 2, bits = g2 ? 64 : 128;
        Span<UInt128> digits = stackalloc UInt128[components];
        SecretScalar.Split(scalar, digits, g2);
        Span<Jacobian<T>> table = stackalloc Jacobian<T>[components * 16];
        table[0] = basis; Jacobian<T>.Double(out table[1], basis);
        for (int i = 2, j = 1; i < 16; i += 2, j++)
        {
            Jacobian<T>.AddConstantTime(out table[i], table[j], table[j - 1]);
            Jacobian<T>.Double(out table[i + 1], table[j]);
        }
        if (g2)
        {
            Span<Jacobian<Fp2>> points = MemoryMarshal.Cast<Jacobian<T>, Jacobian<Fp2>>(table);
            for (int j = 1; j < components; j++) for (int i = 0; i < 16; i++)
            {
                G2Projective.Psi(out points[j * 16 + i], points[(j - 1) * 16 + i]);
                Jacobian<Fp2>.Negate(out points[j * 16 + i], points[j * 16 + i]);
            }
        }
        else
        {
            Span<Jacobian<Fp>> points = MemoryMarshal.Cast<Jacobian<T>, Jacobian<Fp>>(table);
            for (int i = 0; i < 16; i++) G1Msm.Endomorphism(out points[16 + i], points[i]);
        }
        Jacobian<T> acc = default;
        for (int offset = bits / 5 * 5; offset >= 0; offset -= 5)
        {
            for (int i = 0; i < 5; i++) Jacobian<T>.Double(out acc, acc);
            for (int j = 0; j < components; j++)
            {
                // The public offset chooses the shift; the secret digit never chooses an address.
                ulong window = offset == 0 ? (ulong)digits[j] << 1 : (ulong)(digits[j] >> (offset - 1));
                window &= 63;
                ulong sign = 0UL - (window >> 5);
                ulong d = (window + 1) >> 1;
                ulong magnitude = ConstantTime.Select(32 - d, d, sign);
                Jacobian<T> selected = default;
                for (int k = 1; k <= 16; k++)
                    Jacobian<T>.Select(out selected, table[j * 16 + k - 1], selected, ConstantTime.ZeroMask((ulong)k ^ magnitude));
                Jacobian<T>.ConditionalNegate(out selected, selected, sign);
                Jacobian<T>.AddConstantTime(out acc, acc, selected);
            }
        }
        r = acc;
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(digits));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(table));
    }
}
