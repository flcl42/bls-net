using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

/// <summary>Variable-time MSM with contiguous 32-byte little-endian scalars.</summary>
public static class MultiScalarMultiplication
{
    /// <summary>Multiplies and sums G1 subgroup points; variable-time. Infinity contributes zero.</summary>
    public static void MultiScalarMultiply(ReadOnlySpan<G1Affine> points, ReadOnlySpan<byte> scalars, out G1Projective r)
    {
        Validate(points.Length, scalars);
        if (points.Length == 1) { ScalarMultiplication.Multiply(points[0], scalars, out r); return; }
        Msm<Fp>.Multiply(MemoryMarshal.Cast<G1Affine, Affine<Fp>>(points), scalars, out r.Value);
    }

    /// <summary>Multiplies and sums G2 subgroup points; variable-time. Infinity contributes zero.</summary>
    public static void MultiScalarMultiply(ReadOnlySpan<G2Affine> points, ReadOnlySpan<byte> scalars, out G2Projective r)
    {
        Validate(points.Length, scalars);
        if (points.Length == 1) { ScalarMultiplication.Multiply(points[0], scalars, out r); return; }
        Msm<Fp2>.Multiply(MemoryMarshal.Cast<G2Affine, Affine<Fp2>>(points), scalars, out r.Value);
    }

