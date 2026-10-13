using System.Runtime.CompilerServices;

// Tower formulas and constants adapted from blst fp12_tower.c.
// Copyright Supranational LLC; licensed under Apache-2.0.

namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>Fp12 = Fp6[w] / (w^2 - v), in blst's memory layout.</summary>
public struct Fp12 : IEquatable<Fp12>
{
    public Fp6 C0;
    public Fp6 C1;

    public static Fp12 Zero => default;
    public static Fp12 One => new() { C0 = Fp6.One };
    public readonly bool IsZero => EqualsZero(this);
    public readonly bool IsOne => AreEqual(this, One);
    public static bool EqualsZero(in Fp12 a) => Fp6.EqualsZero(a.C0) && Fp6.EqualsZero(a.C1);
    public static bool AreEqual(in Fp12 a, in Fp12 b) => Fp6.AreEqual(a.C0, b.C0) && Fp6.AreEqual(a.C1, b.C1);
    public readonly bool Equals(Fp12 other) => AreEqual(this, other);
    public override readonly bool Equals(object? obj) => obj is Fp12 other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(C0, C1);

    public static bool operator ==(Fp12 left, Fp12 right) => left.Equals(right);
    public static bool operator !=(Fp12 left, Fp12 right) => !left.Equals(right);

    /// <summary>Multiply in Fp12; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Multiply(out Fp12 result, in Fp12 a, in Fp12 b)
    {
        WideTower.Multiply12(out result, a, b);
    }

    /// <summary>Square in Fp12; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Square(out Fp12 result, in Fp12 a)
    {
        Unsafe.SkipInit(out Fp6 t0);
        Unsafe.SkipInit(out Fp6 t1);
        Fp6.Add(out t0, a.C0, a.C1);
        Fp2.MulByNonResidue(out t1.C2, a.C1.C2);
        Fp2.Add(out t1.C0, a.C0.C0, t1.C2);
        Fp2.Add(out t1.C1, a.C0.C1, a.C1.C0);
        Fp2.Add(out t1.C2, a.C0.C2, a.C1.C1);
        Fp6.Multiply(out t0, t0, t1);
        Fp6.Multiply(out t1, a.C0, a.C1);
        Fp6.Add(out result.C1, t1, t1);
        Fp6.Subtract(out result.C0, t0, t1);
        Fp2.MulByNonResidue(out t1.C2, t1.C2);
        Fp2Fused.Subtract(out result.C0.C0, result.C0.C0, t1.C2);
        Fp2Fused.Subtract(out result.C0.C1, result.C0.C1, t1.C0);
        Fp2Fused.Subtract(out result.C0.C2, result.C0.C2, t1.C1);
    }

    /// <summary>Variable-time inverse in Fp12; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Inverse(out Fp12 result, in Fp12 a)
    {
        Unsafe.SkipInit(out Fp6 t0);
        Unsafe.SkipInit(out Fp6 t1);
        Fp6.Square(out t0, a.C0);
        Fp6.Square(out t1, a.C1);
        Fp2.MulByNonResidue(out t1.C2, t1.C2);
        Fp2Fused.Subtract(out t0.C0, t0.C0, t1.C2);
        Fp2Fused.Subtract(out t0.C1, t0.C1, t1.C0);
        Fp2Fused.Subtract(out t0.C2, t0.C2, t1.C1);
        Fp6.Inverse(out t1, t0);
        Fp6.Multiply(out result.C0, a.C0, t1);
        Fp6.Multiply(out result.C1, a.C1, t1);
        Fp6.Negate(out result.C1, result.C1);
    }

    /// <summary>MultiplyBySparse in Fp12; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void MultiplyBySparse(out Fp12 result, in Fp12 a, in Fp6 xy00z0)
    {
        WideTower.MultiplyBySparse12(out result, a, xy00z0);
    }

