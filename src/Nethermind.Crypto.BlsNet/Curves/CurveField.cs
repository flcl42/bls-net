using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

// Keep Fp carry chains outside large point kernels. Fp2 operations are already isolated.
internal static class CurveField<T> where T : struct, IField<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(out T r, in T a, in T b)
    { if (typeof(T) == typeof(Fp2)) T.Add(out r, a, b); else AddIsolated(out r, a, b); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Subtract(out T r, in T a, in T b)
    { if (typeof(T) == typeof(Fp2)) T.Subtract(out r, a, b); else SubtractIsolated(out r, a, b); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Negate(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.Negate(out r, a); else NegateIsolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Double(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.Double(out r, a); else DoubleIsolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy3(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.MulBy3(out r, a); else MulBy3Isolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy4(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.MulBy4(out r, a); else MulBy4Isolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy8(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.MulBy8(out r, a); else MulBy8Isolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Multiply(out T r, in T a, in T b)
    { if (typeof(T) == typeof(Fp2)) T.Multiply(out r, a, b); else MultiplyIsolated(out r, a, b); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Square(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.Square(out r, a); else SquareIsolated(out r, a); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Inverse(out T r, in T a)
    { if (typeof(T) == typeof(Fp2)) T.Inverse(out r, a); else InverseIsolated(out r, a); }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AddIsolated(out T r, in T a, in T b) => T.Add(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SubtractIsolated(out T r, in T a, in T b) => T.Subtract(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void NegateIsolated(out T r, in T a) => T.Negate(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void DoubleIsolated(out T r, in T a) => T.Double(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MulBy3Isolated(out T r, in T a) => T.MulBy3(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MulBy4Isolated(out T r, in T a) => T.MulBy4(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MulBy8Isolated(out T r, in T a) => T.MulBy8(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyIsolated(out T r, in T a, in T b) => T.Multiply(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SquareIsolated(out T r, in T a) => T.Square(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void InverseIsolated(out T r, in T a) => T.Inverse(out r, a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplyDifference(out T r, in T a, in T b, in T c, in T d)
    {
        if (typeof(T) == typeof(Fp2) && Fp.UseExpandedKernels)
        {
            Unsafe.SkipInit(out r);
            Fp2Fused.MultiplyDifference(out Unsafe.As<T, Fp2>(ref r),
                Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)),
                Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in c)), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in d)));
        }
        else
        {
            Multiply(out T t0, a, b); Multiply(out T t1, c, d); Subtract(out r, t0, t1);
        }
    }
}
