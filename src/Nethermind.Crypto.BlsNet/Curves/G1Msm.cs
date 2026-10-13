using System.Buffers;
using System.Buffers.Binary;
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

internal static class G1Msm
{
    private static readonly Fp BetaSquared = GetBetaSquared();

    private static Fp GetBetaSquared()
    {
        Fp.MultiplyComposite(out Fp result, CurveConstants.Beta, CurveConstants.Beta);
        return result;
    }

    internal static void Endomorphism(out Jacobian<Fp> result, in Jacobian<Fp> p)
    {
        Fp.MultiplyComposite(out Fp x, p.X, BetaSquared);
        Fp.Negate(out Fp y, p.Y);
        result = new(x, y, p.Z);
    }

    internal static void Multiply(ReadOnlySpan<G1Affine> points, ReadOnlySpan<byte> scalars, out Jacobian<Fp> result)
    {
        int expandedCount = checked(points.Length * 2);
        Affine<Fp>[] rentedPoints = ArrayPool<Affine<Fp>>.Shared.Rent(expandedCount);
        byte[] rentedScalars = ArrayPool<byte>.Shared.Rent(checked(expandedCount * 32));
        try
        {
            Span<Affine<Fp>> expanded = rentedPoints.AsSpan(0, expandedCount);
            Span<byte> digits = rentedScalars.AsSpan(0, expandedCount * 32);
            digits.Clear();
            Span<UInt128> split = stackalloc UInt128[2];
            for (int i = 0; i < points.Length; i++)
            {
                ScalarMultiplication.SplitG1Scalar(scalars.Slice(i * 32, 32), split);
                expanded[i * 2] = new() { X = points[i].X, Y = points[i].Y };
                Fp.MultiplyComposite(out expanded[i * 2 + 1].X, points[i].X, BetaSquared);
                Fp.Negate(out expanded[i * 2 + 1].Y, points[i].Y);
                for (int j = 0; j < 2; j++)
                {
                    int offset = (i * 2 + j) * 32;
                    BinaryPrimitives.WriteUInt64LittleEndian(digits.Slice(offset, 8), (ulong)split[j]);
                    BinaryPrimitives.WriteUInt64LittleEndian(digits.Slice(offset + 8, 8), (ulong)(split[j] >> 64));
                }
            }
            Msm<Fp>.Multiply(expanded, digits, out result, 128);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedScalars);
            ArrayPool<Affine<Fp>>.Shared.Return(rentedPoints);
        }
    }
}
