using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>Fp2 = Fp[u] / (u^2 + 1), as C0 + C1*u.</summary>
public struct Fp2 : IEquatable<Fp2>, IField<Fp2>
{
    public Fp C0;
    public Fp C1;

    public Fp2(Fp c0, Fp c1)
    {
        C0 = c0;
        C1 = c1;
    }

    public static Fp2 Zero => default;

    public static Fp2 One => new(Fp.One, Fp.Zero);

    public readonly bool IsZero => EqualsZero(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsZero(in Fp2 value)
    {
        return Fp.EqualsZero(value.C0) && Fp.EqualsZero(value.C1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqual(in Fp2 left, in Fp2 right)
    {
        return Fp.AreEqual(left.C0, right.C0) && Fp.AreEqual(left.C1, right.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Add(out Fp2 result, in Fp2 left, in Fp2 right)
    {
        Fp.Add(out result.C0, left.C0, right.C0);
        Fp.Add(out result.C1, left.C1, right.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(out Fp2 result, in Fp2 left, in Fp2 right)
    {
        Fp.Subtract(out result.C0, left.C0, right.C0);
        Fp.Subtract(out result.C1, left.C1, right.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Negate(out Fp2 result, in Fp2 value)
    {
        Fp.Negate(out result.C0, value.C0);
        Fp.Negate(out result.C1, value.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Double(out Fp2 result, in Fp2 value)
    {
        Add(out result, value, value);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MulBy3(out Fp2 result, in Fp2 value)
    {
        Fp.MulBy3(out result.C0, value.C0);
        Fp.MulBy3(out result.C1, value.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MulBy4(out Fp2 result, in Fp2 value)
    {
        Fp.MulBy4(out result.C0, value.C0);
        Fp.MulBy4(out result.C1, value.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MulBy8(out Fp2 result, in Fp2 value)
    {
        Fp.MulBy8(out result.C0, value.C0);
        Fp.MulBy8(out result.C1, value.C1);
    }

    /// <summary>Karatsuba: (a0 + a1 u)(b0 + b1 u) = a0 b0 - a1 b1 + ((a0 + a1)(b0 + b1) - a0 b0 - a1 b1) u.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(out Fp2 result, in Fp2 left, in Fp2 right)
    {
        if (Fp.UseExpandedKernels) MultiplyExpanded(out result, left, right);
        else MultiplyCompact(out result, left, right);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyCompact(out Fp2 result, in Fp2 left, in Fp2 right)
    {
        // The stock .NET 10 JIT spills the wide formula; keep its compact CIOS calls.
        Fp.MultiplyComposite(out Fp t0, left.C0, right.C0);
        Fp.MultiplyComposite(out Fp t1, left.C1, right.C1);
        Fp.AddUnreduced(out Fp sa, left.C0, left.C1);
        Fp.AddUnreduced(out Fp sb, right.C0, right.C1);
        Fp.MultiplyComposite(out Fp t2, sa, sb);
        Fp.SubtractComposite(out result.C0, t0, t1);
        Fp.SubtractSumComposite(out result.C1, t2, t0, t1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyExpanded(out Fp2 result, in Fp2 left, in Fp2 right)
    {
        // Products are below 4p^2. The final real magnitude is below p^2
        // and imaginary lies in [0, 2p^2), so both fit their reducer's pR bound.
        Fp.AddUnreduced(out Fp sa, left.C0, left.C1);
        Fp.AddUnreduced(out Fp sb, right.C0, right.C1);
        Fp.MultiplyWideNoAlias(out FpWide t0, left.C0, right.C0);
        Fp.MultiplyWideNoAlias(out FpWide t1, left.C1, right.C1);
        Fp.MultiplyWideNoAlias(out FpWide t2, sa, sb);
        Fp.SubtractWideImaginary(ref t2, t0, t1);
        Fp.ReduceWideDifference(out result.C0, t0, t1);
        Fp.ReduceWidePositive(out result.C1, t2);
    }

    /// <summary>(a0 + a1 u)^2 = (a0 + a1)(a0 - a1) + 2 a0 a1 u.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Square(out Fp2 result, in Fp2 value)
    {
        if (Fp.UseExpandedKernels) SquareExpanded(out result, value);
        else SquareCompact(out result, value);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SquareCompact(out Fp2 result, in Fp2 value)
    {
        Fp.AddUnreduced(out Fp sum, value.C0, value.C1);
        Fp.SubtractComposite(out Fp difference, value.C0, value.C1);
        Fp.AddUnreduced(out Fp twice0, value.C0, value.C0);
        Fp.MultiplyComposite(out Fp c1, twice0, value.C1);
        Fp.MultiplyComposite(out result.C0, sum, difference);
        result.C1 = c1;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SquareExpanded(out Fp2 result, in Fp2 value)
    {
        Fp.AddUnreduced(out Fp sum, value.C0, value.C1);
        Fp.SubtractComposite(out Fp difference, value.C0, value.C1);
        Fp.AddUnreduced(out Fp twice0, value.C0, value.C0);
        // Inline one CIOS kernel, saving a call within the carry-analysis budget.
        // Stock .NET 10 spills this method; compact mode keeps both isolated calls.
        Fp.Multiply(out Fp c1, twice0, value.C1);
        Fp.MultiplyComposite(out result.C0, sum, difference);
        result.C1 = c1;
    }

    /// <summary>value * Fp scalar.</summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MultiplyByFp(out Fp2 result, in Fp2 value, in Fp scalar)
    {
        Fp.MultiplyComposite(out Fp c0, value.C0, scalar);
        Fp.MultiplyComposite(out Fp c1, value.C1, scalar);
        result.C0 = c0;
        result.C1 = c1;
    }

    /// <summary>value * (1 + u), the non-residue of the Fp6 and Fp12 towers.</summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void MulByNonResidue(out Fp2 result, in Fp2 value)
    {
        Fp.SubtractComposite(out Fp c0, value.C0, value.C1);
        Fp.Add(out Fp c1, value.C0, value.C1);
        result.C0 = c0;
        result.C1 = c1;
    }

    /// <summary>a0 - a1 u, which is also the Frobenius map.</summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Conjugate(out Fp2 result, in Fp2 value)
    {
        Fp c0 = value.C0;
        Fp.Negate(out Fp c1, value.C1);
        result.C0 = c0;
        result.C1 = c1;
    }

    /// <summary>Variable-time inverse: (a0 - a1 u) / (a0^2 + a1^2), and 0 for 0.</summary>
    public static void Inverse(out Fp2 result, in Fp2 value)
    {
        Fp.MultiplyComposite(out Fp t0, value.C0, value.C0);
        Fp.MultiplyComposite(out Fp t1, value.C1, value.C1);
        Fp.Add(out t0, t0, t1);
        Fp.Inverse(out t1, t0);
        Fp.MultiplyComposite(out Fp c0, value.C0, t1);
        Fp.MultiplyComposite(out Fp c1, value.C1, t1);
        Fp.Negate(out c1, c1);
        result.C0 = c0;
        result.C1 = c1;
    }

    /// <summary>sgn0 of RFC 9380: sgn0(C0), or sgn0(C1) when C0 is zero.</summary>
    public static bool Sgn0(in Fp2 value)
    {
        bool sign0 = Fp.Sgn0(value.C0);
        bool zero0 = Fp.EqualsZero(value.C0);
        bool sign1 = Fp.Sgn0(value.C1);
        return sign0 || (zero0 && sign1);
    }

    public readonly bool Equals(Fp2 other) => AreEqual(this, other);

    public override readonly bool Equals(object? obj) => obj is Fp2 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(C0, C1);

    public static bool operator ==(Fp2 left, Fp2 right) => left.Equals(right);

    public static bool operator !=(Fp2 left, Fp2 right) => !left.Equals(right);

    public override readonly string ToString() => $"({C0}, {C1})";
}
