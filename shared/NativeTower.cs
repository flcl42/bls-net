using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

internal static unsafe class TowerInterop
{
    [StructLayout(LayoutKind.Sequential)] internal struct P1 { public Fp X, Y, Z; }
    [StructLayout(LayoutKind.Sequential)] internal struct P2 { public Fp2 X, Y, Z; }
    internal static class Native
    {
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_mul(out Fp r, in Fp a, in Fp b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_sqr(out Fp r, in Fp a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_mul(out Fp2 r, in Fp2 a, in Fp2 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_sub(out Fp2 r, in Fp2 a, in Fp2 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp2_sqr(out Fp2 r, in Fp2 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp_inverse(out Fp r, in Fp a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_mul(out Fp12 r, in Fp12 a, in Fp12 b);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_sqr(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_cyclotomic_sqr(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_inverse(out Fp12 r, in Fp12 a);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_frobenius_map(out Fp12 r, in Fp12 a, nuint n);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_fp12_mul_by_xy00z0(out Fp12 r, in Fp12 a, in Fp6 packed);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_miller_loop(out Fp12 r, in G2Affine q, in G1Affine p);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_miller_loop_n(out Fp12 r, G2Affine** q, G1Affine** p, nuint n);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_final_exp(out Fp12 r, in Fp12 f);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern P1* blst_p1_generator();
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern P2* blst_p2_generator();
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p1_mult(out P1 r, P1* p, byte* scalar, nuint bits);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p2_mult(out P2 r, P2* p, byte* scalar, nuint bits);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p1_to_affine(out G1Affine r, in P1 p);
        [DllImport("blst", CallingConvention = CallingConvention.Cdecl)] internal static extern void blst_p2_to_affine(out G2Affine r, in P2 p);
    }
}
