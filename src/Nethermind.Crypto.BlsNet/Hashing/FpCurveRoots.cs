// Addition chain adapted from blst sqrt-addchain.h (Apache-2.0).
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
        for (int i = 0; i < 12; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[15]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[1]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[6]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[11]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 2; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[9]);
        for (int i = 0; i < 3; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[14]);
        for (int i = 0; i < 3; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[13]);
        for (int i = 0; i < 8; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[11]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[12]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[5]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[9]);
        for (int i = 0; i < 8; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[5]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[11]);
        for (int i = 0; i < 9; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[10]);
        for (int i = 0; i < 2; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[6]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[1]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[9]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[11]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[5]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[10]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[10]);
        for (int i = 0; i < 8; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[2]);
        for (int i = 0; i < 9; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 3; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 8; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 3; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[9]);
        for (int i = 0; i < 9; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[2]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 3; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[8]);
        for (int i = 0; i < 8; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[2]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[7]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[6]);
        for (int i = 0; i < 7; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[5]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 5; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[4]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[3]);
        for (int i = 0; i < 6; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[2]);
        for (int i = 0; i < 4; i++) TSquare.Square(out t[0], t[0]);
        Fp.MultiplyComposite(out t[0], t[0], t[1]);
        TSquare.Square(out t[0], t[0]);
        result = t[0];
    }
}
