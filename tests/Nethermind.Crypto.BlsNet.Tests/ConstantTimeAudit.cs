using System.Runtime.CompilerServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

// Dedicated audit call graph: setup is outside Exercise, all its field/point operands are secret.
internal static class ConstantTimeAudit
{
    private static ulong witness;
    internal static void Run(bool warm)
    {
        byte[] scalar = new byte[32]; new Random(0x4354).NextBytes(scalar);
        Fp a = G1Affine.Generator.X; Fp2 b = G2Affine.Generator.X;
        G1Projective p = G1Projective.Generator; G2Projective q = G2Projective.Generator;
        Fr fr = Fr.FromLittleEndianConstantTime(scalar);
        for (int i = 0; i < (warm ? 1024 : 4); i++) Exercise(a, b, fr, scalar, p, q);
        Console.WriteLine($"Constant-time audit witness: {witness:x16}");
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Exercise(Fp a, Fp2 b, Fr fr, byte[] scalar, G1Projective p, G2Projective q)
    {
        ulong mask = Fp.IsZeroMask(a);
        Fr secret = Fr.FromLittleEndianConstantTime(scalar); secret.ToLittleEndianConstantTime(scalar);
        Fp.Add(out Fp ar, a, a); Fp.Subtract(out ar, ar, a);
        Fp.Negate(out ar, ar); Fp.Multiply(out ar, ar, a); Fp.Square(out ar, ar);
        Fp.InverseConstantTime(out ar, ar);
        Fp.Select(out ar, a, ar, mask); Fp.ConditionalNegate(out ar, ar, mask);
        Fp.ConditionalSwap(ref a, ref ar, mask); mask ^= Fp.EqualMask(a, ar) ^ Fp.IsZeroMask(a);
        mask ^= Fp.SqrtConstantTime(out ar, a); mask ^= Fp.SignMask(a);
        Fp2.Add(out Fp2 br, b, b); Fp2.Subtract(out br, br, b);
        Fp2.Negate(out br, br); Fp2.Multiply(out br, br, b); Fp2.Square(out br, br);
        Fp2.InverseConstantTime(out br, br);
        Fp2.Select(out br, b, br, mask); Fp2.ConditionalNegate(out br, br, mask);
        Fp2.ConditionalSwap(ref b, ref br, mask); mask ^= Fp2.EqualMask(b, br) ^ Fp2.IsZeroMask(b);
        mask ^= Fp2.SqrtConstantTime(out br, b); mask ^= Fp2.SignMask(b);
        Fr.Add(out Fr frr, fr, fr); Fr.Subtract(out frr, frr, fr);
        Fr.Negate(out frr, frr); Fr.Multiply(out frr, frr, fr); Fr.Square(out frr, frr);
        Fr.InverseConstantTime(out frr, frr);
        Fr.Select(out frr, fr, frr, mask); Fr.ConditionalNegate(out frr, frr, mask);
        Fr.ConditionalSwap(ref fr, ref frr, mask); mask ^= Fr.EqualMask(fr, frr) ^ Fr.IsZeroMask(fr);
        MapToCurve.MapToG1ConstantTime(a, out G1Projective pr);
        G1Projective.MultiplyConstantTime(p, scalar, out pr);
        G1Affine pa = pr.ToAffineConstantTime();
        G1Projective.MultiplyConstantTime(pa, scalar, out pr);
        G1Projective.AddConstantTime(p, pr, out pr); G1Projective.AddAffineConstantTime(pr, pa, out pr);
        G1Projective.NegateConstantTime(pr, out pr);
        G1Projective.Select(out pr, pr, p, mask); G1Projective.ConditionalNegate(out pr, pr, mask);
        G1Projective.ConditionalSwap(ref p, ref pr, mask);
        mask ^= G1Projective.EqualMask(p, pr) ^ G1Projective.IsInfinityMask(pr);
        G1Affine.Select(out pa, pa, default, mask); G1Affine.ConditionalNegate(out pa, pa, mask);
        G1Affine pa2 = pa; G1Affine.ConditionalSwap(ref pa, ref pa2, mask);
        mask ^= G1Affine.EqualMask(pa, pa2) ^ G1Affine.IsInfinityMask(pa);
        MapToCurve.MapToG2ConstantTime(b, out G2Projective qr);
        G2Projective.MultiplyConstantTime(q, scalar, out qr);
        G2Affine qa = qr.ToAffineConstantTime();
        G2Projective.MultiplyConstantTime(qa, scalar, out qr);
        G2Projective.AddConstantTime(q, qr, out qr); G2Projective.AddAffineConstantTime(qr, qa, out qr);
        G2Projective.NegateConstantTime(qr, out qr);
        G2Projective.Select(out qr, qr, q, mask); G2Projective.ConditionalNegate(out qr, qr, mask);
        G2Projective.ConditionalSwap(ref q, ref qr, mask);
        mask ^= G2Projective.EqualMask(q, qr) ^ G2Projective.IsInfinityMask(qr);
        G2Affine.Select(out qa, qa, default, mask); G2Affine.ConditionalNegate(out qa, qa, mask);
        G2Affine qa2 = qa; G2Affine.ConditionalSwap(ref qa, ref qa2, mask);
        mask ^= G2Affine.EqualMask(qa, qa2) ^ G2Affine.IsInfinityMask(qa);
        witness = mask;
    }
}
