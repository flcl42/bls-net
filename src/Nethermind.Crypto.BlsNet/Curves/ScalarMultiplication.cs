using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>Variable-time GLV/GLS multiplication of subgroup points.</summary>
public static class ScalarMultiplication
{
    private const ulong AbsZ = 0xd201000000010000;
    private static readonly UInt128 ZZ = (UInt128)AbsZ * AbsZ;

    /// <summary>Multiplies a G1 subgroup point by an arbitrary 256-bit little-endian scalar; variable-time.</summary>
    public static void Multiply(in G1Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G1Projective r)
    {
        Span<UInt128> digits = stackalloc UInt128[2];
        SplitG1Scalar(scalarLittleEndian32, digits);
        Jacobian<Fp>.FromAffine(out Jacobian<Fp> basis, p.X, p.Y);
        Wnaf<Fp>.Multiply(basis, digits, out r.Value);
    }

    /// <summary>Multiplies a G2 subgroup point by an arbitrary 256-bit little-endian scalar; variable-time.</summary>
    public static void Multiply(in G2Affine p, ReadOnlySpan<byte> scalarLittleEndian32, out G2Projective r)
    {
        Span<ulong> split = stackalloc ulong[4];
        SplitG2Scalar(scalarLittleEndian32, split);
        Span<UInt128> digits = stackalloc UInt128[4];
        for (int j = 0; j < 4; j++) digits[j] = split[j];
        Jacobian<Fp2>.FromAffine(out Jacobian<Fp2> basis, p.X, p.Y);
        Wnaf<Fp2>.Multiply(basis, digits, out r.Value);
    }

    internal static void SplitG2Scalar(ReadOnlySpan<byte> scalar, Span<ulong> digits)
    {
        Span<ulong> words = stackalloc ulong[4];
        ReadReduced(scalar, words);
        for (int j = 0; j < 4; j++)
        {
            UInt128 carry = 0;
            for (int i = 3; i >= 0; i--)
            {
                UInt128 v = (carry << 64) | words[i];
                words[i] = (ulong)(v / AbsZ);
                carry = v - (UInt128)words[i] * AbsZ;
            }
            digits[j] = (ulong)carry;
        }
    }

    internal static void SplitG1Scalar(ReadOnlySpan<byte> scalar, Span<UInt128> digits)
    {
        Span<ulong> words = stackalloc ulong[4];
        ReadReduced(scalar, words);
        UInt128 rem = 0, quotient = 0;
        for (int i = 255; i >= 0; i--)
        {
            bool carry = (rem >> 127) != 0;
            rem = (rem << 1) | ((words[i >> 6] >> (i & 63)) & 1);
            if (carry || rem >= ZZ) { rem -= ZZ; if (i < 128) quotient |= (UInt128)1 << i; }
        }
        digits[0] = rem;
        digits[1] = quotient;
    }

    private static void ReadReduced(ReadOnlySpan<byte> scalar, Span<ulong> words)
    {
        if (scalar.Length != 32) throw new ArgumentException("A scalar must contain exactly 32 bytes.", nameof(scalar));
        for (int i = 0; i < 4; i++) words[i] = BinaryPrimitives.ReadUInt64LittleEndian(scalar.Slice(i * 8, 8));
        ReadOnlySpan<ulong> modulus = [0xffffffff00000001, 0x53bda402fffe5bfe, 0x3339d80809a1d805, 0x73eda753299d7d48];
        while (Compare(words, modulus) >= 0)
        {
            ulong borrow = 0;
            for (int i = 0; i < 4; i++)
            {
                UInt128 sub = (UInt128)modulus[i] + borrow;
                borrow = (UInt128)words[i] < sub ? 1UL : 0;
                words[i] = (ulong)((UInt128)words[i] - sub);
            }
        }
    }

    private static int Compare(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
    { for (int i = 3; i >= 0; i--) { if (a[i] > b[i]) return 1; if (a[i] < b[i]) return -1; } return 0; }
}

internal static class Wnaf<T> where T : unmanaged, IField<T>
{
    internal static void Multiply(in Jacobian<T> basis, Span<UInt128> scalars, out Jacobian<T> r)
    {
        Span<sbyte> naf = stackalloc sbyte[4 * 130]; naf.Clear(); int top = 0;
        for (int j = 0; j < scalars.Length; j++)
        {
            UInt128 s = scalars[j]; int bit = 0;
            while (s != 0)
            {
                if ((s & 1) != 0)
                {
                    int digit = (int)(s & 31); if (digit > 16) digit -= 32;
                    naf[j * 130 + bit] = (sbyte)digit;
                    if (digit > 0) s -= (uint)digit; else s += (uint)-digit;
                }
                s >>= 1; bit++;
            }
            top = Math.Max(top, bit);
        }
        if (top == 0) { r = default; return; }
        Span<Jacobian<T>> table = stackalloc Jacobian<T>[scalars.Length * 8];
        Span<T> products = stackalloc T[8];
        table[0] = basis;
        Jacobian<T>.Double(out Jacobian<T> twice, basis);
        for (int i = 1; i < 8; i++) Jacobian<T>.Add(out table[i], table[i - 1], twice);
        Jacobian<T>.BatchToAffine(table[..8], products);
        if (typeof(T) == typeof(Fp2))
        {
            // The GLS bases are successive images under -psi. Applying the
            // endomorphism to the normalized odd multiples saves three tables
            // of projective additions and their normalization products.
            Span<Jacobian<Fp2>> g2Table = MemoryMarshal.Cast<Jacobian<T>, Jacobian<Fp2>>(table);
            for (int j = 1; j < scalars.Length; j++)
                for (int i = 0; i < 8; i++)
                {
                    G2Projective.Psi(out g2Table[j * 8 + i], g2Table[(j - 1) * 8 + i]);
                    Jacobian<Fp2>.Negate(out g2Table[j * 8 + i], g2Table[j * 8 + i]);
                }
        }
        else
        {
            Span<Jacobian<Fp>> g1Table = MemoryMarshal.Cast<Jacobian<T>, Jacobian<Fp>>(table);
            for (int i = 0; i < 8; i++) G1Msm.Endomorphism(out g1Table[8 + i], g1Table[i]);
        }
        r = default;
        for (int bit = top - 1; bit >= 0; bit--)
        {
            Jacobian<T>.Double(out r, r);
            for (int j = 0; j < scalars.Length; j++)
            {
                int digit = naf[j * 130 + bit]; if (digit == 0) continue;
                ref Jacobian<T> p = ref table[j * 8 + ((Math.Abs(digit) - 1) >> 1)];
                T y = p.Y; if (digit < 0) T.Negate(out y, y);
                Jacobian<T>.AddAffine(out r, r, p.X, y);
            }
        }
    }
}
