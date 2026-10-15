using System.Numerics;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe class ConstantTimeTests
{
    private static readonly BigInteger P = BigInteger.Parse("0" + "1a0111ea397fe69a4b1ba7b6434bacd764774b84f38512bf6730d2a0f6b0f6241eabfffeb153ffffb9feffffffffaaab", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger R = BigInteger.Parse("0" + "73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001", System.Globalization.NumberStyles.HexNumber);
    private static readonly Random Random = new(0x4354);
    private static int checks;
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException("Constant-time: " + message); }
    private static byte[] Bytes(BigInteger n, int length)
    { byte[] b = new byte[length]; n.TryWriteBytes(b, out _, isUnsigned: true); return b; }
    private static Fp Field(BigInteger n)
    { Check(Fp.TryFromLittleEndian(Bytes(n, 48), out Fp f), "field parse"); return f; }
    private static Fp RandomFp()
    { byte[] b = new byte[48]; Random.NextBytes(b); return Field(new BigInteger(b, true) % P); }

    internal static int Run(bool native)
    {
        const int rounds = 2000;
        BigInteger[] edges = [0, 1, R - 1, R, R + 1, (BigInteger.One << 256) - 1];
        for (int i = 0; i < rounds + 262; i++)
        {
            byte[] scalar = new byte[32]; Random.NextBytes(scalar);
            if (i < edges.Length) scalar = Bytes(edges[i], 32);
            else if (i < 262) scalar = Bytes(BigInteger.One << (i - 6), 32);
            BigInteger n = new BigInteger(scalar, true) % R;
            Fr fr = Fr.FromLittleEndianConstantTime(scalar);
            byte[] encoded = new byte[32]; fr.ToLittleEndianConstantTime(encoded);
            Check(new BigInteger(encoded, true) == n, "scalar reduction");
            Fr.InverseConstantTime(out Fr fi, fr);
            fi.ToLittleEndianConstantTime(encoded);
            Check(new BigInteger(encoded, true) == BigInteger.ModPow(n, R - 2, R), "Fr inverse reference");
            Fr aliased = fr; Fr.InverseConstantTime(out aliased, aliased); Check(Fr.EqualMask(fi, aliased) == ulong.MaxValue, "Fr inverse alias");
            Fr.Multiply(out Fr identity, fi, fr); Check(Fr.EqualMask(identity, n.IsZero ? Fr.Zero : Fr.One) == ulong.MaxValue, "Fr inverse identity");
            Fr.ConditionalNegate(out Fr fn, fr, ulong.MaxValue); Fr.Negate(out Fr expectedFr, fr);
            Check(Fr.EqualMask(fn, expectedFr) == ulong.MaxValue, "Fr conditional negate");
            Fr.Select(out fn, fi, fr, 0); Check(Fr.EqualMask(fn, fr) == ulong.MaxValue, "Fr select");
            Fr.ConditionalSwap(ref fn, ref fi, ulong.MaxValue); Check(Fr.EqualMask(fi, fr) == ulong.MaxValue, "Fr swap");
            Check(Fr.IsZeroMask(fr) == (n.IsZero ? ulong.MaxValue : 0), "Fr zero mask");

            Fp a = i == 0 ? Fp.Zero : i == 1 ? Fp.One : i == 2 ? Field(P - 1) : RandomFp();
            Fp.InverseConstantTime(out Fp inverse, a); Fp.Inverse(out Fp expected, a);
            Check(Fp.EqualMask(inverse, expected) == ulong.MaxValue, "Fp inverse");
            Fp alias = a; Fp.InverseConstantTime(out alias, alias); Check(alias == inverse, "Fp inverse alias");
            Fp.ConditionalNegate(out Fp negative, a, ulong.MaxValue); Fp.Negate(out Fp neg, a); Check(negative == neg, "Fp conditional negate");
            Fp.Select(out alias, a, inverse, 0); Check(alias == inverse, "Fp select clear");
            Fp.ConditionalSwap(ref alias, ref inverse, ulong.MaxValue); Check(alias == inverse, "Fp swap equal");
            Check(Fp.IsZeroMask(a) == (a.IsZero ? ulong.MaxValue : 0), "Fp zero mask");
            Check(Fp.SignMask(a) == (Fp.Sgn0(a) ? ulong.MaxValue : 0), "Fp sign");
            ulong square = Fp.SqrtConstantTime(out Fp root, a); bool squareVt = Fp.Sqrt(out Fp rootVt, a);
            Check(square == (squareVt ? ulong.MaxValue : 0) && root == rootVt, "Fp square root");

            Fp2 a2 = i == 0 ? default : i == 1 ? Fp2.One : new(a, RandomFp());
            Fp2.InverseConstantTime(out Fp2 inv2, a2); Fp2.Inverse(out Fp2 expected2, a2); Check(inv2 == expected2, "Fp2 inverse");
            Fp2 alias2 = a2; Fp2.InverseConstantTime(out alias2, alias2); Check(alias2 == inv2, "Fp2 inverse alias");
            Fp2.ConditionalNegate(out Fp2 neg2, a2, ulong.MaxValue); Fp2.Negate(out Fp2 neg2Vt, a2); Check(neg2 == neg2Vt, "Fp2 conditional negate");
            Fp2.Select(out alias2, a2, inv2, ulong.MaxValue); Fp2.ConditionalSwap(ref alias2, ref inv2, ulong.MaxValue); Check(inv2 == a2, "Fp2 select/swap");
            Check(Fp2.IsZeroMask(a2) == (a2.IsZero ? ulong.MaxValue : 0), "Fp2 zero mask");
            Check(Fp2.SignMask(a2) == (Fp2.Sgn0(a2) ? ulong.MaxValue : 0), "Fp2 sign");
            square = Fp2.SqrtConstantTime(out Fp2 root2, a2); squareVt = FieldRoots.Sqrt(out Fp2 root2Vt, a2);
            Check(square == (squareVt ? ulong.MaxValue : 0) && root2 == root2Vt, "Fp2 square root");
            if (native)
            {
                KernelNative.blst_fp_inverse(out Fp ni, a); Check(ni == expected, "native Fp inverse");
                KernelNative.blst_fp2_inverse(out Fp2 ni2, a2); Check(ni2 == expected2, "native Fp2 inverse");
                KernelNative.blst_fr_inverse(out Fr nfr, fr); Fr.InverseConstantTime(out fi, fr); Check(Fr.EqualMask(nfr, fi) == ulong.MaxValue, "native Fr inverse");
            }

            MapToCurve.MapToG1ConstantTime(a, out G1Projective p); MapToCurve.MapToG1(a, out G1Projective pv);
            Check(G1Projective.EqualMask(p, pv) == ulong.MaxValue, "G1 map");
            MapToCurve.MapToG2ConstantTime(a2, out G2Projective q); MapToCurve.MapToG2(a2, out G2Projective qv);
            Check(G2Projective.EqualMask(q, qv) == ulong.MaxValue, "G2 map");
            if (native)
            {
                CurveNative.blst_map_to_g1(out G1Projective np, a, null); Check(G1Projective.EqualMask(np, p) == ulong.MaxValue, "native G1 map");
                CurveNative.blst_map_to_g2(out G2Projective nq, a2, null); Check(G2Projective.EqualMask(nq, q) == ulong.MaxValue, "native G2 map");
            }
            // Exercise secret infinity, including noncanonical projective infinity coordinates.
            if (i % 17 == 0) { p.Z = default; q.Z = default; }
            TestG1(p, scalar, native); TestG2(q, scalar, native);
        }
        TestExceptionalMaps(); TestArguments(); TestSmallAndDenseScalars(native); TestMasksAndAliases();
        Console.WriteLine($"Constant-time: {checks:N0} checks, seed 0x4354; native={native}.");
        return checks;
    }

    private static void TestG1(G1Projective p, byte[] scalar, bool native)
    {
        G1Affine affine = p.ToAffineConstantTime(); Check(G1Affine.EqualMask(affine, p.ToAffine()) == ulong.MaxValue, "G1 affine");
        G1Projective.MultiplyConstantTime(p, scalar, out G1Projective r);
        G1Projective.Multiply(affine, scalar, out G1Projective expected); Check(G1Projective.EqualMask(r, expected) == ulong.MaxValue, "G1 projective multiply");
        G1Projective alias = p; G1Projective.MultiplyConstantTime(alias, scalar, out alias); Check(G1Projective.EqualMask(r, alias) == ulong.MaxValue, "G1 multiply alias");
        G1Projective.MultiplyConstantTime(affine, scalar, out alias); Check(G1Projective.EqualMask(r, alias) == ulong.MaxValue, "G1 affine multiply");
        G1Projective.ConditionalNegate(out G1Projective neg, p, ulong.MaxValue); G1Projective.NegateConstantTime(p, out alias); Check(G1Projective.EqualMask(alias, neg) == ulong.MaxValue, "G1 negate");
        foreach (G1Projective other in new[] { p, neg, G1Projective.Generator, default })
        {
            G1Projective.AddConstantTime(p, other, out alias); G1Projective.Add(p, other, out expected); Check(G1Projective.EqualMask(alias, expected) == ulong.MaxValue, "G1 dadd");
            G1Affine oa = other.ToAffine(); G1Projective.AddAffineConstantTime(p, oa, out alias); G1Projective.AddAffine(p, oa, out expected); Check(G1Projective.EqualMask(alias, expected) == ulong.MaxValue, "G1 mixed dadd");
            Check(G1Projective.EqualMask(p, other) == (G1Projective.AreEqual(p, other) ? ulong.MaxValue : 0), "G1 equal mask");
        }
        Check(G1Projective.IsInfinityMask(p) == (p.IsInfinity ? ulong.MaxValue : 0), "G1 infinity mask");
        G1Projective.Select(out alias, r, p, ulong.MaxValue); G1Projective.ConditionalSwap(ref alias, ref neg, ulong.MaxValue); Check(G1Projective.EqualMask(neg, r) == ulong.MaxValue, "G1 select/swap");
        G1Affine an = affine; G1Affine.ConditionalNegate(out an, an, ulong.MaxValue); G1Affine.Select(out an, affine, an, 0); G1Affine.ConditionalSwap(ref an, ref affine, ulong.MaxValue);
        Check(G1Affine.IsInfinityMask(an) == G1Projective.IsInfinityMask(p), "G1 affine infinity");
        if (native)
        {
            fixed (byte* s = scalar) CurveNative.blst_p1_mult(out expected, p, s, 256);
            Check(G1Projective.EqualMask(r, expected) == ulong.MaxValue, "native G1 multiply");
            CurveNative.blst_p1_to_affine(out G1Affine na, p); Check(G1Affine.EqualMask(na, an) == ulong.MaxValue, "native G1 affine");
        }
    }

    private static void TestG2(G2Projective p, byte[] scalar, bool native)
    {
        G2Affine affine = p.ToAffineConstantTime(); Check(G2Affine.EqualMask(affine, p.ToAffine()) == ulong.MaxValue, "G2 affine");
        G2Projective.MultiplyConstantTime(p, scalar, out G2Projective r);
        G2Projective.Multiply(affine, scalar, out G2Projective expected); Check(G2Projective.EqualMask(r, expected) == ulong.MaxValue, "G2 projective multiply");
        G2Projective alias = p; G2Projective.MultiplyConstantTime(alias, scalar, out alias); Check(G2Projective.EqualMask(r, alias) == ulong.MaxValue, "G2 multiply alias");
        G2Projective.MultiplyConstantTime(affine, scalar, out alias); Check(G2Projective.EqualMask(r, alias) == ulong.MaxValue, "G2 affine multiply");
        G2Projective.ConditionalNegate(out G2Projective neg, p, ulong.MaxValue); G2Projective.NegateConstantTime(p, out alias); Check(G2Projective.EqualMask(alias, neg) == ulong.MaxValue, "G2 negate");
        foreach (G2Projective other in new[] { p, neg, G2Projective.Generator, default })
        {
            G2Projective.AddConstantTime(p, other, out alias); G2Projective.Add(p, other, out expected); Check(G2Projective.EqualMask(alias, expected) == ulong.MaxValue, "G2 dadd");
            G2Affine oa = other.ToAffine(); G2Projective.AddAffineConstantTime(p, oa, out alias); G2Projective.AddAffine(p, oa, out expected); Check(G2Projective.EqualMask(alias, expected) == ulong.MaxValue, "G2 mixed dadd");
            Check(G2Projective.EqualMask(p, other) == (G2Projective.AreEqual(p, other) ? ulong.MaxValue : 0), "G2 equal mask");
        }
        Check(G2Projective.IsInfinityMask(p) == (p.IsInfinity ? ulong.MaxValue : 0), "G2 infinity mask");
        G2Projective.Select(out alias, r, p, ulong.MaxValue); G2Projective.ConditionalSwap(ref alias, ref neg, ulong.MaxValue); Check(G2Projective.EqualMask(neg, r) == ulong.MaxValue, "G2 select/swap");
        G2Affine an = affine; G2Affine.ConditionalNegate(out an, an, ulong.MaxValue); G2Affine.Select(out an, affine, an, 0); G2Affine.ConditionalSwap(ref an, ref affine, ulong.MaxValue);
        Check(G2Affine.IsInfinityMask(an) == G2Projective.IsInfinityMask(p), "G2 affine infinity");
        if (native)
        {
            fixed (byte* s = scalar) CurveNative.blst_p2_mult(out expected, p, s, 256);
            Check(G2Projective.EqualMask(r, expected) == ulong.MaxValue, "native G2 multiply");
            CurveNative.blst_p2_to_affine(out G2Affine na, p); Check(G2Affine.EqualMask(na, an) == ulong.MaxValue, "native G2 affine");
        }
    }

    private static void TestExceptionalMaps()
    {
        // SSWU xd=0 when u=0 or Z*u^2=-1. Exercise every root when it exists.
        Fp.Inverse(out Fp invZ, MapConstants1.Z); Fp.Negate(out invZ, invZ);
        if (Fp.Sqrt(out Fp u, invZ))
        {
            for (int sign = 0; sign < 2; sign++)
            {
                MapToCurve.MapToG1ConstantTime(u, out G1Projective p); MapToCurve.MapToG1(u, out G1Projective expected);
                Check(G1Projective.EqualMask(p, expected) == ulong.MaxValue, "G1 exceptional SSWU"); Fp.Negate(out u, u);
            }
        }
        Fp2.Inverse(out Fp2 invZ2, new Fp2(Field(P - 2), Field(P - 1))); Fp2.Negate(out invZ2, invZ2);
        if (FieldRoots.Sqrt(out Fp2 u2, invZ2))
        {
            for (int sign = 0; sign < 2; sign++)
            {
                MapToCurve.MapToG2ConstantTime(u2, out G2Projective q); MapToCurve.MapToG2(u2, out G2Projective expected);
                Check(G2Projective.EqualMask(q, expected) == ulong.MaxValue, "G2 exceptional SSWU"); Fp2.Negate(out u2, u2);
            }
        }
    }

    private static void TestArguments()
    {
        foreach (int length in new[] { 0, 31, 33 })
        {
            try { Fr.FromLittleEndianConstantTime(new byte[length]); Check(false, "Fr length"); } catch (ArgumentException) { checks++; }
            try { G1Projective.MultiplyConstantTime(G1Affine.Generator, new byte[length], out _); Check(false, "G1 length"); } catch (ArgumentException) { checks++; }
            try { G2Projective.MultiplyConstantTime(G2Affine.Generator, new byte[length], out _); Check(false, "G2 length"); } catch (ArgumentException) { checks++; }
        }
    }

    private static void TestSmallAndDenseScalars(bool native)
    {
        for (int i = 0; i < 256; i++)
        {
            byte[] dense = Bytes(((BigInteger.One << 256) - 1) ^ (BigInteger.One << i), 32);
            TestG1(G1Projective.Generator, dense, native); TestG2(G2Projective.Generator, dense, native);
        }
        for (int i = 2; i <= 31; i++)
        { byte[] small = Bytes(i, 32); TestG1(G1Projective.Generator, small, native); TestG2(G2Projective.Generator, small, native); }
        Fp.Negate(out Fp minusOne, Fp.One);
        foreach (Fp2 input in new Fp2[] { new(Fp.Zero, Fp.One), new(Fp.Zero, minusOne), new(minusOne, Fp.Zero), new(Fp.One, Fp.One), new(Fp.One, minusOne), new(minusOne, minusOne) })
        {
            ulong mask = Fp2.SqrtConstantTime(out Fp2 root, input);
            bool square = FieldRoots.Sqrt(out Fp2 expectedRoot, input);
            Check(mask == (square ? ulong.MaxValue : 0) && root == expectedRoot, "Fp2 root exceptional coefficients");
            MapToCurve.MapToG2ConstantTime(input, out G2Projective map); MapToCurve.MapToG2(input, out G2Projective expected);
            Check(G2Projective.EqualMask(map, expected) == ulong.MaxValue, "Fp2 map exceptional coefficients");
            if (native) { CurveNative.blst_map_to_g2(out expected, input, null); Check(G2Projective.EqualMask(map, expected) == ulong.MaxValue, "native Fp2 exceptional coefficients"); }
        }
    }

    private static void TestMasksAndAliases()
    {
        foreach (ulong mask in new[] { 0UL, ulong.MaxValue })
        {
            Fp a = Fp.One, b = Fp.FromUInt64(2); Fp expected = mask == 0 ? a : b;
            Fp.ConditionalSwap(ref a, ref b, mask); Check(a == expected, "Fp distinct swap");
            Fp.ConditionalSwap(ref a, ref a, mask); Check(a == expected, "Fp identical swap");
            Fp.Select(out b, b, a, 0); Check(b == a, "Fp select alias");
            Fp2 a2 = Fp2.One, b2 = new(Fp.Zero, Fp.One), e2 = mask == 0 ? a2 : b2;
            Fp2.ConditionalSwap(ref a2, ref b2, mask); Check(a2 == e2, "Fp2 distinct swap");
            Fp2.ConditionalSwap(ref a2, ref a2, mask); Check(a2 == e2, "Fp2 identical swap");
            Fr ar = Fr.One, br = Fr.FromUInt64(2), er = mask == 0 ? ar : br;
            Fr.ConditionalSwap(ref ar, ref br, mask); Check(Fr.EqualMask(ar, er) == ulong.MaxValue, "Fr distinct swap");
            Fr.ConditionalSwap(ref ar, ref ar, mask); Check(Fr.EqualMask(ar, er) == ulong.MaxValue, "Fr identical swap");
            Check(Fp.EqualMask(Fp.Zero, Fp.One) == 0 && Fp2.EqualMask(Fp2.Zero, Fp2.One) == 0 && Fr.EqualMask(Fr.Zero, Fr.One) == 0, "unequal field masks");
            G1Projective p = G1Projective.Generator, zero = default, expected1 = mask == 0 ? p : zero;
            G1Projective.ConditionalSwap(ref p, ref zero, mask); Check(G1Projective.EqualMask(p, expected1) == ulong.MaxValue, "G1 distinct swap");
            G1Projective.ConditionalSwap(ref p, ref p, mask); Check(G1Projective.EqualMask(p, expected1) == ulong.MaxValue, "G1 identical swap");
            G2Projective q = G2Projective.Generator, zero2 = default, expected2 = mask == 0 ? q : zero2;
            G2Projective.ConditionalSwap(ref q, ref zero2, mask); Check(G2Projective.EqualMask(q, expected2) == ulong.MaxValue, "G2 distinct swap");
            G2Projective.ConditionalSwap(ref q, ref q, mask); Check(G2Projective.EqualMask(q, expected2) == ulong.MaxValue, "G2 identical swap");
        }
        G1Projective g1 = G1Projective.Generator; G1Projective.Double(g1, out G1Projective twice1);
        G1Projective.AddConstantTime(g1, g1, out g1); Check(G1Projective.EqualMask(g1, twice1) == ulong.MaxValue, "G1 dadd identical alias");
        G1Projective.AddAffineConstantTime(g1, G1Affine.Generator, out g1); G1Projective.Add(twice1, G1Projective.Generator, out G1Projective three1);
        Check(G1Projective.EqualMask(g1, three1) == ulong.MaxValue, "G1 mixed alias");
        G2Projective g2 = G2Projective.Generator; G2Projective.Double(g2, out G2Projective twice2);
        G2Projective.AddConstantTime(g2, g2, out g2); Check(G2Projective.EqualMask(g2, twice2) == ulong.MaxValue, "G2 dadd identical alias");
        G2Projective.AddAffineConstantTime(g2, G2Affine.Generator, out g2); G2Projective.Add(twice2, G2Projective.Generator, out G2Projective three2);
        Check(G2Projective.EqualMask(g2, three2) == ulong.MaxValue, "G2 mixed alias");
    }
}
