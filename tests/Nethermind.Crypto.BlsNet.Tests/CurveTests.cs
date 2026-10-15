using System.Numerics;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class CurveTests
{
    private static readonly Random Rng = new(0x2537);
    private static int checks;
    private static bool NativeAvailable;
    private static readonly BigInteger Order = BigInteger.Parse("0" + "73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001", System.Globalization.NumberStyles.HexNumber);
    private static readonly byte[][] Edges = [Scalar(0), Scalar(1), Scalar(Order - 1), Scalar(Order), Scalar(Order + 1), Enumerable.Repeat((byte)255, 32).ToArray()];
    private static byte[] Scalar(BigInteger value)
    { byte[] bytes = new byte[32]; value.TryWriteBytes(bytes, out _, isUnsigned: true); return bytes; }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static Fp RandomFp()
    {
        Span<byte> bytes = stackalloc byte[48];
        do { Rng.NextBytes(bytes); bytes[0] &= 31; } while (!Fp.TryFromBigEndian(bytes, out _));
        Fp.TryFromBigEndian(bytes, out Fp r); return r;
    }
    private static Fp2 RandomFp2() => new(RandomFp(), RandomFp());
    internal static int Run(bool nativeAvailable)
    {
        NativeAvailable = nativeAvailable;
        if (NativeAvailable) { TestG1(); TestG2(); }
        TestArguments(); TestPropertiesAndAllocations();
        Console.WriteLine($"Curves/maps/MSM: {checks:N0} checks, deterministic seed 0x2537.");
        return checks;
    }
    private static void Equal(in G1Projective managed, in G1Projective native, string message)
    {
        G1Affine a = managed.ToAffine(); CurveNative.blst_p1_to_affine(out G1Affine b, native);
        ReadOnlySpan<G1Affine> aa = new(in a), bb = new(in b);
        Check(MemoryMarshal.AsBytes(aa).SequenceEqual(MemoryMarshal.AsBytes(bb)), message);
    }
    private static void TestG1()
    {
        G1Affine generator = G1Affine.Generator;
        Check(Fp.AreEqual(generator.X, CurveNative.blst_p1_affine_generator()->X) && Fp.AreEqual(generator.Y, CurveNative.blst_p1_affine_generator()->Y), "G1 generator");
        G1Projective g = G1Projective.Generator, infinity = default;
        foreach (byte[] scalar in Edges)
        {
            fixed (byte* sp = scalar)
            {
                G1Projective.Multiply(generator, scalar, out G1Projective actual); CurveNative.blst_p1_mult(out G1Projective expected, g, sp, 256);
                Equal(actual, expected, "G1 scalar edge");
            }
        }
        for (int i = 0; i < 2000; i++)
        {
            Fp u = i == 0 ? Fp.Zero : i == 1 ? Fp.One : RandomFp();
            MapToCurve.MapToG1(u, out G1Projective point); CurveNative.blst_map_to_g1(out G1Projective native, u, null);
            Equal(point, native, $"G1 map {i}");
            G1Affine affine = point.ToAffine();
            Check(affine.IsOnCurve() && CurveNative.blst_p1_affine_on_curve(affine) != 0, "G1 mapped on curve");
            Check(affine.IsInSubgroup() && CurveNative.blst_p1_affine_in_g1(affine) != 0, "G1 mapped subgroup");
            G1Affine bad = new(RandomFp(), RandomFp());
            Check(bad.IsOnCurve() == (CurveNative.blst_p1_affine_on_curve(bad) != 0), "G1 random on-curve check");
            G1Projective.Add(point, g, out G1Projective sum); CurveNative.blst_p1_add_or_double(out G1Projective nativeSum, point, g);
            Equal(sum, nativeSum, "G1 addition");
            G1Projective.AddAffine(point, generator, out sum); Equal(sum, nativeSum, "G1 mixed addition");
            G1Projective.Double(point, out sum); CurveNative.blst_p1_double(out nativeSum, point); Equal(sum, nativeSum, "G1 doubling");
            G1Projective.Add(point, point, out sum); Equal(sum, nativeSum, "G1 P+P");
            G1Projective.AddAffine(point, affine, out sum); Equal(sum, nativeSum, "G1 mixed P+P");
            G1Projective.Negate(point, out G1Projective neg); G1Projective.Add(point, neg, out sum); Check(sum.IsInfinity, "G1 P-P");
            G1Projective.AddAffine(neg, affine, out sum); Check(sum.IsInfinity, "G1 mixed P-P");
            G1Projective.Add(infinity, point, out sum); Check(G1Projective.AreEqual(sum, point), "G1 infinity+P");
            G1Projective.Add(point, infinity, out sum); Check(G1Projective.AreEqual(sum, point), "G1 P+infinity");
            G1Projective.AddAffine(point, default, out sum); Check(G1Projective.AreEqual(sum, point), "G1 P+affine infinity");
            G1Projective.AddAffine(infinity, affine, out sum); Check(G1Projective.AreEqual(sum, point), "G1 infinity+affine");
            G1Projective alias = point; G1Projective.Add(alias, g, out alias); CurveNative.blst_p1_add_or_double(out nativeSum, point, g); Equal(alias, nativeSum, "G1 aliased output");
            byte[] scalar = new byte[32]; Rng.NextBytes(scalar);
            fixed (byte* sp = scalar)
            {
                G1Projective.Multiply(affine, scalar, out sum); CurveNative.blst_p1_mult(out nativeSum, point, sp, 256);
                Equal(sum, nativeSum, "G1 multiplication");
            }
            Fp x = RandomFp(); Fp.Square(out Fp rhs, x); Fp.Multiply(out rhs, rhs, x);
            Fp.MulBy4(out Fp b, Fp.One); Fp.Add(out rhs, rhs, b);
            if (Fp.Sqrt(out Fp y, rhs))
            {
                G1Affine arbitrary = new(x, y); Check(arbitrary.IsOnCurve(), "G1 solved point on curve");
                Check(arbitrary.IsInSubgroup() == (CurveNative.blst_p1_affine_in_g1(arbitrary) != 0), "G1 arbitrary subgroup check");
            }
        }
        G1Affine zero = default; Check(zero.IsOnCurve() && zero.IsInSubgroup(), "G1 infinity predicates");
        G1Projective.Double(infinity, out G1Projective doubledInfinity); Check(doubledInfinity.IsInfinity, "G1 double infinity");
        G1Projective.MultiScalarMultiply(ReadOnlySpan<G1Affine>.Empty, [], out G1Projective empty); Check(empty.IsInfinity, "G1 empty MSM");
        foreach (int count in new[] { 1, 2, 3, 8, 31, 32, 33, 64, 128, 200, 256, 512, 1024 })
        {
            G1Affine[] points = new G1Affine[count]; byte[] scalars = new byte[count * 32];
            for (int i = 0; i < count; i++)
            {
                MapToCurve.MapToG1(RandomFp(), out G1Projective q); points[i] = q.ToAffine();
            }
            for (int round = 0; round < 12; round++)
            {
                Rng.NextBytes(scalars);
                if (round < 6) for (int j = 0; j < count; j++) Edges[(j + round) % 6].CopyTo(scalars, j * 32);
                if (round == 6) points[count / 2] = default;
                if (round == 10) Array.Clear(points);
                if (round == 11) Array.Clear(scalars);
                G1Projective.MultiScalarMultiply(points, scalars, out G1Projective actual); CurveNative.Msm(points, scalars, out G1Projective expected);
                Equal(actual, expected, $"G1 MSM n={count}, round={round}");
            }
        }
        TestG1BatchAffine(generator);
        Console.WriteLine("G1 differential tests passed.");
    }

    private static void TestG1BatchAffine(G1Affine generator)
    {
        const int count = 512;
        G1Affine[] points = Enumerable.Repeat(generator, count).ToArray();
        byte[] scalars = new byte[count * 32];
        for (int pattern = 0; pattern < 2; pattern++)
        {
            for (int i = 0; i < count; i++)
            {
                points[i] = pattern == 1 && i % 19 == 0 ? default : generator;
                scalars[i * 32] = pattern == 0 ? (byte)1 : i % 2 == 0 ? (byte)1 : (byte)15;
            }
            G1Projective.MultiScalarMultiply(points, scalars, out G1Projective actual);
            CurveNative.Msm(points, scalars, out G1Projective expected);
            Equal(actual, expected, pattern == 0 ? "G1 batch bucket doubling" : "G1 batch bucket inverse and infinity");
        }
    }
    private static void Equal(in G2Projective managed, in G2Projective native, string message)
    {
        G2Affine a = managed.ToAffine(); CurveNative.blst_p2_to_affine(out G2Affine b, native);
        ReadOnlySpan<G2Affine> aa = new(in a), bb = new(in b);
        Check(MemoryMarshal.AsBytes(aa).SequenceEqual(MemoryMarshal.AsBytes(bb)), message);
    }
    private static void TestG2()
    {
        G2Affine generator = G2Affine.Generator;
        Check(Fp2.AreEqual(generator.X, CurveNative.blst_p2_affine_generator()->X) && Fp2.AreEqual(generator.Y, CurveNative.blst_p2_affine_generator()->Y), "G2 generator");
        G2Projective g = G2Projective.Generator, infinity = default;
        foreach (byte[] scalar in Edges)
        {
            fixed (byte* sp = scalar)
            {
                G2Projective.Multiply(generator, scalar, out G2Projective actual); CurveNative.blst_p2_mult(out G2Projective expected, g, sp, 256);
                Equal(actual, expected, "G2 scalar edge");
            }
        }
        for (int i = 0; i < 2000; i++)
        {
            Fp2 u = i == 0 ? Fp2.Zero : i == 1 ? Fp2.One : RandomFp2();
            MapToCurve.MapToG2(u, out G2Projective point); CurveNative.blst_map_to_g2(out G2Projective native, u, null);
            Equal(point, native, $"G2 map {i}");
            G2Affine affine = point.ToAffine();
            Check(affine.IsOnCurve() && CurveNative.blst_p2_affine_on_curve(affine) != 0, "G2 mapped on curve");
            Check(affine.IsInSubgroup() && CurveNative.blst_p2_affine_in_g2(affine) != 0, "G2 mapped subgroup");
            G2Affine bad = new(RandomFp2(), RandomFp2());
            Check(bad.IsOnCurve() == (CurveNative.blst_p2_affine_on_curve(bad) != 0), "G2 random on-curve check");
            G2Projective.Add(point, g, out G2Projective sum); CurveNative.blst_p2_add_or_double(out G2Projective nativeSum, point, g);
            Equal(sum, nativeSum, "G2 addition");
            G2Projective.AddAffine(point, generator, out sum); Equal(sum, nativeSum, "G2 mixed addition");
            G2Projective.Double(point, out sum); CurveNative.blst_p2_double(out nativeSum, point); Equal(sum, nativeSum, "G2 doubling");
            G2Projective.Add(point, point, out sum); Equal(sum, nativeSum, "G2 P+P");
            G2Projective.AddAffine(point, affine, out sum); Equal(sum, nativeSum, "G2 mixed P+P");
            G2Projective.Negate(point, out G2Projective neg); G2Projective.Add(point, neg, out sum); Check(sum.IsInfinity, "G2 P-P");
            G2Projective.AddAffine(neg, affine, out sum); Check(sum.IsInfinity, "G2 mixed P-P");
            G2Projective.Add(infinity, point, out sum); Check(G2Projective.AreEqual(sum, point), "G2 infinity+P");
            G2Projective.Add(point, infinity, out sum); Check(G2Projective.AreEqual(sum, point), "G2 P+infinity");
            G2Projective.AddAffine(point, default, out sum); Check(G2Projective.AreEqual(sum, point), "G2 P+affine infinity");
            G2Projective.AddAffine(infinity, affine, out sum); Check(G2Projective.AreEqual(sum, point), "G2 infinity+affine");
            G2Projective alias = point; G2Projective.Add(alias, g, out alias); CurveNative.blst_p2_add_or_double(out nativeSum, point, g); Equal(alias, nativeSum, "G2 aliased output");
            byte[] scalar = new byte[32]; Rng.NextBytes(scalar);
            fixed (byte* sp = scalar)
            {
                G2Projective.Multiply(affine, scalar, out sum); CurveNative.blst_p2_mult(out nativeSum, point, sp, 256);
                Equal(sum, nativeSum, "G2 multiplication");
            }
            Fp2 x = RandomFp2(); Fp2.Square(out Fp2 rhs, x); Fp2.Multiply(out rhs, rhs, x);
            Fp2.MulBy4(out Fp2 b, new Fp2(Fp.One, Fp.One)); Fp2.Add(out rhs, rhs, b);
            if (FieldRoots.Sqrt(out Fp2 y, rhs))
            {
                G2Affine arbitrary = new(x, y); Check(arbitrary.IsOnCurve(), "G2 solved point on curve");
                Check(arbitrary.IsInSubgroup() == (CurveNative.blst_p2_affine_in_g2(arbitrary) != 0), "G2 arbitrary subgroup check");
            }
        }
        G2Affine zero = default; Check(zero.IsOnCurve() && zero.IsInSubgroup(), "G2 infinity predicates");
        G2Projective.Double(infinity, out G2Projective doubledInfinity); Check(doubledInfinity.IsInfinity, "G2 double infinity");
        G2Projective.MultiScalarMultiply(ReadOnlySpan<G2Affine>.Empty, [], out G2Projective empty); Check(empty.IsInfinity, "G2 empty MSM");
        foreach (int count in new[] { 1, 2, 3, 8, 31, 32, 33, 64, 128, 200, 256, 512, 1024 })
        {
            G2Affine[] points = new G2Affine[count]; byte[] scalars = new byte[count * 32];
            for (int i = 0; i < count; i++)
            {
                MapToCurve.MapToG2(RandomFp2(), out G2Projective q); points[i] = q.ToAffine();
            }
            for (int round = 0; round < 12; round++)
            {
                Rng.NextBytes(scalars);
                if (round < 6) for (int j = 0; j < count; j++) Edges[(j + round) % 6].CopyTo(scalars, j * 32);
                if (round == 6) points[count / 2] = default;
                if (round == 10) Array.Clear(points);
                if (round == 11) Array.Clear(scalars);
                G2Projective.MultiScalarMultiply(points, scalars, out G2Projective actual); CurveNative.Msm(points, scalars, out G2Projective expected);
                Equal(actual, expected, $"G2 MSM n={count}, round={round}");
            }
        }
        TestG2BatchAffine(generator);
        Console.WriteLine("G2 differential tests passed.");
    }

    private static void TestG2BatchAffine(G2Affine generator)
    {
        const int count = 128;
        G2Affine[] points = Enumerable.Repeat(generator, count).ToArray();
        byte[] scalars = new byte[count * 32];
        for (int pattern = 0; pattern < 2; pattern++)
        {
            for (int i = 0; i < count; i++)
            {
                points[i] = pattern == 1 && i % 17 == 0 ? default : generator;
                scalars[i * 32] = pattern == 0 ? (byte)1 : i % 2 == 0 ? (byte)1 : (byte)15;
            }
            G2Projective.MultiScalarMultiply(points, scalars, out G2Projective actual);
            CurveNative.Msm(points, scalars, out G2Projective expected);
            Equal(actual, expected, pattern == 0 ? "G2 batch bucket doubling" : "G2 batch bucket inverse and infinity");
        }
    }
    private static void TestArguments()
    {
        try { G1Projective.Multiply(default, new byte[31], out _); Check(false, "scalar length"); } catch (ArgumentException) { checks++; }
        try { G2Projective.MultiScalarMultiply(new G2Affine[2], new byte[32], out _); Check(false, "MSM length"); } catch (ArgumentException) { checks++; }
    }
}
