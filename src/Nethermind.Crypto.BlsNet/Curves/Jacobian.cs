using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>Jacobian coordinates for a curve with a = 0. Arithmetic is variable-time.</summary>
public struct Jacobian<T> where T : struct, IField<T>
{
    public T X, Y, Z;
    public readonly bool IsInfinity => T.EqualsZero(Z);

    public Jacobian(T x, T y, T z) { X = x; Y = y; Z = z; }

    /// <summary>Creates a projective point from affine coordinates.</summary>
    public static void FromAffine(out Jacobian<T> r, in T x, in T y)
        => r = new(x, y, T.EqualsZero(x) && T.EqualsZero(y) ? T.Zero : T.One);

    /// <summary>Converts to affine with one inversion, returning (0,0) for infinity.</summary>
    public readonly void ToAffine(out T x, out T y)
    {
        if (IsInfinity) { x = y = T.Zero; return; }
        if (T.AreEqual(Z, T.One)) { x = X; y = Y; return; }
        CurveField<T>.Inverse(out T zi, Z); CurveField<T>.Square(out T zz, zi);
        CurveField<T>.Multiply(out x, X, zz); CurveField<T>.Multiply(out zz, zz, zi); CurveField<T>.Multiply(out y, Y, zz);
    }

    /// <summary>Doubles using dbl-2009-l; variable-time.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Double(out Jacobian<T> r, in Jacobian<T> p)
    {
        if (Fp.UseExpandedKernels) DoubleExpanded(out r, p);
        else DoubleCompact(out r, p);
    }

    private static void DoubleExpanded(out Jacobian<T> r, in Jacobian<T> p)
    {
        CurveField<T>.Square(out T a, p.X); CurveField<T>.Square(out T b, p.Y); CurveField<T>.Square(out T c, b);
        CurveField<T>.Add(out T d, p.X, b); CurveField<T>.Square(out d, d); CurveField<T>.SubtractSum(out d, d, a, c);
        CurveField<T>.Double(out d, d); CurveField<T>.MulBy3(out T e, a);
        // Consume Y and Z before assigning X; this order permits r to alias p.
        CurveField<T>.Multiply(out r.Z, p.Y, p.Z); CurveField<T>.Double(out r.Z, r.Z);
        CurveField<T>.Square(out r.X, e); CurveField<T>.Double(out T t, d); CurveField<T>.Subtract(out r.X, r.X, t);
        CurveField<T>.Subtract(out r.Y, d, r.X); CurveField<T>.Multiply(out r.Y, r.Y, e); CurveField<T>.MulBy8(out c, c);
        CurveField<T>.Subtract(out r.Y, r.Y, c);
    }

    private static void DoubleCompact(out Jacobian<T> r, in Jacobian<T> p)
    {
        CurveField<T>.Square(out T a, p.X); CurveField<T>.Square(out T b, p.Y); CurveField<T>.Square(out T c, b);
        CurveField<T>.Add(out T d, p.X, b); CurveField<T>.Square(out d, d); CurveField<T>.Subtract(out d, d, a);
        CurveField<T>.Subtract(out d, d, c); CurveField<T>.Double(out d, d); CurveField<T>.MulBy3(out T e, a);
        CurveField<T>.Square(out T x, e); CurveField<T>.Double(out T t, d); CurveField<T>.Subtract(out x, x, t);
        CurveField<T>.Subtract(out T y, d, x); CurveField<T>.Multiply(out y, y, e); CurveField<T>.MulBy8(out c, c);
        CurveField<T>.Subtract(out y, y, c); CurveField<T>.Multiply(out T z, p.Y, p.Z); CurveField<T>.Double(out z, z);
        r = new(x, y, z);
    }

    /// <summary>Adds arbitrary projective points, including doubling and infinity; variable-time.</summary>
    public static void Add(out Jacobian<T> r, in Jacobian<T> p, in Jacobian<T> q)
    {
        if (p.IsInfinity) { r = q; return; }
        if (q.IsInfinity) { r = p; return; }
        CurveField<T>.Square(out T z1, p.Z); CurveField<T>.Square(out T z2, q.Z);
        CurveField<T>.Multiply(out T u1, p.X, z2); CurveField<T>.Multiply(out T u2, q.X, z1);
        CurveField<T>.Multiply(out z2, z2, q.Z); CurveField<T>.Multiply(out z1, z1, p.Z);
        CurveField<T>.Multiply(out T s1, p.Y, z2); CurveField<T>.Multiply(out T s2, q.Y, z1);
        CurveField<T>.Subtract(out T h, u2, u1); CurveField<T>.Subtract(out T v, s2, s1);
        if (T.EqualsZero(h)) { if (T.EqualsZero(v)) Double(out r, p); else r = default; return; }
        CurveField<T>.Multiply(out T z, p.Z, q.Z); CurveField<T>.Multiply(out z, z, h);
        FinishAdd(out r, u1, s1, h, v, z);
    }

