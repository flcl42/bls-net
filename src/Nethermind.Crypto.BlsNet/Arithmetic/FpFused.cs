using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class FpFused
{
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Square(out Fp r, in Fp a) => Fp.Square(out r, a);

    // Canonical inputs: |a*b-c*d| < p^2 < pR; consume all inputs before output.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyDifference(out Fp r, in Fp a, in Fp b, in Fp c, in Fp d)
    {
        Fp.MultiplyWide(out FpWide t0, a, b); Fp.MultiplyWide(out FpWide t1, c, d);
        Fp.ReduceWideDifference(out r, t0, t1);
    }

    // Canonical inputs: 0 <= a*b+c*d < 2p^2 < pR.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplySum(out Fp r, in Fp a, in Fp b, in Fp c, in Fp d)
    {
        Fp.MultiplyWide(out FpWide t0, a, b); Fp.MultiplyWide(out FpWide t1, c, d);
        Fp.AddWide(out t0, t0, t1); Fp.ReduceWidePositive(out r, t0);
    }
}
