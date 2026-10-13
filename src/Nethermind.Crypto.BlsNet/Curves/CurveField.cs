using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

// Keep Fp carry chains outside large point kernels. Fp2 operations are already isolated.
internal static class CurveField<T> where T : struct, IField<T>
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Add(out T r, in T a, in T b) => T.Add(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Subtract(out T r, in T a, in T b) => T.Subtract(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Negate(out T r, in T a) => T.Negate(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Double(out T r, in T a) => T.Double(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MulBy3(out T r, in T a) => T.MulBy3(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MulBy4(out T r, in T a) => T.MulBy4(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MulBy8(out T r, in T a) => T.MulBy8(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Multiply(out T r, in T a, in T b) => T.Multiply(out r, a, b);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Square(out T r, in T a) => T.Square(out r, a);
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Inverse(out T r, in T a) => T.Inverse(out r, a);
}