    /// <summary>Cyclotomic square; equals Square on the cyclotomic subgroup. Inputs may alias the result.</summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CyclotomicSquare(out Fp12 result, in Fp12 a)
    {
        Unsafe.SkipInit(out Fp4 t0);
        Unsafe.SkipInit(out Fp4 t1);
        Unsafe.SkipInit(out Fp4 t2);
        SquareFp4(out t0, a.C0.C0, a.C1.C1);
        SquareFp4(out t1, a.C1.C0, a.C0.C2);
        SquareFp4(out t2, a.C0.C1, a.C1.C2);
        Fp2Fused.Subtract(out result.C0.C0, t0.C0, a.C0.C0);
        Fp2.Add(out result.C0.C0, result.C0.C0, result.C0.C0);
        Fp2.Add(out result.C0.C0, result.C0.C0, t0.C0);
        Fp2Fused.Subtract(out result.C0.C1, t1.C0, a.C0.C1);
        Fp2.Add(out result.C0.C1, result.C0.C1, result.C0.C1);
        Fp2.Add(out result.C0.C1, result.C0.C1, t1.C0);
        Fp2Fused.Subtract(out result.C0.C2, t2.C0, a.C0.C2);
        Fp2.Add(out result.C0.C2, result.C0.C2, result.C0.C2);
        Fp2.Add(out result.C0.C2, result.C0.C2, t2.C0);
        Fp2.MulByNonResidue(out t2.C1, t2.C1);
        Fp2.Add(out result.C1.C0, t2.C1, a.C1.C0);
        Fp2.Add(out result.C1.C0, result.C1.C0, result.C1.C0);
        Fp2.Add(out result.C1.C0, result.C1.C0, t2.C1);
        Fp2.Add(out result.C1.C1, t0.C1, a.C1.C1);
        Fp2.Add(out result.C1.C1, result.C1.C1, result.C1.C1);
        Fp2.Add(out result.C1.C1, result.C1.C1, t0.C1);
        Fp2.Add(out result.C1.C2, t1.C1, a.C1.C2);
        Fp2.Add(out result.C1.C2, result.C1.C2, result.C1.C2);
        Fp2.Add(out result.C1.C2, result.C1.C2, t1.C1);
    }

    private struct Fp4 { public Fp2 C0, C1; }
    [SkipLocalsInit]
    private static void SquareFp4(out Fp4 result, in Fp2 a0, in Fp2 a1)
    {
        Unsafe.SkipInit(out Fp4 ret);
        Unsafe.SkipInit(out Fp2 t0);
        Unsafe.SkipInit(out Fp2 t1);
        Fp2.Square(out t0, a0);
        Fp2.Square(out t1, a1);
        Fp2.Add(out ret.C1, a0, a1);
        Fp2.MulByNonResidue(out ret.C0, t1);
        Fp2.Add(out ret.C0, ret.C0, t0);
        Fp2.Square(out ret.C1, ret.C1);
        Fp2Fused.SubtractSum(out ret.C1, ret.C1, t0, t1);
        result = ret;
    }

    // Karabina, ePrint 2010/542, Theorems 3.1 and 3.2. The two omitted
    // coefficients do not feed these four coordinates during squaring.
    // Only cyclotomic inputs (or zero) are valid for this internal operation.
    [SkipLocalsInit]
    internal static void CyclotomicSquareRepeated(out Fp12 result, in Fp12 a, int count)
    {
        if (count < 15 || a.IsZero)
        {
            Fp12 ret = a;
            for (int i = 0; i < count; i++) CyclotomicSquare(out ret, ret);
            result = ret;
            return;
        }
        Fp2 g1 = a.C0.C1, g2 = a.C0.C2, g3 = a.C1.C0, g5 = a.C1.C2;
        for (int i = 0; i < count; i++)
        {
            SquareFp4(out Fp4 t1, g3, g2);
            SquareFp4(out Fp4 t2, g1, g5);
            Fp2Fused.Subtract(out g1, t1.C0, g1);
            Fp2.Double(out g1, g1);
            Fp2.Add(out g1, g1, t1.C0);
            Fp2Fused.Subtract(out g2, t2.C0, g2);
            Fp2.Double(out g2, g2);
            Fp2.Add(out g2, g2, t2.C0);
            Fp2.MulByNonResidue(out t2.C1, t2.C1);
            Fp2.Add(out g3, t2.C1, g3);
            Fp2.Double(out g3, g3);
            Fp2.Add(out g3, g3, t2.C1);
            Fp2.Add(out g5, t1.C1, g5);
            Fp2.Double(out g5, g5);
            Fp2.Add(out g5, g5, t1.C1);
        }
        Fp2 numerator, denominator;
        if (g3.IsZero)
        {
            if (g2.IsZero) { result = One; return; }
            Fp2.Multiply(out numerator, g1, g5);
            Fp2.Double(out numerator, numerator);
            denominator = g2;
        }
        else
        {
            Fp2.Square(out Fp2 t0, g1);
            Fp2Fused.Subtract(out numerator, t0, g2);
            Fp2.Double(out numerator, numerator);
            Fp2.Add(out numerator, numerator, t0);
            Fp2.Square(out t0, g5);
            Fp2.MulByNonResidue(out t0, t0);
            Fp2.Add(out numerator, numerator, t0);
            Fp2.MulBy4(out denominator, g3);
        }
        Fp2.Inverse(out denominator, denominator);
        Fp2.Multiply(out Fp2 g4, numerator, denominator);
        Fp2.Multiply(out Fp2 product, g2, g1);
        Fp2.Square(out Fp2 g0, g4);
        Fp2Fused.Subtract(out g0, g0, product);
        Fp2.Double(out g0, g0);
        Fp2Fused.Subtract(out g0, g0, product);
        Fp2.Multiply(out product, g3, g5);
        Fp2.Add(out g0, g0, product);
        Fp2.MulByNonResidue(out g0, g0);
        Fp2.Add(out g0, g0, Fp2.One);
        result = new Fp12 { C0 = new Fp6 { C0 = g0, C1 = g1, C2 = g2 }, C1 = new Fp6 { C0 = g3, C1 = g4, C2 = g5 } };
    }

