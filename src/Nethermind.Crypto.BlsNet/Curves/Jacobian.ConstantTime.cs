// Constant-time dadd formula ported from blst ec_ops.h (Apache-2.0).
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

public partial struct Jacobian<T> where T : struct, IField<T>
{
    internal static ulong InfinityMask(in Jacobian<T> p) => SecretField<T>.IsZeroMask(p.Z);

    internal static void Select(out Jacobian<T> r, in Jacobian<T> a, in Jacobian<T> b, ulong mask)
    {
        SecretField<T>.Select(out T x, a.X, b.X, mask);
        SecretField<T>.Select(out T y, a.Y, b.Y, mask);
        SecretField<T>.Select(out T z, a.Z, b.Z, mask); r = new(x, y, z);
    }

    internal static void FromAffineConstantTime(out Jacobian<T> r, in T x, in T y)
    {
        ulong infinity = SecretField<T>.IsZeroMask(x) & SecretField<T>.IsZeroMask(y);
        SecretField<T>.Select(out T z, T.Zero, T.One, infinity); r = new(x, y, z);
    }

    internal readonly void ToAffineConstantTime(out T x, out T y)
    {
        SecretField<T>.Inverse(out T zi, Z); CurveField<T>.Square(out T zz, zi);
        CurveField<T>.Multiply(out x, X, zz); CurveField<T>.Multiply(out zz, zz, zi);
        CurveField<T>.Multiply(out y, Y, zz);
        // Inverse(0)=0 already yields canonical affine infinity, even for arbitrary X/Y.
    }

    internal static void ConditionalNegate(out Jacobian<T> r, in Jacobian<T> p, ulong mask)
    {
        CurveField<T>.Negate(out T negative, p.Y);
        SecretField<T>.Select(out T y, negative, p.Y, mask); r = new(p.X, y, p.Z);
    }

    internal static ulong EqualMask(in Jacobian<T> p, in Jacobian<T> q)
    {
        CurveField<T>.Square(out T zz1, p.Z); CurveField<T>.Square(out T zz2, q.Z);
        CurveField<T>.Multiply(out T a, p.X, zz2); CurveField<T>.Multiply(out T b, q.X, zz1);
        ulong equalX = SecretField<T>.EqualMask(a, b);
        CurveField<T>.Multiply(out zz1, zz1, p.Z); CurveField<T>.Multiply(out zz2, zz2, q.Z);
        CurveField<T>.Multiply(out a, p.Y, zz2); CurveField<T>.Multiply(out b, q.Y, zz1);
        ulong pInf = InfinityMask(p), qInf = InfinityMask(q);
        return (pInf & qInf) | (~(pInf | qInf) & equalX & SecretField<T>.EqualMask(a, b));
    }

    internal static void AddConstantTime(out Jacobian<T> r, in Jacobian<T> p, in Jacobian<T> q)
    {
        CurveField<T>.Double(out T dsx, p.X); CurveField<T>.Square(out T dr, p.X);
        CurveField<T>.MulBy3(out dr, dr); CurveField<T>.Double(out T dh, p.Y);
        CurveField<T>.Square(out T z2, q.Z); CurveField<T>.Multiply(out T zz, p.Z, q.Z);
        CurveField<T>.Square(out T z1, p.Z);
        CurveField<T>.Multiply(out T s1, p.Y, q.Z); CurveField<T>.Multiply(out s1, s1, z2);
        CurveField<T>.Multiply(out T v, q.Y, p.Z); CurveField<T>.Multiply(out v, v, z1);
        CurveField<T>.Subtract(out v, v, s1);
        CurveField<T>.Multiply(out T u1, z2, p.X); CurveField<T>.Multiply(out T u2, z1, q.X);
        CurveField<T>.Add(out T sx, u1, u2); CurveField<T>.Subtract(out T h, u2, u1);
        ulong doubling = SecretField<T>.IsZeroMask(h) & SecretField<T>.IsZeroMask(v);
        SecretField<T>.Select(out u1, p.X, u1, doubling);
        SecretField<T>.Select(out s1, p.Y, s1, doubling);
        SecretField<T>.Select(out zz, p.Z, zz, doubling);
        SecretField<T>.Select(out h, dh, h, doubling);
        SecretField<T>.Select(out v, dr, v, doubling);
        SecretField<T>.Select(out sx, dsx, sx, doubling);
        CurveField<T>.Multiply(out T z, zz, h); CurveField<T>.Square(out T hh, h);
        CurveField<T>.Multiply(out T hhh, hh, h); CurveField<T>.Multiply(out hhh, hhh, s1);
        CurveField<T>.Multiply(out T y, hh, u1); CurveField<T>.Multiply(out hh, hh, sx);
        CurveField<T>.Square(out T x, v); CurveField<T>.Subtract(out x, x, hh);
        CurveField<T>.Subtract(out y, y, x); CurveField<T>.Multiply(out y, y, v); CurveField<T>.Subtract(out y, y, hhh);
        Jacobian<T> sum = new(x, y, z);
        Select(out sum, p, sum, InfinityMask(q)); Select(out r, q, sum, InfinityMask(p));
    }

