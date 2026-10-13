using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>XYZZ bucket coordinates. Arithmetic is variable-time.</summary>
public struct Xyzz<T> where T : struct, IField<T>
{
    public T X, Y, ZZ, ZZZ;
    public readonly bool IsInfinity => T.EqualsZero(ZZ);

    /// <summary>Converts without inversion.</summary>
    public readonly void ToJacobian(out Jacobian<T> r)
    { CurveField<T>.Multiply(out T x, X, ZZ); CurveField<T>.Multiply(out T y, Y, ZZZ); r = new(x, y, ZZ); }

    /// <summary>Adds two buckets, handling doubling and infinity; variable-time.</summary>
    public static void Add(out Xyzz<T> r, in Xyzz<T> a, in Xyzz<T> b)
    {
        if (a.IsInfinity) { r = b; return; }
        if (b.IsInfinity) { r = a; return; }
        CurveField<T>.Multiply(out T u, a.X, b.ZZ); CurveField<T>.Multiply(out T s, a.Y, b.ZZZ);
        CurveField<T>.Multiply(out T h, b.X, a.ZZ); CurveField<T>.Multiply(out T v, b.Y, a.ZZZ);
        CurveField<T>.Subtract(out h, h, u); CurveField<T>.Subtract(out v, v, s);
        if (T.EqualsZero(h)) { if (T.EqualsZero(v)) Double(out r, a); else r = default; return; }
        CurveField<T>.Multiply(out T zz, a.ZZ, b.ZZ); CurveField<T>.Multiply(out T zzz, a.ZZZ, b.ZZZ);
        Finish(out r, u, s, h, v, zz, zzz);
    }

    /// <summary>Adds or subtracts an affine point; variable-time.</summary>
    public static void AddAffine(out Xyzz<T> r, in Xyzz<T> a, in T x, in T y, bool subtract = false)
    {
        if (T.EqualsZero(x) && T.EqualsZero(y)) { r = a; return; }
        T sy = y; if (subtract) CurveField<T>.Negate(out sy, sy);
        if (a.IsInfinity) { r = new() { X = x, Y = sy, ZZ = T.One, ZZZ = T.One }; return; }
        CurveField<T>.Multiply(out T h, x, a.ZZ); CurveField<T>.Multiply(out T v, sy, a.ZZZ);
        CurveField<T>.Subtract(out h, h, a.X); CurveField<T>.Subtract(out v, v, a.Y);
        if (T.EqualsZero(h)) { if (T.EqualsZero(v)) Double(out r, a); else r = default; return; }
        Finish(out r, a.X, a.Y, h, v, a.ZZ, a.ZZZ);
    }

    private static void Finish(out Xyzz<T> r, in T u, in T s, in T h, in T v, in T zz, in T zzz)
    {
        CurveField<T>.Square(out T hh, h); CurveField<T>.Multiply(out T hhh, h, hh); CurveField<T>.Multiply(out T q, u, hh);
        CurveField<T>.Square(out T x, v); CurveField<T>.Subtract(out x, x, hhh); CurveField<T>.Double(out T t, q); CurveField<T>.Subtract(out x, x, t);
        CurveField<T>.Subtract(out T y, q, x); CurveField<T>.MultiplyDifference(out y, y, v, s, hhh);
        CurveField<T>.Multiply(out T rz, zz, hh); CurveField<T>.Multiply(out T rzz, zzz, hhh);
        r = new() { X = x, Y = y, ZZ = rz, ZZZ = rzz };
    }

    private static void Double(out Xyzz<T> r, in Xyzz<T> a)
    {
        CurveField<T>.Double(out T u, a.Y); CurveField<T>.Square(out T v, u); CurveField<T>.Multiply(out T w, u, v);
        CurveField<T>.Multiply(out T s, a.X, v); CurveField<T>.Square(out T m, a.X); CurveField<T>.MulBy3(out m, m);
        CurveField<T>.Square(out T x, m); CurveField<T>.Double(out T t, s); CurveField<T>.Subtract(out x, x, t);
        CurveField<T>.Subtract(out T y, s, x); CurveField<T>.Multiply(out y, y, m); CurveField<T>.Multiply(out t, w, a.Y); CurveField<T>.Subtract(out y, y, t);
        CurveField<T>.Multiply(out T zz, a.ZZ, v); CurveField<T>.Multiply(out T zzz, a.ZZZ, w);
        r = new() { X = x, Y = y, ZZ = zz, ZZZ = zzz };
    }
}