    /// <summary>Addition; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Add(out Fp12 result, in Fp12 a, in Fp12 b)
    {
        Fp6.Add(out Fp6 c0, a.C0, b.C0);
        Fp6.Add(out Fp6 c1, a.C1, b.C1);
        result = new Fp12 { C0 = c0, C1 = c1 };
    }

    /// <summary>Subtraction; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Subtract(out Fp12 result, in Fp12 a, in Fp12 b)
    {
        Fp6.Subtract(out Fp6 c0, a.C0, b.C0);
        Fp6.Subtract(out Fp6 c1, a.C1, b.C1);
        result = new Fp12 { C0 = c0, C1 = c1 };
    }

    /// <summary>Negation; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Negate(out Fp12 result, in Fp12 a)
    {
        Fp6.Negate(out Fp6 c0, a.C0);
        Fp6.Negate(out Fp6 c1, a.C1);
        result = new Fp12 { C0 = c0, C1 = c1 };
    }

    /// <summary>Conjugation over Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Conjugate(out Fp12 result, in Fp12 a)
    {
        Fp6 c0 = a.C0;
        Fp6.Negate(out Fp6 c1, a.C1);
        result = new Fp12 { C0 = c0, C1 = c1 };
    }

    /// <summary>Multiply by packed coefficients at positions 0, 1 and 4.</summary>
    [SkipLocalsInit]
    public static void MultiplyBySparse(out Fp12 result, in Fp12 a, in Fp2 x, in Fp2 y, in Fp2 z)
    {
        Fp6 packed = new() { C0 = x, C1 = y, C2 = z };
        MultiplyBySparse(out result, a, packed);
    }

    /// <summary>The p^n Frobenius map, for n = 1, 2 or 3.</summary>
    [SkipLocalsInit]
    public static void FrobeniusMap(out Fp12 result, in Fp12 a, int n)
    {
        if (n is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(n));
        Fp6.FrobeniusMap(out Fp6 c0, a.C0, n);
        Fp6.FrobeniusMap(out Fp6 c1, a.C1, n);
        Fp2 coefficient = n switch
        {
            1 => new Fp2(Fp.FromMontgomery(0x07089552b319d465UL, 0xc6695f92b50a8313UL, 0x97e83cccd117228fUL, 0xa35baecab2dc29eeUL, 0x1ce393ea5daace4dUL, 0x08f2220fb0fb66ebUL), Fp.FromMontgomery(0xb2f66aad4ce5d646UL, 0x5842a06bfc497cecUL, 0xcf4895d42599d394UL, 0xc11b9cba40a8e8d0UL, 0x2e3813cbe5a0de89UL, 0x110eefda88847fafUL)),
            2 => new Fp2(Fp.FromMontgomery(0xecfb361b798dba3aUL, 0xc100ddb891865a2cUL, 0x0ec08ff1232bda8eUL, 0xd5c13cc6f1ca4721UL, 0x47222a47bf7b5c04UL, 0x0110f184e51c5f59UL), Fp.Zero),
            _ => new Fp2(Fp.FromMontgomery(0x3e2f585da55c9ad1UL, 0x4294213d86c18183UL, 0x382844c88b623732UL, 0x92ad2afd19103e18UL, 0x1d794e4fac7cf0b9UL, 0x0bd592fc7d825ec8UL), Fp.FromMontgomery(0x7bcfa7a25aa30fdaUL, 0xdc17dec12a927e7cUL, 0x2f088dd86b4ebef1UL, 0xd1ca2087da74d4a7UL, 0x2da2596696cebc1dUL, 0x0e2b7eedbbfd87d2UL)),
        };
        if (n == 2)
        {
            Fp2.MultiplyByFp(out c1.C0, c1.C0, coefficient.C0);
            Fp2.MultiplyByFp(out c1.C1, c1.C1, coefficient.C0);
            Fp2.MultiplyByFp(out c1.C2, c1.C2, coefficient.C0);
        }
        else
        {
            ExtensionConstants.MultiplyConjugatePair(out c1.C0, c1.C0, coefficient.C0);
            ExtensionConstants.MultiplyConjugatePair(out c1.C1, c1.C1, coefficient.C0);
            ExtensionConstants.MultiplyConjugatePair(out c1.C2, c1.C2, coefficient.C0);
        }
        result = new Fp12 { C0 = c0, C1 = c1 };
    }
}
