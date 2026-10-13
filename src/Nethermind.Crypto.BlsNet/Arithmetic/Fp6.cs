using System.Runtime.CompilerServices;

// Tower formulas and constants adapted from blst fp12_tower.c.
// Copyright Supranational LLC; licensed under Apache-2.0.

namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>Fp6 = Fp2[v] / (v^3 - (1 + u)), in blst's memory layout.</summary>
public struct Fp6 : IEquatable<Fp6>
{
    public Fp2 C0;
    public Fp2 C1;
    public Fp2 C2;

    public static Fp6 Zero => default;
    public static Fp6 One => new() { C0 = Fp2.One };
    public readonly bool IsZero => EqualsZero(this);
    public readonly bool IsOne => AreEqual(this, One);
    public static bool EqualsZero(in Fp6 a) => Fp2.EqualsZero(a.C0) && Fp2.EqualsZero(a.C1) && Fp2.EqualsZero(a.C2);
    public static bool AreEqual(in Fp6 a, in Fp6 b) => Fp2.AreEqual(a.C0, b.C0) && Fp2.AreEqual(a.C1, b.C1) && Fp2.AreEqual(a.C2, b.C2);
    public readonly bool Equals(Fp6 other) => AreEqual(this, other);
    public override readonly bool Equals(object? obj) => obj is Fp6 other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(C0, C1, C2);

    public static bool operator ==(Fp6 left, Fp6 right) => left.Equals(right);
    public static bool operator !=(Fp6 left, Fp6 right) => !left.Equals(right);

    /// <summary>Add in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Add(out Fp6 result, in Fp6 a, in Fp6 b)
    {
        Fp2.Add(out result.C0, a.C0, b.C0);
        Fp2.Add(out result.C1, a.C1, b.C1);
        Fp2.Add(out result.C2, a.C2, b.C2);
    }

    /// <summary>Subtract in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Subtract(out Fp6 result, in Fp6 a, in Fp6 b)
    {
        Fp2.Subtract(out result.C0, a.C0, b.C0);
        Fp2.Subtract(out result.C1, a.C1, b.C1);
        Fp2.Subtract(out result.C2, a.C2, b.C2);
    }

    /// <summary>Negate in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Negate(out Fp6 result, in Fp6 a)
    {
        Fp2.Negate(out result.C0, a.C0);
        Fp2.Negate(out result.C1, a.C1);
        Fp2.Negate(out result.C2, a.C2);
    }

    /// <summary>Multiply in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Multiply(out Fp6 result, in Fp6 a, in Fp6 b)
    {
        WideTower.Multiply6(out Fp6Wide wide, a, b);
        WideTower.Reduce6Small(out result, wide);
    }

    /// <summary>Square in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Square(out Fp6 result, in Fp6 a)
    {
        if (Fp.UseExpandedKernels) { WideTower.Square6(out result, a); return; }
        Unsafe.SkipInit(out Fp2 s0);
        Unsafe.SkipInit(out Fp2 m01);
        Unsafe.SkipInit(out Fp2 m12);
        Unsafe.SkipInit(out Fp2 s2);
        Fp2.Square(out s0, a.C0);
        Fp2.Multiply(out m01, a.C0, a.C1);
        Fp2.Add(out m01, m01, m01);
        Fp2.Multiply(out m12, a.C1, a.C2);
        Fp2.Add(out m12, m12, m12);
        Fp2.Square(out s2, a.C2);
        Fp2.Add(out result.C2, a.C2, a.C1);
        Fp2.Add(out result.C2, result.C2, a.C0);
        Fp2.Square(out result.C2, result.C2);
        Fp2.Subtract(out result.C2, result.C2, s0);
        Fp2.Subtract(out result.C2, result.C2, s2);
        Fp2.Subtract(out result.C2, result.C2, m01);
        Fp2.Subtract(out result.C2, result.C2, m12);
        Fp2.MulByNonResidue(out result.C0, m12);
        Fp2.Add(out result.C0, result.C0, s0);
        Fp2.MulByNonResidue(out result.C1, s2);
        Fp2.Add(out result.C1, result.C1, m01);
    }

    /// <summary>Variable-time inverse in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void Inverse(out Fp6 result, in Fp6 a)
    {
        Unsafe.SkipInit(out Fp2 c0);
        Unsafe.SkipInit(out Fp2 c1);
        Unsafe.SkipInit(out Fp2 c2);
        Unsafe.SkipInit(out Fp2 t0);
        Unsafe.SkipInit(out Fp2 t1);
        Fp2.Square(out c0, a.C0);
        Fp2.Multiply(out t0, a.C1, a.C2);
        Fp2.MulByNonResidue(out t0, t0);
        Fp2.Subtract(out c0, c0, t0);
        Fp2.Square(out c1, a.C2);
        Fp2.MulByNonResidue(out c1, c1);
        Fp2.Multiply(out t0, a.C0, a.C1);
        Fp2.Subtract(out c1, c1, t0);
        Fp2.Square(out c2, a.C1);
        Fp2.Multiply(out t0, a.C0, a.C2);
        Fp2.Subtract(out c2, c2, t0);
        Fp2.Multiply(out t0, c1, a.C2);
        Fp2.Multiply(out t1, c2, a.C1);
        Fp2.Add(out t0, t0, t1);
        Fp2.MulByNonResidue(out t0, t0);
        Fp2.Multiply(out t1, c0, a.C0);
        Fp2.Add(out t0, t0, t1);
        Fp2.Inverse(out t1, t0);
        Fp2.Multiply(out result.C0, c0, t1);
        Fp2.Multiply(out result.C1, c1, t1);
        Fp2.Multiply(out result.C2, c2, t1);
    }