    private static void Validate(int count, ReadOnlySpan<byte> scalars)
    {
        if ((long)count * 32 != scalars.Length)
            throw new ArgumentException("Provide exactly 32 scalar bytes for each point.", nameof(scalars));
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Affine<T> where T : unmanaged, IField<T>
{
    public T X, Y;
}

internal static class Msm<T> where T : unmanaged, IField<T>
{
    internal static void Multiply(ReadOnlySpan<Affine<T>> points, ReadOnlySpan<byte> scalars, out Jacobian<T> r)
    {
        r = default; if (points.IsEmpty) return;
        if (points.Length < 32) { Small(points, scalars, out r); return; }
        int log = BitOperations.Log2((uint)points.Length);
        int w = log > 12 ? log - 3 : log > 8 ? log - 2 : log > 4 ? log - 1 : 2;
        int count = 1 << (w - 1);
        // .NET 10's Fp2 carry kernels favor XYZZ; newer JITs benefit from affine batches.
        int batchThreshold = typeof(T) == typeof(Fp2) ? (Fp.UseExpandedKernels ? 128 : int.MaxValue) : 512;
        if (points.Length >= batchThreshold)
        {
            int batchWindow = Math.Max(2, w - 1);
            BatchAffine(points, scalars, 1 << (batchWindow - 1), batchWindow, out r);
            return;
        }
        Xyzz<T>[] rented = ArrayPool<Xyzz<T>>.Shared.Rent(count);
        try
        {
            Span<Xyzz<T>> buckets = rented.AsSpan(0, count);
            for (int bit = (256 / w) * w; bit >= 0; bit -= w)
            {
                for (int j = 0; j < w; j++) Jacobian<T>.Double(out r, r);
                buckets.Clear();
                for (int i = 0; i < points.Length; i++)
                {
                    int digit = Booth(scalars.Slice(i * 32, 32), bit, w);
                    if (digit != 0)
                    {
                        ref Xyzz<T> b = ref buckets[Math.Abs(digit) - 1];
                        Xyzz<T>.AddAffine(out b, b, points[i].X, points[i].Y, digit < 0);
                    }
                }
                Xyzz<T> acc = default, sum = default;
                for (int i = count - 1; i >= 0; i--)
                { Xyzz<T>.Add(out acc, acc, buckets[i]); Xyzz<T>.Add(out sum, sum, acc); }
                sum.ToJacobian(out Jacobian<T> tile); Jacobian<T>.Add(out r, r, tile);
            }
        }
        finally { ArrayPool<Xyzz<T>>.Shared.Return(rented); }
    }

    // Group signed points by window digit, then reduce each bucket in parallel rounds.
    // One Montgomery batch inversion covers all affine slopes in each round.
    private static void BatchAffine(ReadOnlySpan<Affine<T>> points, ReadOnlySpan<byte> scalars,
        int bucketCount, int window, out Jacobian<T> result)
    {
        int n = points.Length, maxPairs = (n + 1) / 2;
        Affine<T>[] wa = ArrayPool<Affine<T>>.Shared.Rent(n), wb = ArrayPool<Affine<T>>.Shared.Rent(n);
        int[] sa = ArrayPool<int>.Shared.Rent(bucketCount), sb = ArrayPool<int>.Shared.Rent(bucketCount);
        int[] ca = ArrayPool<int>.Shared.Rent(bucketCount), cb = ArrayPool<int>.Shared.Rent(bucketCount);
        int[] cursors = ArrayPool<int>.Shared.Rent(bucketCount);
        int[] left = ArrayPool<int>.Shared.Rent(maxPairs), right = ArrayPool<int>.Shared.Rent(maxPairs), dest = ArrayPool<int>.Shared.Rent(maxPairs);
        byte[] kind = ArrayPool<byte>.Shared.Rent(maxPairs);
        T[] den = ArrayPool<T>.Shared.Rent(maxPairs), num = ArrayPool<T>.Shared.Rent(maxPairs), prefix = ArrayPool<T>.Shared.Rent(maxPairs);
        try
        {
            Span<Affine<T>> a = wa.AsSpan(0, n), b = wb.AsSpan(0, n);
            Span<int> starts = sa.AsSpan(0, bucketCount), nextStarts = sb.AsSpan(0, bucketCount);
            Span<int> counts = ca.AsSpan(0, bucketCount), nextCounts = cb.AsSpan(0, bucketCount);
            Span<int> cursor = cursors.AsSpan(0, bucketCount);
            Span<T> denominators = den.AsSpan(0, maxPairs), numerators = num.AsSpan(0, maxPairs), prefixes = prefix.AsSpan(0, maxPairs);
            result = default;
            for (int bit = (256 / window) * window; bit >= 0; bit -= window)
            {
                for (int j = 0; j < window; j++) Jacobian<T>.Double(out result, result);
                counts.Clear();
                for (int i = 0; i < n; i++)
                {
                    int d = Booth(scalars.Slice(i * 32, 32), bit, window);
                    if (d != 0 && !IsInfinity(points[i])) counts[Math.Abs(d) - 1]++;
                }
                PrefixBuckets(counts, starts); starts.CopyTo(cursor);
                for (int i = 0; i < n; i++)
                {
                    int d = Booth(scalars.Slice(i * 32, 32), bit, window);
                    if (d == 0 || IsInfinity(points[i])) continue;
                    T y = points[i].Y; if (d < 0) T.Negate(out y, y);
                    a[cursor[Math.Abs(d) - 1]++] = new() { X = points[i].X, Y = y };
                }

                int active = n;
                while (active > 0)
                {
                    bool reduce = false;
                    for (int k = 0; k < bucketCount; k++)
                    { nextCounts[k] = (counts[k] + 1) >> 1; reduce |= counts[k] > 1; }
                    if (!reduce) break;
                    PrefixBuckets(nextCounts, nextStarts);
                    int pairCount = 0;
                    for (int k = 0; k < bucketCount; k++)
                    {
                        int src = starts[k], remain = counts[k], dst = nextStarts[k];
                        while (remain >= 2)
                        {
                            int li = src++, ri = src++; remain -= 2;
                            Affine<T> p = a[li], q = a[ri]; int output = dst++;
                            if (IsInfinity(p)) { b[output] = q; continue; }
                            if (IsInfinity(q)) { b[output] = p; continue; }
                            T.Subtract(out T dx, q.X, p.X); T.Subtract(out T dy, q.Y, p.Y);
                            byte op;
                            if (T.EqualsZero(dx))
                            {
                                if (!T.EqualsZero(dy)) { b[output] = default; continue; }
                                T.Double(out denominators[pairCount], p.Y);
                                if (T.EqualsZero(denominators[pairCount])) { b[output] = default; continue; }
                                T.Square(out T xx, p.X); T.MulBy3(out numerators[pairCount], xx); op = 2;
                            }
                            else { denominators[pairCount] = dx; numerators[pairCount] = dy; op = 1; }
                            left[pairCount] = li; right[pairCount] = ri; dest[pairCount] = output; kind[pairCount] = op; pairCount++;
                        }
                        if (remain != 0) b[dst] = a[src];
                    }
                    BatchInvert(denominators[..pairCount], prefixes[..pairCount]);
                    for (int i = 0; i < pairCount; i++)
                    {
                        Affine<T> p = a[left[i]], q = a[right[i]];
                        CurveField<T>.Multiply(out T lambda, numerators[i], denominators[i]);
                        CurveField<T>.Square(out T x, lambda);
                        if (kind[i] == 1) { CurveField<T>.Subtract(out x, x, p.X); CurveField<T>.Subtract(out x, x, q.X); }
                        else { CurveField<T>.Double(out T twice, p.X); CurveField<T>.Subtract(out x, x, twice); }
                        CurveField<T>.Subtract(out T y, p.X, x); CurveField<T>.Multiply(out y, lambda, y); CurveField<T>.Subtract(out y, y, p.Y);
                        b[dest[i]] = new() { X = x, Y = y };
                    }
                    active = 0; for (int k = 0; k < bucketCount; k++) active += nextCounts[k];
                    Span<Affine<T>> tp = a; a = b; b = tp;
                    Span<int> ti = starts; starts = nextStarts; nextStarts = ti;
                    ti = counts; counts = nextCounts; nextCounts = ti;
                }

                Xyzz<T> acc = default, sum = default;
                for (int k = bucketCount - 1; k >= 0; k--)
                {
                    Xyzz<T> p = counts[k] == 0 ? default : new Xyzz<T>
                    { X = a[starts[k]].X, Y = a[starts[k]].Y, ZZ = T.One, ZZZ = T.One };
                    Xyzz<T>.Add(out acc, acc, p); Xyzz<T>.Add(out sum, sum, acc);
                }
                sum.ToJacobian(out Jacobian<T> tile); Jacobian<T>.Add(out result, result, tile);
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(prefix); ArrayPool<T>.Shared.Return(num); ArrayPool<T>.Shared.Return(den);
            ArrayPool<byte>.Shared.Return(kind); ArrayPool<int>.Shared.Return(dest); ArrayPool<int>.Shared.Return(right); ArrayPool<int>.Shared.Return(left);
            ArrayPool<int>.Shared.Return(cursors); ArrayPool<int>.Shared.Return(cb); ArrayPool<int>.Shared.Return(ca);
            ArrayPool<int>.Shared.Return(sb); ArrayPool<int>.Shared.Return(sa); ArrayPool<Affine<T>>.Shared.Return(wb); ArrayPool<Affine<T>>.Shared.Return(wa);
        }
    }

    private static bool IsInfinity(in Affine<T> p) => T.EqualsZero(p.X) && T.EqualsZero(p.Y);

    private static void PrefixBuckets(ReadOnlySpan<int> counts, Span<int> starts)
    {
        int total = 0;
        for (int i = 0; i < counts.Length; i++) { starts[i] = total; total += counts[i]; }
    }

    private static void BatchInvert(Span<T> values, Span<T> prefixes)
    {
        T product = T.One;
        for (int i = 0; i < values.Length; i++)
        { prefixes[i] = product; if (!T.EqualsZero(values[i])) CurveField<T>.Multiply(out product, product, values[i]); }
        CurveField<T>.Inverse(out product, product);
        for (int i = values.Length - 1; i >= 0; i--)
        {
            if (T.EqualsZero(values[i])) continue;
            CurveField<T>.Multiply(out T inverse, product, prefixes[i]);
            CurveField<T>.Multiply(out product, product, values[i]); values[i] = inverse;
        }
    }

    private static void Small(ReadOnlySpan<Affine<T>> points, ReadOnlySpan<byte> scalars, out Jacobian<T> r)
    {
        const int w = 4, entries = 8;
        int length = points.Length * entries;
        Jacobian<T>[] rented = ArrayPool<Jacobian<T>>.Shared.Rent(length);
        T[] products = ArrayPool<T>.Shared.Rent(length);
        try
        {
            Span<Jacobian<T>> table = rented.AsSpan(0, length);
            for (int j = 0; j < points.Length; j++)
            {
                int row = j * entries;
                Jacobian<T>.FromAffine(out table[row], points[j].X, points[j].Y);
                for (int i = 1; i < entries; i++)
                    Jacobian<T>.AddAffine(out table[row + i], table[row + i - 1], points[j].X, points[j].Y);
            }
            Jacobian<T>.BatchToAffine(table, products.AsSpan(0, length)); r = default;
            for (int bit = 256; bit >= 0; bit -= w)
            {
                for (int j = 0; j < w; j++) Jacobian<T>.Double(out r, r);
                for (int j = 0; j < points.Length; j++)
                {
                    int digit = Booth(scalars.Slice(j * 32, 32), bit, w); if (digit == 0) continue;
                    ref Jacobian<T> p = ref table[j * entries + Math.Abs(digit) - 1];
                    T y = p.Y; if (digit < 0) T.Negate(out y, y);
                    Jacobian<T>.AddAffine(out r, r, p.X, y);
                }
            }
        }
        finally { ArrayPool<T>.Shared.Return(products); ArrayPool<Jacobian<T>>.Shared.Return(rented); }
    }

    private static int Booth(ReadOnlySpan<byte> s, int bit, int w)
    {
        int start = bit - 1;
        uint word;
        if (start < 0) word = BinaryPrimitives.ReadUInt32LittleEndian(s) << 1;
        else
        {
            int byteIndex = start >> 3;
            if (byteIndex <= 28) word = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(byteIndex, 4));
            else
            {
                word = 0;
                for (int i = byteIndex; i < 32; i++) word |= (uint)s[i] << ((i - byteIndex) * 8);
            }
            word >>= start & 7;
        }
        uint v = word & ((1u << (w + 1)) - 1);
        return (int)((v + 1) >> 1) - (int)((v >> w) << w);
    }
}
