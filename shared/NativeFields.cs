using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static class KernelNative
{
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_inverse(out Fp2 r, in Fp2 a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fr_inverse(out Fr r, in Fr a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fr_mul(out Fr r, in Fr a, in Fr b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fr_add(out Fr r, in Fr a, in Fr b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_add(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_sub(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_mul_by_3(out Fp2 r, in Fp2 a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_mul_by_8(out Fp2 r, in Fp2 a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_lshift(out Fp2 r, in Fp2 a, nuint count);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_mul(out Fp r, in Fp a, in Fp b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_sqr(out Fp r, in Fp a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_mul(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp2_sqr(out Fp2 r, in Fp2 a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_inverse(out Fp r, in Fp a);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_add(out Fp r, in Fp a, in Fp b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_sub(out Fp r, in Fp a, in Fp b);
    [DllImport("blst"), SuppressGCTransition] internal static extern void blst_fp_cneg(out Fp r, in Fp a, byte flag);
}
