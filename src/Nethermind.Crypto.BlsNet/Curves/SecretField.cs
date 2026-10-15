using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

// Restricted to the library's Fp/Fp2 specializations; keep IField's public contract unchanged.
internal static class SecretField<T> where T : struct, IField<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong IsZeroMask(in T a)
    {
        if (typeof(T) == typeof(Fp)) return Fp.IsZeroMask(Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        return Fp2.IsZeroMask(Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong EqualMask(in T a, in T b)
    {
        if (typeof(T) == typeof(Fp)) return Fp.EqualMask(Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in b)));
        return Fp2.EqualMask(Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Select(out T r, in T a, in T b, ulong mask)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
            Fp.Select(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in b)), mask);
        else
            Fp2.Select(out Unsafe.As<T, Fp2>(ref r), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in b)), mask);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Inverse(out T r, in T a)
    {
        Unsafe.SkipInit(out r);
        if (typeof(T) == typeof(Fp))
            Fp.InverseConstantTime(out Unsafe.As<T, Fp>(ref r), Unsafe.As<T, Fp>(ref Unsafe.AsRef(in a)));
        else
            Fp2.InverseConstantTime(out Unsafe.As<T, Fp2>(ref r), Unsafe.As<T, Fp2>(ref Unsafe.AsRef(in a)));
    }
}
