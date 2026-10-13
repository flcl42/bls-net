using System.Buffers;
using System.Buffers.Binary;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

internal static class G2Msm
{
    internal static void Multiply(ReadOnlySpan<G2Affine> points, ReadOnlySpan<byte> scalars, out Jacobian<Fp2> result)
    {
        int expandedCount = checked(points.Length * 4);
        Affine<Fp2>[] rentedPoints = ArrayPool<Affine<Fp2>>.Shared.Rent(expandedCount);
        byte[] rentedScalars = ArrayPool<byte>.Shared.Rent(checked(expandedCount * 32));
        try
        {
            Span<Affine<Fp2>> expanded = rentedPoints.AsSpan(0, expandedCount);
            Span<byte> digits = rentedScalars.AsSpan(0, expandedCount * 32);
            digits.Clear();
            Span<ulong> split = stackalloc ulong[4];
            for (int i = 0; i < points.Length; i++)
            {
                ScalarMultiplication.SplitG2Scalar(scalars.Slice(i * 32, 32), split);
                Jacobian<Fp2>.FromAffine(out Jacobian<Fp2> p, points[i].X, points[i].Y);
                for (int j = 0; j < 4; j++)
                {
                    int index = i * 4 + j;
                    expanded[index] = new() { X = p.X, Y = p.Y };
                    BinaryPrimitives.WriteUInt64LittleEndian(digits.Slice(index * 32, 8), split[j]);
                    if (j != 3)
                    {
                        G2Projective.Psi(out p, p);
                        Jacobian<Fp2>.Negate(out p, p);
                    }
                }
            }
            // psi(P) = [z]P on G2. Base-|z| digits therefore turn the MSM
            // into four times as many points with just 64 scalar bits.
            Msm<Fp2>.Multiply(expanded, digits, out result, 64);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedScalars);
            ArrayPool<Affine<Fp2>>.Shared.Return(rentedPoints);
        }
    }
}
