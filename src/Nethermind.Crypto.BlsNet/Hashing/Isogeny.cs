using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

internal static class Isogeny<T> where T : unmanaged, IField<T>
{
    [SkipLocalsInit]
    internal static void Apply(out Jacobian<T> r, in Jacobian<T> p, ReadOnlySpan<T> xnMap,
        ReadOnlySpan<T> xdMap, ReadOnlySpan<T> ynMap, ReadOnlySpan<T> ydMap)
    {
        int degree = Math.Max(1, Math.Max(Math.Max(xnMap.Length - 1, xdMap.Length), Math.Max(ynMap.Length - 1, ydMap.Length)));
        Span<T> powers = stackalloc T[degree + 1]; powers[0] = T.One;
        CurveField<T>.Square(out powers[1], p.Z);
        for (int i = 2; i < powers.Length; i++)
        {
            if ((i & 1) == 0 && (typeof(T) == typeof(Fp2) || Fp.UseExpandedKernels)) CurveField<T>.Square(out powers[i], powers[i / 2]);
            else CurveField<T>.Multiply(out powers[i], powers[i - 1], powers[1]);
        }
        Evaluate(out T xn, p.X, powers, xnMap, false);
        Evaluate(out T xd, p.X, powers, xdMap, true); CurveField<T>.Multiply(out xd, xd, powers[1]);
        Evaluate(out T yn, p.X, powers, ynMap, false); CurveField<T>.Multiply(out yn, yn, p.Y);
        Evaluate(out T yd, p.X, powers, ydMap, true);
        CurveField<T>.Multiply(out T z3, powers[1], p.Z); CurveField<T>.Multiply(out yd, yd, z3);
        CurveField<T>.Multiply(out T z, xd, yd); CurveField<T>.Multiply(out T x, xn, yd); CurveField<T>.Multiply(out x, x, z);
        CurveField<T>.Square(out T y, z); CurveField<T>.Multiply(out y, y, xd); CurveField<T>.Multiply(out y, y, yn);
        r = new(x, y, z);
    }

    private static void Evaluate(out T r, in T x, ReadOnlySpan<T> powers, ReadOnlySpan<T> map, bool monic)
    {
        int degree = monic ? map.Length : map.Length - 1;
        r = monic ? T.One : map[^1];
        for (int i = degree - 1; i >= 0; i--)
        {
            if (monic && i == degree - 1)
            {
                CurveField<T>.Multiply(out T t, map[i], powers[degree - i]); CurveField<T>.Add(out r, x, t);
            }
            else CurveField<T>.MultiplySum(out r, r, x, map[i], powers[degree - i]);
        }
    }
}