    /// <summary>MultiplyBy0Y0 in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void MultiplyBy0Y0(out Fp6 result, in Fp6 a, in Fp2 b)
    {
        Unsafe.SkipInit(out Fp6 ret);
        Unsafe.SkipInit(out Fp2 t);
        Fp2.Multiply(out t, a.C2, b);
        Fp2.Multiply(out ret.C2, a.C1, b);
        Fp2.Multiply(out ret.C1, a.C0, b);
        Fp2.MulByNonResidue(out ret.C0, t);
        result = ret;
    }

    /// <summary>MultiplyByXY0 in Fp6; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void MultiplyByXY0(out Fp6 result, in Fp6 a, in Fp6 b)
    {
        Unsafe.SkipInit(out Fp2 t0);
        Unsafe.SkipInit(out Fp2 t1);
        Unsafe.SkipInit(out Fp2 t3);
        Unsafe.SkipInit(out Fp2 t4);
        Unsafe.SkipInit(out Fp2 t5);
        Fp2.Multiply(out t0, a.C0, b.C0);
        Fp2.Multiply(out t1, a.C1, b.C1);
        Fp2.Multiply(out t3, a.C2, b.C1);
        Fp2.MulByNonResidue(out t3, t3);
        Fp2.Add(out t4, a.C0, a.C1);
        Fp2.Add(out t5, b.C0, b.C1);
        Fp2.Multiply(out result.C1, t4, t5);
        Fp2.Subtract(out result.C1, result.C1, t0);
        Fp2.Subtract(out result.C1, result.C1, t1);
        Fp2.Multiply(out result.C2, a.C2, b.C0);
        Fp2.Add(out result.C2, result.C2, t1);
        Fp2.Add(out result.C0, t3, t0);
    }

    /// <summary>Multiplication by v; inputs may alias the result.</summary>
    [SkipLocalsInit]
    public static void MultiplyByV(out Fp6 result, in Fp6 a)
    {
        Fp2.MulByNonResidue(out Fp2 c0, a.C2);
        Fp2 c1 = a.C0, c2 = a.C1;
        result = new Fp6 { C0 = c0, C1 = c1, C2 = c2 };
    }

    /// <summary>The p^n Frobenius map, for n = 1, 2 or 3.</summary>
    [SkipLocalsInit]
    public static void FrobeniusMap(out Fp6 result, in Fp6 a, int n)
    {
        if (n is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(n));
        Fp6 ret = a;
        if ((n & 1) != 0)
        {
            Fp2.Conjugate(out ret.C0, ret.C0);
            Fp2.Conjugate(out ret.C1, ret.C1);
            Fp2.Conjugate(out ret.C2, ret.C2);
        }
        Fp2 coefficient1 = n switch
        {
            1 => new Fp2(Fp.Zero, Fp.FromMontgomery(0xcd03c9e48671f071UL, 0x5dab22461fcda5d2UL, 0x587042afd3851b95UL, 0x8eb60ebe01bacb9eUL, 0x03f97d6e83d050d2UL, 0x18f0206554638741UL)),
            2 => new Fp2(Fp.FromMontgomery(0x30f1361b798a64e8UL, 0xf3b8ddab7ece5a2aUL, 0x16a8ca3ac61577f7UL, 0xc26a2ff874fd029bUL, 0x3636b76660701c6eUL, 0x051ba4ab241b6160UL), Fp.Zero),
            _ => new Fp2(Fp.Zero, Fp.One),
        };
        Fp coefficient2 = n switch
        {
            1 => Fp.FromMontgomery(0x890dc9e4867545c3UL, 0x2af322533285a5d5UL, 0x50880866309b7e2cUL, 0xa20d1b8c7e881024UL, 0x14e4f04fe2db9068UL, 0x14e56d3f1564853aUL),
            2 => Fp.FromMontgomery(0xcd03c9e48671f071UL, 0x5dab22461fcda5d2UL, 0x587042afd3851b95UL, 0x8eb60ebe01bacb9eUL, 0x03f97d6e83d050d2UL, 0x18f0206554638741UL),
            _ => Fp.FromMontgomery(0x43f5fffffffcaaaeUL, 0x32b7fff2ed47fffdUL, 0x07e83a49a2e99d69UL, 0xeca8f3318332bb7aUL, 0xef148d1ea0f4c069UL, 0x040ab3263eff0206UL),
        };
        if (n == 1)
        {
            Fp.MultiplyComposite(out Fp real, ret.C1.C1, coefficient1.C1);
            Fp.MultiplyComposite(out ret.C1.C1, ret.C1.C0, coefficient1.C1);
            Fp.Negate(out ret.C1.C0, real);
            Fp2.MultiplyByFp(out ret.C2, ret.C2, coefficient2);
        }
        else if (n == 2)
        {
            Fp2.MultiplyByFp(out ret.C1, ret.C1, coefficient1.C0);
            Fp2.MultiplyByFp(out ret.C2, ret.C2, coefficient2);
        }
        else
        {
            Fp real = ret.C1.C0;
            Fp.Negate(out ret.C1.C0, ret.C1.C1);
            ret.C1.C1 = real;
            Fp2.Negate(out ret.C2, ret.C2);
        }
        result = ret;
    }
}
