using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

internal static class KernelNative
{
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_inverse(out Fp2 r, in Fp2 a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fr_inverse(out Fr r, in Fr a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fr_mul(out Fr r, in Fr a, in Fr b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fr_add(out Fr r, in Fr a, in Fr b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_add(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_sub(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_mul_by_3(out Fp2 r, in Fp2 a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_mul_by_8(out Fp2 r, in Fp2 a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_lshift(out Fp2 r, in Fp2 a, nuint count);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_mul(out Fp r, in Fp a, in Fp b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_sqr(out Fp r, in Fp a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_mul(out Fp2 r, in Fp2 a, in Fp2 b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp2_sqr(out Fp2 r, in Fp2 a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_inverse(out Fp r, in Fp a);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_add(out Fp r, in Fp a, in Fp b);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_sub(out Fp r, in Fp a, in Fp b);
    // exports.c takes int here, rather than the public header's bool.
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl), SuppressGCTransition] internal static extern void blst_fp_cneg(out Fp r, in Fp a, int flag);
}
