using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

// A line is x+y*v+z*v*w. Two lines have a product with C1.C0=0.
// Forming that product (18 raw products/10 reductions), then multiplying
// by its five coefficients (51/12), replaces two sparse multiplies (78/24).
internal static class SparseTower
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MultiplyPair(out Fp12 r, in Fp12 a, in Fp6 line0, in Fp6 line1)
    {
        if (Fp.UseExpandedKernels) MultiplyPairExpanded(out r, a, line0, line1);
        else
        {
            Fp12.MultiplyBySparse(out Fp12 t, a, line0);
            Fp12.MultiplyBySparse(out r, t, line1);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyPairExpanded(out Fp12 r, in Fp12 a, in Fp6 line0, in Fp6 line1)
    {
        LineProduct(out Fp12 product, line0, line1);
        MultiplyFive(out r, a, product);
    }

    // Canonical inputs, including output/input aliases. Cross terms are bounded
    // by 4p^2; the non-residue combination by 5p^2. All fit the pR reducer.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void LineProduct(out Fp12 r, in Fp6 a, in Fp6 b)
    {
        WideTower.Multiply2(out Fp2Wide t0, a.C0, b.C0);
        WideTower.Multiply2(out Fp2Wide t1, a.C1, b.C1);
        WideTower.Multiply2(out Fp2Wide t2, a.C2, b.C2);
        Cross(out Fp2Wide xy, a.C0, a.C1, b.C0, b.C1, t0, t1);
        Cross(out Fp2Wide xz, a.C0, a.C2, b.C0, b.C2, t0, t2);
        Cross(out Fp2Wide yz, a.C1, a.C2, b.C1, b.C2, t1, t2);
        WideTower.NonResidue2(out t2, t2); WideTower.Add2(out t0, t0, t2);
        WideTower.Reduce2Small(out r.C0.C0, t0);
        WideTower.Reduce2Small(out r.C0.C1, xy);
        WideTower.Reduce2Small(out r.C0.C2, t1);
        r.C1.C0 = default;
        WideTower.Reduce2Small(out r.C1.C1, xz);
        WideTower.Reduce2Small(out r.C1.C2, yz);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Cross(out Fp2Wide r, in Fp2 a0, in Fp2 a1, in Fp2 b0, in Fp2 b1, in Fp2Wide p0, in Fp2Wide p1)
    {
        Fp2.Add(out Fp2 sa, a0, a1); Fp2.Add(out Fp2 sb, b0, b1);
        WideTower.Multiply2(out r, sa, sb);
        WideTower.Subtract2(out r, r, p0); WideTower.Subtract2(out r, r, p1);
    }

    // a*(b1*v+b2*v^2), with five wide Fp2 products. The largest signed
    // coefficient has magnitude below 7p^2, within the original tower bounds.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyYZ6(out Fp6Wide r, in Fp6 a, in Fp6 b)
    {
        WideTower.Multiply2(out Fp2Wide t0, a.C1, b.C1);
        WideTower.Multiply2(out Fp2Wide t1, a.C2, b.C2);
        Cross(out r.C0, a.C1, a.C2, b.C1, b.C2, t0, t1);
        WideTower.NonResidue2(out r.C0, r.C0);
        WideTower.Multiply2(out r.C1, a.C0, b.C1);
        WideTower.NonResidue2(out t1, t1); WideTower.Add2(out r.C1, r.C1, t1);
        WideTower.Multiply2(out r.C2, a.C0, b.C2); WideTower.Add2(out r.C2, r.C2, t0);
    }

    // b.C1.C0 must be zero. Remaining inputs are canonical, and aliases are
    // permitted. The full signed bound is no larger than Multiply12's 22p^2.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyFive(out Fp12 r, in Fp12 a, in Fp12 b)
    {
        WideTower.Multiply6(out Fp6Wide t0, a.C0, b.C0);
        MultiplyYZ6(out Fp6Wide t1, a.C1, b.C1);
        Fp6.Add(out Fp6 sa, a.C0, a.C1); Fp6.Add(out Fp6 sb, b.C0, b.C1);
        WideTower.Multiply6(out Fp6Wide cross, sa, sb);
        WideTower.Subtract6(out cross, cross, t0); WideTower.Subtract6(out cross, cross, t1);
        WideTower.Reduce6(out r.C1, cross);
        WideTower.NonResidue2(out t1.C2, t1.C2);
        WideTower.Add2(out cross.C0, t0.C0, t1.C2);
        WideTower.Add2(out cross.C1, t0.C1, t1.C0);
        WideTower.Add2(out cross.C2, t0.C2, t1.C1);
        WideTower.Reduce6(out r.C0, cross);
    }
}
