// Addition chain adapted from blst sqrt-addchain.h (Apache-2.0).
// Copyright Supranational LLC. Adapted for this C# rewrite; see NOTICE and LICENSE.
using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

internal static class FpCurveRoots
{
    private interface ISquare { static abstract void Square(out Fp result, in Fp value); }
    private readonly struct CompactSquare : ISquare
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Square(out Fp result, in Fp value) => Fp.MultiplyComposite(out result, value, value);
    }
    private readonly struct ExpandedSquare : ISquare
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Square(out Fp result, in Fp value) => FpFused.Square(out result, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReciprocalSqrt(out Fp result, in Fp input)
    {
        if (Fp.UseExpandedKernels) Chain<ExpandedSquare>(out result, input);
        else Chain<CompactSquare>(out result, input);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Chain<TSquare>(out Fp result, in Fp input) where TSquare : struct, ISquare
    {
        Span<Fp> t = stackalloc Fp[16];
        t[13] = input;
        TSquare.Square(out t[0], t[13]);
        Fp.MultiplyComposite(out t[8], t[0], t[13]);
        TSquare.Square(out t[4], t[0]);
        Fp.MultiplyComposite(out t[1], t[8], t[0]);
        Fp.MultiplyComposite(out t[6], t[4], t[8]);
        Fp.MultiplyComposite(out t[9], t[1], t[4]);
        Fp.MultiplyComposite(out t[12], t[6], t[4]);
        Fp.MultiplyComposite(out t[3], t[9], t[4]);
        Fp.MultiplyComposite(out t[7], t[12], t[4]);
        Fp.MultiplyComposite(out t[15], t[3], t[4]);
        Fp.MultiplyComposite(out t[10], t[7], t[4]);
        Fp.MultiplyComposite(out t[2], t[15], t[4]);
        Fp.MultiplyComposite(out t[11], t[10], t[4]);
        TSquare.Square(out t[0], t[3]);
        Fp.MultiplyComposite(out t[14], t[11], t[4]);
        Fp.MultiplyComposite(out t[5], t[0], t[8]);
        Fp.MultiplyComposite(out t[4], t[0], t[1]);
        Fp.SquareNThenMultiply(out t[0], t[0], 12, t[15]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[1]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[6]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[11]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 2, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[9]);
        Fp.SquareNThenMultiply(out t[0], t[0], 3, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[14]);
        Fp.SquareNThenMultiply(out t[0], t[0], 3, t[13]);
        Fp.SquareNThenMultiply(out t[0], t[0], 8, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[11]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[12]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[5]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[9]);
        Fp.SquareNThenMultiply(out t[0], t[0], 8, t[5]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[11]);
        Fp.SquareNThenMultiply(out t[0], t[0], 9, t[10]);
        Fp.SquareNThenMultiply(out t[0], t[0], 2, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[6]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[1]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[9]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[11]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[5]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[10]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[10]);
        Fp.SquareNThenMultiply(out t[0], t[0], 8, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[2]);
        Fp.SquareNThenMultiply(out t[0], t[0], 9, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 3, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 8, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 3, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[9]);
        Fp.SquareNThenMultiply(out t[0], t[0], 9, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[2]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 3, t[8]);
        Fp.SquareNThenMultiply(out t[0], t[0], 8, t[2]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[7]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[6]);
        Fp.SquareNThenMultiply(out t[0], t[0], 7, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[5]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 5, t[4]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[3]);
        Fp.SquareNThenMultiply(out t[0], t[0], 6, t[2]);
        Fp.SquareNThenMultiply(out t[0], t[0], 4, t[1]);
        TSquare.Square(out t[0], t[0]);
        result = t[0];
    }
}
