using System.Runtime.CompilerServices;

// Pairing formulas adapted from blst pairing.c.
// Copyright Supranational LLC; licensed under Apache-2.0.

using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Pairing;

/// <summary>Variable-time BLS12-381 pairing for public, subgroup-checked inputs.</summary>
public static class Pairing
{
    private struct Jacobian { public Fp2 X, Y, Z; }

    [SkipLocalsInit]
    private static void LineDouble(out Fp6 line, ref Jacobian t, in Jacobian q)
    {
        Unsafe.SkipInit(out Jacobian ret);
        Unsafe.SkipInit(out Fp2 ZZ);
        Unsafe.SkipInit(out Fp2 A);
        Unsafe.SkipInit(out Fp2 B);
        Unsafe.SkipInit(out Fp2 C);
        Unsafe.SkipInit(out Fp2 D);
        Unsafe.SkipInit(out Fp2 E);
        Unsafe.SkipInit(out Fp2 F);
        Fp2.Square(out A, q.X);
        Fp2.Square(out B, q.Y);
        Fp2.Square(out ZZ, q.Z);
        Fp2.Square(out C, B);
        Fp2.Add(out D, q.X, B);
        Fp2.Square(out D, D);
        Fp2Fused.SubtractSum(out D, D, A, C);
        Fp2.Add(out D, D, D);
        Fp2.MulBy3(out E, A);
        Fp2.Square(out F, E);
        Fp2.Add(out line.C0, E, q.X);
        Fp2Fused.SubtractSum(out ret.X, F, D, D);
        Fp2.Add(out ret.Z, q.Y, q.Z);
        Fp2.Square(out ret.Z, ret.Z);
        Fp2Fused.SubtractSum(out ret.Z, ret.Z, B, ZZ);
        Fp2.MulBy8(out C, C);
        Fp2Fused.Subtract(out ret.Y, D, ret.X);
        Fp2.Multiply(out ret.Y, ret.Y, E);
        Fp2Fused.Subtract(out ret.Y, ret.Y, C);
        Fp2.Square(out line.C0, line.C0);
        Fp2Fused.SubtractSum(out line.C0, line.C0, A, F);
        Fp2.MulBy4(out B, B);
        Fp2Fused.Subtract(out line.C0, line.C0, B);
        Fp2.Multiply(out line.C1, E, ZZ);
        Fp2.Multiply(out line.C2, ret.Z, ZZ);
        t = ret;
    }

    [SkipLocalsInit]
    private static void LineAdd(out Fp6 line, ref Jacobian t, in Jacobian r, in G2Affine q)
    {
        Unsafe.SkipInit(out Jacobian ret);
        Unsafe.SkipInit(out Fp2 Z1Z1);
        Unsafe.SkipInit(out Fp2 U2);
        Unsafe.SkipInit(out Fp2 S2);
        Unsafe.SkipInit(out Fp2 H);
        Unsafe.SkipInit(out Fp2 HH);
        Unsafe.SkipInit(out Fp2 I);
        Unsafe.SkipInit(out Fp2 J);
        Unsafe.SkipInit(out Fp2 V);
        Fp2.Square(out Z1Z1, r.Z);
        Fp2.Multiply(out U2, q.X, Z1Z1);
        Fp2.Multiply(out S2, q.Y, r.Z);
        Fp2.Multiply(out S2, S2, Z1Z1);
        Fp2Fused.Subtract(out H, U2, r.X);
        Fp2.Square(out HH, H);
        Fp2.Add(out I, HH, HH);
        Fp2.Add(out I, I, I);
        Fp2.Multiply(out J, H, I);
        Fp2Fused.Subtract(out line.C1, S2, r.Y);
        Fp2.Add(out line.C1, line.C1, line.C1);
        Fp2.Multiply(out V, r.X, I);
        Fp2.Square(out ret.X, line.C1);
        Fp2Fused.Subtract(out ret.X, ret.X, J);
        Fp2Fused.Subtract(out ret.X, ret.X, V);
        Fp2Fused.Subtract(out ret.X, ret.X, V);
        Fp2.Multiply(out J, J, r.Y);
        Fp2Fused.Subtract(out ret.Y, V, ret.X);
        Fp2.Multiply(out ret.Y, ret.Y, line.C1);
        Fp2Fused.SubtractSum(out ret.Y, ret.Y, J, J);
        Fp2.Add(out ret.Z, r.Z, H);
        Fp2.Square(out ret.Z, ret.Z);
        Fp2Fused.SubtractSum(out ret.Z, ret.Z, Z1Z1, HH);
        if (Fp.UseExpandedKernels) Fp2Fused.MultiplyDifference(out I, line.C1, q.X, q.Y, ret.Z);
        else
        {
            Fp2.Multiply(out I, line.C1, q.X); Fp2.Multiply(out J, q.Y, ret.Z);
            Fp2.Subtract(out I, I, J);
        }
        Fp2.Add(out line.C0, I, I);
        line.C2 = ret.Z;
        t = ret;
    }

