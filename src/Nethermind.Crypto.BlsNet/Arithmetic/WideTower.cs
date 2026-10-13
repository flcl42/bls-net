using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal struct Fp2Wide { internal FpWide C0, C1; }
internal struct Fp6Wide { internal Fp2Wide C0, C1, C2; }

// Lazy tower formulas adapted from blst fp12_tower.c (Apache-2.0).
// Copyright Supranational LLC; SPDX-License-Identifier: Apache-2.0.
// Unreduced Fp2 sums make imag=a0*b1+a1*b0: 0<=imag<2p^2, |real|<p^2.
// Interval propagation bounds Fp6 products by 8p^2, sparse products by 9p^2,
// and full Fp12 products by 22p^2. The first two fit |x|<pR; the last fits
// |x|<3pR. Products reduce once per output coordinate.
// Narrow inputs and wide output scratch buffers are distinct.
internal static class WideTower
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Multiply2(out Fp2Wide result, in Fp2 a, in Fp2 b)
    {
        if (Fp.UseExpandedKernels) Multiply2Expanded(out result, a, b);
        else Multiply2Compact(out result, a, b);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Multiply2Expanded(out Fp2Wide result, in Fp2 a, in Fp2 b)
    {
        Fp.MultiplyWide(out result.C0, a.C0, b.C0);
        Fp.MultiplyWide(out FpWide t1, a.C1, b.C1);
        Fp.AddUnreduced(out Fp sa, a.C0, a.C1);
        Fp.AddUnreduced(out Fp sb, b.C0, b.C1);
        Fp.MultiplyWide(out result.C1, sa, sb);
        Fp.SubtractWideProducts(ref result.C0, t1, ref result.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Multiply2Compact(out Fp2Wide result, in Fp2 a, in Fp2 b)
    {
        Fp.MultiplyWide(out FpWide t0, a.C0, b.C0);
        Fp.MultiplyWide(out FpWide t1, a.C1, b.C1);
        Fp.AddUnreduced(out Fp sa, a.C0, a.C1);
        Fp.AddUnreduced(out Fp sb, b.C0, b.C1);
        Fp.MultiplyWide(out FpWide t2, sa, sb);
        Fp.SubtractWide(out result.C0, t0, t1);
        Fp.SubtractWide(out t2, t2, t0);
        Fp.SubtractWide(out result.C1, t2, t1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Add2(out Fp2Wide result, in Fp2Wide a, in Fp2Wide b)
    {
        Fp.AddWideInline(out result.C0, a.C0, b.C0);
        Fp.AddWideInline(out result.C1, a.C1, b.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Subtract2(out Fp2Wide result, in Fp2Wide a, in Fp2Wide b)
    {
        Fp.SubtractWideInline(out result.C0, a.C0, b.C0);
        Fp.SubtractWideInline(out result.C1, a.C1, b.C1);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void NonResidue2(out Fp2Wide result, in Fp2Wide a)
    {
        Fp.AddWideInline(out FpWide c1, a.C0, a.C1);
        Fp.SubtractWideInline(out result.C0, a.C0, a.C1);
        result.C1 = c1;
    }

    [SkipLocalsInit]
    internal static void Reduce2(out Fp2 result, in Fp2Wide a)
    {
        Fp.ReduceWide(out result.C0, a.C0);
        Fp.ReduceWide(out result.C1, a.C1);
    }

    [SkipLocalsInit]
    internal static void Reduce6(out Fp6 result, in Fp6Wide a)
    {
        Reduce2(out result.C0, a.C0);
        Reduce2(out result.C1, a.C1);
        Reduce2(out result.C2, a.C2);
    }

    [SkipLocalsInit]
    internal static void Reduce2Small(out Fp2 result, in Fp2Wide a)
    {
        Fp.ReduceWideSmall(out result.C0, a.C0);
        Fp.ReduceWideSmall(out result.C1, a.C1);
    }

    [SkipLocalsInit]
    internal static void Reduce6Small(out Fp6 result, in Fp6Wide a)
    {
        Reduce2Small(out result.C0, a.C0);
        Reduce2Small(out result.C1, a.C1);
        Reduce2Small(out result.C2, a.C2);
    }

    [SkipLocalsInit]
    internal static void Subtract6(out Fp6Wide result, in Fp6Wide a, in Fp6Wide b)
    {
        Subtract2(out result.C0, a.C0, b.C0);
        Subtract2(out result.C1, a.C1, b.C1);
        Subtract2(out result.C2, a.C2, b.C2);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Multiply6(out Fp6Wide result, in Fp6 a, in Fp6 b)
    {
        Unsafe.SkipInit(out Fp2Wide t0);
        Unsafe.SkipInit(out Fp2Wide t1);
        Unsafe.SkipInit(out Fp2Wide t2);
        Unsafe.SkipInit(out Fp2Wide t3);
        Unsafe.SkipInit(out Fp2 t4);
        Unsafe.SkipInit(out Fp2 t5);
        Multiply2(out t0, a.C0, b.C0);
        Multiply2(out t1, a.C1, b.C1);
        Multiply2(out t2, a.C2, b.C2);
        Fp2.Add(out t4, a.C1, a.C2);
        Fp2.Add(out t5, b.C1, b.C2);
        Multiply2(out t3, t4, t5);
        Subtract2(out t3, t3, t1);
        Subtract2(out t3, t3, t2);
        NonResidue2(out t3, t3);
        Fp2.Add(out t4, a.C0, a.C1);
        Fp2.Add(out t5, b.C0, b.C1);
        Multiply2(out result.C1, t4, t5);
        Subtract2(out result.C1, result.C1, t0);
        Subtract2(out result.C1, result.C1, t1);
        NonResidue2(out Fp2Wide nr, t2);
        Add2(out result.C1, result.C1, nr);
        Fp2.Add(out t4, a.C0, a.C2);
        Fp2.Add(out t5, b.C0, b.C2);
        Multiply2(out result.C2, t4, t5);
        Subtract2(out result.C2, result.C2, t0);
        Subtract2(out result.C2, result.C2, t2);
        Add2(out result.C2, result.C2, t1);
        Add2(out result.C0, t3, t0);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyBy0Y06(out Fp6Wide result, in Fp6 a, in Fp2 b)
    {
        Unsafe.SkipInit(out Fp2Wide t);
        Multiply2(out t, a.C2, b);
        Multiply2(out result.C2, a.C1, b);
        Multiply2(out result.C1, a.C0, b);
        NonResidue2(out result.C0, t);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Square6(out Fp6 result, in Fp6 a)
    {
        Fp2Fused.SquareWide(out Fp2Wide s0, a.C0);
        Multiply2(out Fp2Wide m01, a.C0, a.C1); Add2(out m01, m01, m01);
        Multiply2(out Fp2Wide m12, a.C1, a.C2); Add2(out m12, m12, m12);
        Fp2Fused.SquareWide(out Fp2Wide s2, a.C2);
        Fp2.Add(out Fp2 sum, a.C0, a.C1); Fp2.Add(out sum, sum, a.C2);
        Fp2Fused.SquareWide(out Fp2Wide c2, sum);
        Subtract2(out c2, c2, s0); Subtract2(out c2, c2, s2);
        Subtract2(out c2, c2, m01); Subtract2(out c2, c2, m12);
        NonResidue2(out m12, m12); Add2(out s0, s0, m12);
        NonResidue2(out s2, s2); Add2(out m01, m01, s2);
        Reduce2Small(out result.C0, s0);
        Reduce2Small(out result.C1, m01);
        // C2 can be negative by up to 12p^2; use the full signed reducer.
        Reduce2(out result.C2, c2);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyByXY06(out Fp6Wide result, in Fp6 a, in Fp6 b)
    {
        Unsafe.SkipInit(out Fp2Wide t0);
        Unsafe.SkipInit(out Fp2Wide t1);
        Unsafe.SkipInit(out Fp2Wide t3);
        Unsafe.SkipInit(out Fp2 t4);
        Unsafe.SkipInit(out Fp2 t5);
        Multiply2(out t0, a.C0, b.C0);
        Multiply2(out t1, a.C1, b.C1);
        Multiply2(out t3, a.C2, b.C1);
        NonResidue2(out t3, t3);
        Fp2.Add(out t4, a.C0, a.C1);
        Fp2.Add(out t5, b.C0, b.C1);
        Multiply2(out result.C1, t4, t5);
        Subtract2(out result.C1, result.C1, t0);
        Subtract2(out result.C1, result.C1, t1);
        Multiply2(out result.C2, a.C2, b.C0);
        Add2(out result.C2, result.C2, t1);
        Add2(out result.C0, t3, t0);
    }

    [SkipLocalsInit]
    internal static void Multiply12(out Fp12 result, in Fp12 a, in Fp12 b)
    {
        Unsafe.SkipInit(out Fp6Wide t0);
        Unsafe.SkipInit(out Fp6Wide t1);
        Unsafe.SkipInit(out Fp6 t2);
        Multiply6(out t0, a.C0, b.C0);
        Multiply6(out t1, a.C1, b.C1);
        Fp6.Add(out t2, a.C0, a.C1);
        Fp6.Add(out Fp6 sum, b.C0, b.C1);
        Multiply6(out Fp6Wide rr, sum, t2);
        Subtract6(out rr, rr, t0);
        Subtract6(out rr, rr, t1);
        Reduce6(out result.C1, rr);
        NonResidue2(out t1.C2, t1.C2);
        Add2(out rr.C0, t0.C0, t1.C2);
        Add2(out rr.C1, t0.C1, t1.C0);
        Add2(out rr.C2, t0.C2, t1.C1);
        Reduce6(out result.C0, rr);
    }

    [SkipLocalsInit]
    internal static void MultiplyBySparse12(out Fp12 result, in Fp12 a, in Fp6 xy00z0)
    {
        Unsafe.SkipInit(out Fp6Wide t0);
        Unsafe.SkipInit(out Fp6Wide t1);
        Unsafe.SkipInit(out Fp6 t2);
        MultiplyByXY06(out t0, a.C0, xy00z0);
        MultiplyBy0Y06(out t1, a.C1, xy00z0.C2);
        t2.C0 = xy00z0.C0;
        Fp2.Add(out t2.C1, xy00z0.C1, xy00z0.C2);
        Fp6.Add(out Fp6 sum, a.C0, a.C1);
        MultiplyByXY06(out Fp6Wide rr, sum, t2);
        Subtract6(out rr, rr, t0);
        Subtract6(out rr, rr, t1);
        Reduce6Small(out result.C1, rr);
        NonResidue2(out t1.C2, t1.C2);
        Add2(out rr.C0, t0.C0, t1.C2);
        Add2(out rr.C1, t0.C1, t1.C0);
        Add2(out rr.C2, t0.C2, t1.C1);
        Reduce6Small(out result.C0, rr);
    }
}
