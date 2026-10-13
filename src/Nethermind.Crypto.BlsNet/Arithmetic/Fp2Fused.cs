using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class Fp2Fused
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Subtract(out Fp2 r, in Fp2 a, in Fp2 b)
    {
        if (Fp.UseExpandedKernels) SubtractExpanded(out r, a, b);
        else Fp2.Subtract(out r, a, b);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SubtractExpanded(out Fp2 r, in Fp2 a, in Fp2 b)
    {
        Fp.Subtract(out r.C0, a.C0, b.C0);
        Fp.Subtract(out r.C1, a.C1, b.C1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractSum(out Fp2 r, in Fp2 a, in Fp2 b, in Fp2 c)
    {
        if (Fp.UseExpandedKernels) SubtractSumExpanded(out r, a, b, c);
        else { Fp2.Subtract(out Fp2 t, a, b); Fp2.Subtract(out r, t, c); }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SubtractSumExpanded(out Fp2 r, in Fp2 a, in Fp2 b, in Fp2 c)
    {
        Fp.SubtractSumComposite(out r.C0, a.C0, b.C0, c.C0);
        Fp.SubtractSumComposite(out r.C1, a.C1, b.C1, c.C1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplySum(out Fp2 result, in Fp2 a, in Fp2 b, in Fp2 c, in Fp2 d)
    {
        if (Fp.UseExpandedKernels) MultiplySumExpanded(out result, a, b, c, d);
        else { Fp2.Multiply(out Fp2 t0, a, b); Fp2.Multiply(out Fp2 t1, c, d); Fp2.Add(out result, t0, t1); }
    }

    // |real|<2p^2, 0<=imag<4p^2<pR. Read all inputs before writing result.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplySumExpanded(out Fp2 result, in Fp2 a, in Fp2 b, in Fp2 c, in Fp2 d)
    {
        WideTower.Multiply2(out Fp2Wide t0, a, b);
        WideTower.Multiply2(out Fp2Wide t1, c, d);
        WideTower.Add2(out t0, t0, t1);
        Fp.ReduceWideSmall(out result.C0, t0.C0);
        Fp.ReduceWidePositive(out result.C1, t0.C1);
    }

    // Two canonical Fp2 products, subtracted before the two final reductions.
    // Each component has magnitude below 2p^2 < pR. Inputs may alias output.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyDifference(out Fp2 result, in Fp2 a, in Fp2 b, in Fp2 c, in Fp2 d)
    {
        WideTower.Multiply2(out Fp2Wide t0, a, b);
        WideTower.Multiply2(out Fp2Wide t1, c, d);
        WideTower.Subtract2(out t0, t0, t1);
        WideTower.Reduce2Small(out result, t0);
    }

    // Canonical inputs. The real component uses a modular difference, so it
    // is congruent to a0^2-a1^2 and lies in [0,2p^2), not (-p^2,p^2).
    // Imaginary is exactly 2a0a1 in [0,2p^2). No reductions occur here.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SquareWide(out Fp2Wide result, in Fp2 a)
    {
        Fp.AddUnreduced(out Fp sum, a.C0, a.C1);
        Fp.SubtractComposite(out Fp difference, a.C0, a.C1);
        Fp.MultiplyWide(out result.C0, sum, difference);
        Fp.MultiplyWide(out result.C1, a.C0, a.C1);
        Fp.AddWide(out result.C1, result.C1, result.C1);
    }

}