    [SkipLocalsInit]
    private static void Evaluate(ref Fp6 line, in G1Affine p)
    {
        Fp2.MultiplyByFp(out line.C1, line.C1, p.X);
        Fp2.MultiplyByFp(out line.C2, line.C2, p.Y);
    }

    /// <summary>Batched variable-time Miller loop. Infinity pairs contribute one.</summary>
    [SkipLocalsInit]
    public static void MillerLoop(ReadOnlySpan<G1Affine> p, ReadOnlySpan<G2Affine> q, out Fp12 f)
    {
        if (p.Length != q.Length) throw new ArgumentException("Pair counts must match.");
        int batchSize = Math.Min(64, p.Length);
        Span<Jacobian> t = stackalloc Jacobian[batchSize];
        Span<G2Affine> qs = stackalloc G2Affine[batchSize];
        Span<G1Affine> px2 = stackalloc G1Affine[batchSize];
        Fp12 acc = Fp12.One;
        bool hasBatch = false;
        int count = 0;
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i].IsInfinity || q[i].IsInfinity) continue;
            qs[count] = q[i];
            t[count] = new Jacobian { X = q[i].X, Y = q[i].Y, Z = Fp2.One };
            Fp.Double(out px2[count].X, p[i].X);
            Fp.Negate(out px2[count].X, px2[count].X);
            Fp.Double(out px2[count].Y, p[i].Y);
            count++;
            if (count == batchSize)
            {
                LoopBatch(out Fp12 batch, t, qs, px2, count);
                if (!hasBatch) { acc = batch; hasBatch = true; }
                else Fp12.Multiply(out acc, acc, batch);
                count = 0;
            }
        }
        if (count != 0)
        {
            LoopBatch(out Fp12 batch, t, qs, px2, count);
            if (!hasBatch) acc = batch;
            else Fp12.Multiply(out acc, acc, batch);
        }
        f = acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoopBatch(out Fp12 f, Span<Jacobian> t, ReadOnlySpan<G2Affine> q, ReadOnlySpan<G1Affine> p, int count)
    {
        if (Fp.UseExpandedKernels && count >= 2) LoopBatchPaired(out f, t, q, p, count);
        else LoopBatchSingle(out f, t, q, p, count);
    }

    [SkipLocalsInit]
    private static void LoopBatchSingle(out Fp12 f, Span<Jacobian> t, ReadOnlySpan<G2Affine> q, ReadOnlySpan<G1Affine> p, int count)
    {
        LineDouble(out Fp6 line, ref t[0], t[0]);
        Evaluate(ref line, p[0]);
        Fp12 acc = new() { C0 = new Fp6 { C0 = line.C0, C1 = line.C1 }, C1 = new Fp6 { C1 = line.C2 } };
        for (int i = 1; i < count; i++)
        {
            LineDouble(out line, ref t[i], t[i]);
            Evaluate(ref line, p[i]);
            Fp12.MultiplyBySparse(out acc, acc, line);
        }
        ReadOnlySpan<int> runs = [2, 3, 9, 32, 16];
        foreach (int run in runs)
        {
            for (int i = 0; i < count; i++)
            {
                LineAdd(out line, ref t[i], t[i], q[i]);
                Evaluate(ref line, p[i]);
                Fp12.MultiplyBySparse(out acc, acc, line);
            }
            for (int k = 0; k < run; k++)
            {
                Fp12.Square(out acc, acc);
                for (int i = 0; i < count; i++)
                {
                    LineDouble(out line, ref t[i], t[i]);
                    Evaluate(ref line, p[i]);
                    Fp12.MultiplyBySparse(out acc, acc, line);
                }
            }
        }
        Fp12.Conjugate(out f, acc);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void LoopBatchPaired(out Fp12 f, Span<Jacobian> t, ReadOnlySpan<G2Affine> q, ReadOnlySpan<G1Affine> p, int count)
    {
        t = t[..count];
        LineDouble(out Fp6 line0, ref t[0], t[0]); Evaluate(ref line0, p[0]);
        LineDouble(out Fp6 line1, ref t[1], t[1]); Evaluate(ref line1, p[1]);
        SparseTower.LineProduct(out Fp12 acc, line0, line1);
        DoubleLines(ref acc, t, p, 2);
        ReadOnlySpan<int> runs = [2, 3, 9, 32, 16];
        foreach (int run in runs)
        {
            int i = 0;
            for (; i + 1 < count; i += 2)
            {
                LineAdd(out line0, ref t[i], t[i], q[i]); Evaluate(ref line0, p[i]);
                LineAdd(out line1, ref t[i + 1], t[i + 1], q[i + 1]); Evaluate(ref line1, p[i + 1]);
                SparseTower.MultiplyPair(out acc, acc, line0, line1);
            }
            if (i < count)
            {
                LineAdd(out line0, ref t[i], t[i], q[i]); Evaluate(ref line0, p[i]);
                Fp12.MultiplyBySparse(out acc, acc, line0);
            }
            for (int k = 0; k < run; k++)
            {
                Fp12.Square(out acc, acc); DoubleLines(ref acc, t, p, 0);
            }
        }
        Fp12.Conjugate(out f, acc);
    }

    [SkipLocalsInit]
    private static void DoubleLines(ref Fp12 acc, Span<Jacobian> t, ReadOnlySpan<G1Affine> p, int start)
    {
        int i = start;
        for (; i + 1 < t.Length; i += 2)
        {
            LineDouble(out Fp6 line0, ref t[i], t[i]); Evaluate(ref line0, p[i]);
            LineDouble(out Fp6 line1, ref t[i + 1], t[i + 1]); Evaluate(ref line1, p[i + 1]);
            SparseTower.MultiplyPair(out acc, acc, line0, line1);
        }
        if (i < t.Length)
        {
            LineDouble(out Fp6 line, ref t[i], t[i]); Evaluate(ref line, p[i]);
            Fp12.MultiplyBySparse(out acc, acc, line);
        }
    }

    [SkipLocalsInit]
    private static void RaiseToHalfZ(out Fp12 result, in Fp12 a)
    {
        Fp12.CyclotomicSquare(out Fp12 ret, a);
        ReadOnlySpan<int> runs = [2, 3, 9, 32, 15];
        foreach (int run in runs)
        {
            Fp12.Multiply(out ret, ret, a);
            Fp12.CyclotomicSquareRepeated(out ret, ret, run);
        }
        Fp12.Conjugate(out result, ret);
    }

    [SkipLocalsInit]
    private static void RaiseToZ(out Fp12 result, in Fp12 a)
    {
        RaiseToHalfZ(out Fp12 ret, a);
        Fp12.CyclotomicSquare(out result, ret);
    }

    /// <summary>Variable-time final exponentiation, matching blst's final_exp.</summary>
    [SkipLocalsInit]
    public static void FinalExponentiation(in Fp12 f, out Fp12 result)
    {
        Unsafe.SkipInit(out Fp12 ret);
        Unsafe.SkipInit(out Fp12 y0);
        Unsafe.SkipInit(out Fp12 y1);
        Unsafe.SkipInit(out Fp12 y2);
        Unsafe.SkipInit(out Fp12 y3);
        y1 = f;
        Fp12.Conjugate(out y1, y1);
        Fp12.Inverse(out y2, f);
        Fp12.Multiply(out ret, y1, y2);
        Fp12.FrobeniusMap(out y2, ret, 2);
        Fp12.Multiply(out ret, ret, y2);
        Fp12.CyclotomicSquare(out y0, ret);
        RaiseToZ(out y1, y0);
        RaiseToHalfZ(out y2, y1);
        y3 = ret;
        Fp12.Conjugate(out y3, y3);
        Fp12.Multiply(out y1, y1, y3);
        Fp12.Conjugate(out y1, y1);
        Fp12.Multiply(out y1, y1, y2);
        RaiseToZ(out y2, y1);
        RaiseToZ(out y3, y2);
        Fp12.Conjugate(out y1, y1);
        Fp12.Multiply(out y3, y3, y1);
        Fp12.Conjugate(out y1, y1);
        Fp12.FrobeniusMap(out y1, y1, 3);
        Fp12.FrobeniusMap(out y2, y2, 2);
        Fp12.Multiply(out y1, y1, y2);
        RaiseToZ(out y2, y3);
        Fp12.Multiply(out y2, y2, y0);
        Fp12.Multiply(out y2, y2, ret);
        Fp12.Multiply(out y1, y1, y2);
        Fp12.FrobeniusMap(out y2, y3, 1);
        Fp12.Multiply(out ret, y1, y2);
        result = ret;
    }

    /// <summary>Variable-time product pairing check; callers must validate curves and subgroups.</summary>
    public static bool PairingCheck(ReadOnlySpan<G1Affine> p, ReadOnlySpan<G2Affine> q)
    {
        MillerLoop(p, q, out Fp12 f);
        if (f.IsOne) return true;
        FinalExponentiation(f, out Fp12 result);
        return result.IsOne;
    }
}
