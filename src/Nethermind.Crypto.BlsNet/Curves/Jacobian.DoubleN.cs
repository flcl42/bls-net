using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

public partial struct Jacobian<T> where T : struct, IField<T>
{
    // Fixed public loop count; also used for secret map coordinates.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void DoubleN(out Jacobian<T> r, in Jacobian<T> p, int count)
    {
        if (typeof(T) != typeof(Fp) && typeof(T) != typeof(Fp2))
        {
            r = p;
            for (int i = 0; i < count; i++) Double(out r, r);
            return;
        }
        T x = p.X, y = p.Y, z = p.Z;
        for (int i = 0; i < count; i++)
        {
            PrepareDouble(out T d, out T e, out T c, x, y);
            DoubleZ(out z, y, z);
            FinishDouble(out x, out y, d, e, c);
        }
        r = new(x, y, z);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void PrepareDouble(out T d, out T e, out T c, in T x, in T y)
    {
        CurveField<T>.Square(out T a, x); CurveField<T>.Square(out T b, y); CurveField<T>.Square(out c, b);
        if (typeof(T) == typeof(Fp2))
        {
            DoublingField<T>.Add(out d, x, b); CurveField<T>.Square(out d, d);
            DoublingField<T>.SubtractSum(out d, d, a, c); DoublingField<T>.Double(out d, d);
        }
        else
        {
            CurveField<T>.Multiply(out d, x, b); DoublingField<T>.MulBy4(out d, d);
        }
        DoublingField<T>.MulBy3(out e, a);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void DoubleZ(out T z, in T y, in T oldZ)
    {
        CurveField<T>.Multiply(out z, y, oldZ); DoublingField<T>.Double(out z, z);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void FinishDouble(out T x, out T y, in T d, in T e, in T c)
    {
        CurveField<T>.Square(out x, e); DoublingField<T>.Double(out T t, d); DoublingField<T>.Subtract(out x, x, t);
        DoublingField<T>.Subtract(out y, d, x); CurveField<T>.Multiply(out y, y, e); DoublingField<T>.MulBy8(out T c8, c);
        DoublingField<T>.Subtract(out y, y, c8);
    }
}

// Inline only linear arithmetic into the doubling run; products stay isolated.
internal static class DoublingField<T> where T : struct, IField<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(out T r, in T a, in T b)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.Add(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in b)));
        }
        else
        {
            Fp.Add(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C0);
            Fp.Add(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Subtract(out T r, in T a, in T b)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.Subtract(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in b)));
        }
        else
        {
            Fp.Subtract(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C0);
            Fp.Subtract(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Double(out T r, in T a)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.Double(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        }
        else
        {
            Fp.Double(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0);
            Fp.Double(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy3(out T r, in T a)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.MulBy3(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        }
        else
        {
            Fp.MulBy3(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0);
            Fp.MulBy3(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy8(out T r, in T a)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.MulBy8Fast(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        }
        else
        {
            Fp.MulBy8Fast(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0);
            Fp.MulBy8Fast(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy4(out T r, in T a)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.MulBy4(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        }
        else
        {
            Fp.MulBy4(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0);
            Fp.MulBy4(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractSum(out T r, in T a, in T b, in T c)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
        {
            Fp.SubtractSumComposite(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in b)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in c)));
        }
        else
        {
            Fp.SubtractSumComposite(out Unsafe.As<T, Fp2>(ref r).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C0, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in c)).C0);
            Fp.SubtractSumComposite(out Unsafe.As<T, Fp2>(ref r).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)).C1, Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in c)).C1);
        }
    }
}
