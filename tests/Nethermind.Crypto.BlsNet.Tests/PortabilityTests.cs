using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

internal static class PortabilityTests
{
    internal static int Run(bool native)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"Portability: {name}");
            checks++;
        }
        Check(BitConverter.IsLittleEndian, "limb byte order");
        Check(Unsafe.SizeOf<Fp>() == 48 && Unsafe.SizeOf<Fr>() == 32, "base field layout");
        Check(Unsafe.SizeOf<Fp2>() == 96 && Unsafe.SizeOf<Fp6>() == 288 && Unsafe.SizeOf<Fp12>() == 576, "tower layout");
        Check(Unsafe.SizeOf<G1Affine>() == 96 && Unsafe.SizeOf<G1Projective>() == 144, "G1 layout");
        Check(Unsafe.SizeOf<G2Affine>() == 192 && Unsafe.SizeOf<G2Projective>() == 288, "G2 layout");
        G1Projective layout1 = default;
        G2Projective layout2 = default;
        Check(Unsafe.ByteOffset(ref layout1.Value.X, ref layout1.Value.Y) == 48 && Unsafe.ByteOffset(ref layout1.Value.X, ref layout1.Value.Z) == 96, "G1 coordinate offsets");
        Check(Unsafe.ByteOffset(ref layout2.Value.X, ref layout2.Value.Y) == 96 && Unsafe.ByteOffset(ref layout2.Value.X, ref layout2.Value.Z) == 192, "G2 coordinate offsets");
        if (native)
        {
            G1Affine good1 = G1Affine.Generator, bad1 = new(Fp.One, Fp.One);
            G2Affine good2 = G2Affine.Generator, bad2 = new(Fp2.One, Fp2.One);
            Check(CurveNative.blst_p1_affine_on_curve(good1) == 1 && CurveNative.blst_p1_affine_on_curve(bad1) == 0, "native G1 int predicate");
            Check(CurveNative.blst_p2_affine_on_curve(good2) == 1 && CurveNative.blst_p2_affine_on_curve(bad2) == 0, "native G2 int predicate");
            Check(CurveNative.blst_p1_affine_in_g1(good1) == 1 && CurveNative.blst_p2_affine_in_g2(good2) == 1, "native subgroup int predicates");
            KernelNative.blst_fp_cneg(out Fp unchanged, Fp.One, 0);
            KernelNative.blst_fp_cneg(out Fp negated, Fp.One, 1);
            Fp.Negate(out Fp expected, Fp.One);
            Check(Fp.AreEqual(unchanged, Fp.One) && Fp.AreEqual(negated, expected), "native int cneg flag 0/1");
            KernelNative.blst_fp_cneg(out Fp wideFlag, Fp.One, 256);
            KernelNative.blst_fp_cneg(out Fp negativeFlag, Fp.One, -1);
            Check(Fp.AreEqual(wideFlag, expected) && Fp.AreEqual(negativeFlag, expected), "native int cneg flag width/sign");
        }
        Console.WriteLine($"Portability: {checks} checks.");
        return checks;
    }
}