    /// <summary>Mixed addition, including doubling and infinity; variable-time.</summary>
    public static void AddAffine(out Jacobian<T> r, in Jacobian<T> p, in T qx, in T qy)
    {
        if (T.EqualsZero(qx) && T.EqualsZero(qy)) { r = p; return; }
        if (p.IsInfinity) { FromAffine(out r, qx, qy); return; }
        CurveField<T>.Square(out T zz, p.Z); CurveField<T>.Multiply(out T h, qx, zz);
        CurveField<T>.Multiply(out zz, zz, p.Z); CurveField<T>.Multiply(out T v, qy, zz);
        CurveField<T>.Subtract(out h, h, p.X); CurveField<T>.Subtract(out v, v, p.Y);
        if (T.EqualsZero(h)) { if (T.EqualsZero(v)) Double(out r, p); else r = default; return; }
        CurveField<T>.Multiply(out T z, p.Z, h); FinishAdd(out r, p.X, p.Y, h, v, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FinishAdd(out Jacobian<T> r, in T u, in T s, in T h, in T v, in T z)
    {
        if (Fp.UseExpandedKernels) FinishAddExpanded(out r, u, s, h, v, z);
        else FinishAddCompact(out r, u, s, h, v, z);
    }

    private static void FinishAddExpanded(out Jacobian<T> r, in T u, in T s, in T h, in T v, in T z)
    {
        CurveField<T>.Square(out T hh, h); CurveField<T>.Multiply(out T hhh, hh, h); CurveField<T>.Multiply(out T t, u, hh);
        CurveField<T>.Square(out r.X, v); CurveField<T>.Subtract(out r.X, r.X, hhh); CurveField<T>.Double(out T t2, t); CurveField<T>.Subtract(out r.X, r.X, t2);
        CurveField<T>.Subtract(out T y, t, r.X); CurveField<T>.MultiplyDifference(out r.Y, y, v, hhh, s);
        r.Z = z;
    }

    private static void FinishAddCompact(out Jacobian<T> r, in T u, in T s, in T h, in T v, in T z)
    {
        CurveField<T>.Square(out T hh, h); CurveField<T>.Multiply(out T hhh, hh, h); CurveField<T>.Multiply(out T t, u, hh);
        CurveField<T>.Square(out T x, v); CurveField<T>.Subtract(out x, x, hhh); CurveField<T>.Double(out T t2, t); CurveField<T>.Subtract(out x, x, t2);
        CurveField<T>.Subtract(out T y, t, x); CurveField<T>.Multiply(out y, y, v); CurveField<T>.Multiply(out hhh, hhh, s);
        CurveField<T>.Subtract(out y, y, hhh); r = new(x, y, z);
    }

    /// <summary>Negates a point.</summary>
    public static void Negate(out Jacobian<T> r, in Jacobian<T> p)
    { CurveField<T>.Negate(out T y, p.Y); r = new(p.X, y, p.Z); }

    /// <summary>Compares projective points without inversion; variable-time.</summary>
    public static bool AreEqual(in Jacobian<T> p, in Jacobian<T> q)
    {
        if (p.IsInfinity || q.IsInfinity) return p.IsInfinity == q.IsInfinity;
        CurveField<T>.Square(out T zz1, p.Z); CurveField<T>.Square(out T zz2, q.Z);
        CurveField<T>.Multiply(out T a, p.X, zz2); CurveField<T>.Multiply(out T b, q.X, zz1);
        if (!T.AreEqual(a, b)) return false;
        CurveField<T>.Multiply(out zz1, zz1, p.Z); CurveField<T>.Multiply(out zz2, zz2, q.Z);
        CurveField<T>.Multiply(out a, p.Y, zz2); CurveField<T>.Multiply(out b, q.Y, zz1); return T.AreEqual(a, b);
    }

    internal static void TimesMinusZ(out Jacobian<T> r, in Jacobian<T> p)
    {
        Jacobian<T> source = p;
        Double(out r, source);
        AddAndDouble(ref r, source, 2); AddAndDouble(ref r, source, 3);
        AddAndDouble(ref r, source, 9); AddAndDouble(ref r, source, 32); AddAndDouble(ref r, source, 16);
    }

    private static void AddAndDouble(ref Jacobian<T> r, in Jacobian<T> p, int n)
    {
        if (T.AreEqual(p.Z, T.One)) AddAffine(out r, r, p.X, p.Y); else Add(out r, r, p);
        for (int i = 0; i < n; i++) Double(out r, r);
    }

    internal static void BatchToAffine(Span<Jacobian<T>> points, Span<T> products)
    {
        T acc = T.One;
        for (int i = 0; i < points.Length; i++)
        { products[i] = acc; if (!points[i].IsInfinity) CurveField<T>.Multiply(out acc, acc, points[i].Z); }
        CurveField<T>.Inverse(out acc, acc);
        for (int i = points.Length - 1; i >= 0; i--)
        {
            ref Jacobian<T> p = ref points[i];
            if (p.IsInfinity) { p = default; continue; }
            CurveField<T>.Multiply(out T zi, acc, products[i]); CurveField<T>.Multiply(out acc, acc, p.Z);
            CurveField<T>.Square(out T zz, zi); CurveField<T>.Multiply(out p.X, p.X, zz);
            CurveField<T>.Multiply(out zz, zz, zi); CurveField<T>.Multiply(out p.Y, p.Y, zz); p.Z = T.One;
        }
    }
}