    internal static void AddAffineConstantTime(out Jacobian<T> r, in Jacobian<T> p, in T qx, in T qy)
    {
        // Mixed dadd from ec_ops.h; doubling uses the affine operand to avoid Z rescaling.
        CurveField<T>.Double(out T dsx, qx); CurveField<T>.Square(out T dr, qx);
        CurveField<T>.MulBy3(out dr, dr); CurveField<T>.Double(out T dh, qy);
        CurveField<T>.Square(out T zz, p.Z); CurveField<T>.Multiply(out T v, zz, p.Z);
        CurveField<T>.Multiply(out v, v, qy); CurveField<T>.Subtract(out v, v, p.Y);
        CurveField<T>.Multiply(out T h, zz, qx); CurveField<T>.Add(out T sx, p.X, h);
        CurveField<T>.Subtract(out h, h, p.X); CurveField<T>.Multiply(out T z, h, p.Z);
        ulong doubling = SecretField<T>.IsZeroMask(h) & SecretField<T>.IsZeroMask(v);
        SecretField<T>.Select(out T u, qx, p.X, doubling);
        SecretField<T>.Select(out T s, qy, p.Y, doubling);
        SecretField<T>.Select(out z, dh, z, doubling);
        SecretField<T>.Select(out h, dh, h, doubling); SecretField<T>.Select(out v, dr, v, doubling);
        SecretField<T>.Select(out sx, dsx, sx, doubling);
        CurveField<T>.Square(out T hh, h); CurveField<T>.Multiply(out T hhh, hh, h);
        CurveField<T>.Multiply(out hhh, hhh, s); CurveField<T>.Multiply(out T y, hh, u);
        CurveField<T>.Multiply(out hh, hh, sx); CurveField<T>.Square(out T x, v);
        CurveField<T>.Subtract(out x, x, hh); CurveField<T>.Subtract(out y, y, x);
        CurveField<T>.Multiply(out y, y, v); CurveField<T>.Subtract(out y, y, hhh);
        Jacobian<T> sum = new(x, y, z);
        FromAffineConstantTime(out Jacobian<T> q, qx, qy);
        Select(out sum, p, sum, InfinityMask(q)); Select(out r, q, sum, InfinityMask(p));
    }

    internal static void TimesMinusZConstantTime(out Jacobian<T> r, in Jacobian<T> p)
    {
        Jacobian<T> source = p; Double(out r, source);
        AddAndDoubleConstantTime(ref r, source, 2); AddAndDoubleConstantTime(ref r, source, 3);
        AddAndDoubleConstantTime(ref r, source, 9); AddAndDoubleConstantTime(ref r, source, 32);
        AddAndDoubleConstantTime(ref r, source, 16);
    }

    private static void AddAndDoubleConstantTime(ref Jacobian<T> r, in Jacobian<T> p, int count)
    { AddConstantTime(out r, r, p); for (int i = 0; i < count; i++) Double(out r, r); }
}
