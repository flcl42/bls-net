using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class Program
{
    private static void TestPropertiesAndAllocations()
    {
        TestScalarSplits();
        TestIsogenyPolynomials();
        G1Affine normalized1 = G1Projective.FromAffine(G1Affine.Generator).ToAffine();
        G2Affine normalized2 = G2Projective.FromAffine(G2Affine.Generator).ToAffine();
        Check(Fp.AreEqual(normalized1.X, G1Affine.Generator.X) && Fp.AreEqual(normalized1.Y, G1Affine.Generator.Y), "normalized G1 affine conversion");
        Check(Fp2.AreEqual(normalized2.X, G2Affine.Generator.X) && Fp2.AreEqual(normalized2.Y, G2Affine.Generator.Y), "normalized G2 affine conversion");
        Fp.Negate(out Fp minusOne, Fp.One);
        foreach (Fp u in new[] { Fp.Zero, Fp.One, minusOne })
        {
            MapToCurve.MapToG1(u, out G1Projective p);
            Check(p.ToAffine().IsOnCurve() && p.ToAffine().IsInSubgroup(), "G1 map field edge");
            if (NativeAvailable) { Native.blst_map_to_g1(out G1Projective q, u, null); Equal(p, q, "G1 map field edge native"); }
        }
        foreach (Fp2 u in new[] { Fp2.Zero, Fp2.One, new Fp2(minusOne, Fp.Zero), new Fp2(Fp.Zero, Fp.One), new Fp2(Fp.Zero, minusOne), new Fp2(Fp.One, Fp.One), new Fp2(Fp.One, minusOne) })
        {
            MapToCurve.MapToG2(u, out G2Projective p);
            Check(p.ToAffine().IsOnCurve() && p.ToAffine().IsInSubgroup(), "G2 map field edge");
            if (NativeAvailable) { Native.blst_map_to_g2(out G2Projective q, u, null); Equal(p, q, "G2 map field edge native"); }
            Fp2.Square(out Fp2 square, u); Check(FieldRoots.Sqrt(out Fp2 root, square), "Fp2 square root edge");
            Fp2.Square(out Fp2 recovered, root); Check(Fp2.AreEqual(square, recovered), "Fp2 square root equation");
        }
        {
            G1Affine infinity = default;
            foreach (byte[] edge in Edges)
            {
                G1Projective.Multiply(infinity, edge, out G1Projective zero); Check(zero.IsInfinity, "G1 infinity multiply");
            }
            G1Affine generator = G1Affine.Generator; G1Projective g = G1Projective.Generator;
            for (int bit = 0; bit < 256; bit++)
            {
                byte[] scalar = Scalar(BigIntegerOne << bit);
                G1Projective.Multiply(generator, scalar, out G1Projective actual);
                if (NativeAvailable) fixed (byte* sp = scalar) { Native.blst_p1_mult(out G1Projective native, g, sp, 256); Equal(actual, native, "G1 scalar bit"); }
            }
            for (int i = 0; i < 128; i++)
            {
                MapToCurve.MapToG1(RandomFp(), out G1Projective p);
                MapToCurve.MapToG1(RandomFp(), out G1Projective q);
                MapToCurve.MapToG1(RandomFp(), out G1Projective t);
                G1Projective.Add(p, q, out G1Projective pq); G1Projective.Add(pq, t, out G1Projective lhs);
                G1Projective.Add(q, t, out G1Projective qt); G1Projective.Add(p, qt, out G1Projective rhs);
                Check(G1Projective.AreEqual(lhs, rhs), "G1 associativity");
                G1Projective.Add(q, p, out rhs); Check(G1Projective.AreEqual(pq, rhs), "G1 commutativity");
                Fp scale;
                do { scale = RandomFp(); } while (Fp.EqualsZero(scale));
                Fp.Square(out Fp s2, scale); Fp.Multiply(out Fp s3, s2, scale);
                Fp.Multiply(out Fp x, p.X, s2); Fp.Multiply(out Fp y, p.Y, s3); Fp.Multiply(out Fp z, p.Z, scale);
                G1Projective scaled = new(x, y, z); Check(G1Projective.AreEqual(p, scaled), "G1 projective equality");
                G1Projective.Add(scaled, q, out rhs); Check(G1Projective.AreEqual(pq, rhs), "G1 rescaled addition");
                Fp.Square(out Fp zz, p.Z); Fp.Multiply(out Fp zzz, zz, p.Z);
                Xyzz<Fp> bucket = new() { X = p.X, Y = p.Y, ZZ = zz, ZZZ = zzz };
                G1Affine qa = q.ToAffine();
                Xyzz<Fp>.AddAffine(out Xyzz<Fp> result, bucket, qa.X, qa.Y);
                result.ToJacobian(out Jacobian<Fp> jac);
                Check(Jacobian<Fp>.AreEqual(jac, new Jacobian<Fp>(pq.X, pq.Y, pq.Z)), "G1 XYZZ add");
                Xyzz<Fp>.AddAffine(out result, result, qa.X, qa.Y, true); result.ToJacobian(out jac);
                Check(Jacobian<Fp>.AreEqual(jac, new Jacobian<Fp>(p.X, p.Y, p.Z)), "G1 XYZZ subtract");
                Xyzz<Fp>.Add(out result, bucket, bucket); result.ToJacobian(out jac);
                G1Projective.Double(p, out rhs); Check(Jacobian<Fp>.AreEqual(jac, new Jacobian<Fp>(rhs.X, rhs.Y, rhs.Z)), "G1 XYZZ double");
                G1Projective.Double(pq, out lhs); G1Projective.Double(p, out G1Projective dp); G1Projective.Double(q, out G1Projective dq); G1Projective.Add(dp, dq, out rhs);
                Check(G1Projective.AreEqual(lhs, rhs), "G1 doubling distributes");
                G1Projective alias = p; G1Projective.Double(alias, out alias); Check(G1Projective.AreEqual(alias, dp), "G1 double output alias");
                alias = p; G1Projective.Add(alias, q, out alias); Check(G1Projective.AreEqual(alias, pq), "G1 add left output alias");
                alias = q; G1Projective.Add(p, alias, out alias); Check(G1Projective.AreEqual(alias, pq), "G1 add right output alias");
            }
            byte[] bytes = new byte[32]; Rng.NextBytes(bytes);
            foreach (int count in new[] { 1, 2, 8, 31, 32, 64, 128, 512 })
            {
                G1Affine[] points = Enumerable.Repeat(generator, count).ToArray();
                byte[] scalars = new byte[count * 32]; Rng.NextBytes(scalars);
                for (int i = 0; i < 30; i++) { G1Projective.Multiply(generator, bytes, out _); G1Projective.MultiScalarMultiply(points, scalars, out _); }
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20; i++) { G1Projective.Multiply(generator, bytes, out _); G1Projective.MultiScalarMultiply(points, scalars, out _); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                Check(allocated == 0, $"G1 allocations n={count}: {allocated}");
            }
        }
        {
            G2Affine infinity = default;
            foreach (byte[] edge in Edges)
            {
                G2Projective.Multiply(infinity, edge, out G2Projective zero); Check(zero.IsInfinity, "G2 infinity multiply");
            }
            G2Affine generator = G2Affine.Generator; G2Projective g = G2Projective.Generator;
            for (int bit = 0; bit < 256; bit++)
            {
                byte[] scalar = Scalar(BigIntegerOne << bit);
                G2Projective.Multiply(generator, scalar, out G2Projective actual);
                if (NativeAvailable) fixed (byte* sp = scalar) { Native.blst_p2_mult(out G2Projective native, g, sp, 256); Equal(actual, native, "G2 scalar bit"); }
            }
            for (int i = 0; i < 128; i++)
            {
                MapToCurve.MapToG2(RandomFp2(), out G2Projective p);
                MapToCurve.MapToG2(RandomFp2(), out G2Projective q);
                MapToCurve.MapToG2(RandomFp2(), out G2Projective t);
                G2Projective.Add(p, q, out G2Projective pq); G2Projective.Add(pq, t, out G2Projective lhs);
                G2Projective.Add(q, t, out G2Projective qt); G2Projective.Add(p, qt, out G2Projective rhs);
                Check(G2Projective.AreEqual(lhs, rhs), "G2 associativity");
                G2Projective.Add(q, p, out rhs); Check(G2Projective.AreEqual(pq, rhs), "G2 commutativity");
                Fp2 scale;
                do { scale = RandomFp2(); } while (Fp2.EqualsZero(scale));
                Fp2.Square(out Fp2 s2, scale); Fp2.Multiply(out Fp2 s3, s2, scale);
                Fp2.Multiply(out Fp2 x, p.X, s2); Fp2.Multiply(out Fp2 y, p.Y, s3); Fp2.Multiply(out Fp2 z, p.Z, scale);
                G2Projective scaled = new(x, y, z); Check(G2Projective.AreEqual(p, scaled), "G2 projective equality");
                G2Projective.Add(scaled, q, out rhs); Check(G2Projective.AreEqual(pq, rhs), "G2 rescaled addition");
                Fp2.Square(out Fp2 zz, p.Z); Fp2.Multiply(out Fp2 zzz, zz, p.Z);
                Xyzz<Fp2> bucket = new() { X = p.X, Y = p.Y, ZZ = zz, ZZZ = zzz };
                G2Affine qa = q.ToAffine();
                Xyzz<Fp2>.AddAffine(out Xyzz<Fp2> result, bucket, qa.X, qa.Y);
                result.ToJacobian(out Jacobian<Fp2> jac);
                Check(Jacobian<Fp2>.AreEqual(jac, new Jacobian<Fp2>(pq.X, pq.Y, pq.Z)), "G2 XYZZ add");
                Xyzz<Fp2>.AddAffine(out result, result, qa.X, qa.Y, true); result.ToJacobian(out jac);
                Check(Jacobian<Fp2>.AreEqual(jac, new Jacobian<Fp2>(p.X, p.Y, p.Z)), "G2 XYZZ subtract");
                Xyzz<Fp2>.Add(out result, bucket, bucket); result.ToJacobian(out jac);
                G2Projective.Double(p, out rhs); Check(Jacobian<Fp2>.AreEqual(jac, new Jacobian<Fp2>(rhs.X, rhs.Y, rhs.Z)), "G2 XYZZ double");
                G2Projective.Double(pq, out lhs); G2Projective.Double(p, out G2Projective dp); G2Projective.Double(q, out G2Projective dq); G2Projective.Add(dp, dq, out rhs);
                Check(G2Projective.AreEqual(lhs, rhs), "G2 doubling distributes");
                G2Projective alias = p; G2Projective.Double(alias, out alias); Check(G2Projective.AreEqual(alias, dp), "G2 double output alias");
                alias = p; G2Projective.Add(alias, q, out alias); Check(G2Projective.AreEqual(alias, pq), "G2 add left output alias");
                alias = q; G2Projective.Add(p, alias, out alias); Check(G2Projective.AreEqual(alias, pq), "G2 add right output alias");
            }
            byte[] bytes = new byte[32]; Rng.NextBytes(bytes);
            foreach (int count in new[] { 1, 2, 8, 31, 32, 64, 128, 512 })
            {
                G2Affine[] points = Enumerable.Repeat(generator, count).ToArray();
                byte[] scalars = new byte[count * 32]; Rng.NextBytes(scalars);
                for (int i = 0; i < 30; i++) { G2Projective.Multiply(generator, bytes, out _); G2Projective.MultiScalarMultiply(points, scalars, out _); }
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20; i++) { G2Projective.Multiply(generator, bytes, out _); G2Projective.MultiScalarMultiply(points, scalars, out _); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                Check(allocated == 0, $"G2 allocations n={count}: {allocated}");
            }
        }
    }

    private static void TestScalarSplits()
    {
        Span<UInt128> glv = stackalloc UInt128[2];
        Span<ulong> gls = stackalloc ulong[4];
        System.Numerics.BigInteger absZ = 0xd201000000010000UL;
        for (int i = 0; i < 768 + Edges.Length; i++)
        {
            byte[] scalar = i < Edges.Length ? Edges[i] : i < 256 + Edges.Length
                ? Scalar(BigIntegerOne << (i - Edges.Length)) : new byte[32];
            if (i >= 256 + Edges.Length) Rng.NextBytes(scalar);
            System.Numerics.BigInteger reduced = new System.Numerics.BigInteger(scalar, isUnsigned: true) % Order;
            ScalarMultiplication.SplitG1Scalar(scalar, glv);
            ScalarMultiplication.SplitG2Scalar(scalar, gls);
            Check((System.Numerics.BigInteger)glv[0] + (System.Numerics.BigInteger)glv[1] * absZ * absZ == reduced, "GLV scalar reconstruction");
            Check((System.Numerics.BigInteger)glv[0] < absZ * absZ, "GLV remainder bound");
            System.Numerics.BigInteger reconstructed = 0;
            for (int j = 3; j >= 0; j--)
            {
                Check(gls[j] < absZ, "GLS digit bound");
                reconstructed = reconstructed * absZ + gls[j];
            }
            Check(reconstructed == reduced, "GLS scalar reconstruction");
        }
    }
    private static System.Numerics.BigInteger BigIntegerOne => System.Numerics.BigInteger.One;
}
