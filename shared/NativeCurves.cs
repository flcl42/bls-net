using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

internal static unsafe class CurveNative
{
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern G1Affine* blst_p1_affine_generator();
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int blst_p1_affine_on_curve(in G1Affine p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int blst_p1_affine_in_g1(in G1Affine p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1_to_affine(out G1Affine r, in G1Projective p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1_add_or_double(out G1Projective r, in G1Projective p, in G1Projective q);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1_add_or_double_affine(out G1Projective r, in G1Projective p, in G1Affine q);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1_double(out G1Projective r, in G1Projective p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1_mult(out G1Projective r, in G1Projective p, byte* scalar, nuint bits);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_map_to_g1(out G1Projective r, in Fp u, void* v);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint blst_p1s_mult_pippenger_scratch_sizeof(nuint n);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p1s_mult_pippenger(out G1Projective r, G1Affine** points, nuint n, byte** scalars, nuint bits, ulong* scratch);
    internal static void Msm(ReadOnlySpan<G1Affine> points, ReadOnlySpan<byte> scalars, out G1Projective r)
    {
        G1Affine** ps = stackalloc G1Affine*[points.Length]; byte** ss = stackalloc byte*[points.Length];
        nuint size = blst_p1s_mult_pippenger_scratch_sizeof((nuint)points.Length);
        ulong* scratch = (ulong*)NativeMemory.Alloc(size);
        try
        {
            fixed (G1Affine* pp = points) fixed (byte* sp = scalars)
            {
                for (int i = 0; i < points.Length; i++) { ps[i] = pp + i; ss[i] = sp + i * 32; }
                blst_p1s_mult_pippenger(out r, ps, (nuint)points.Length, ss, 256, scratch);
            }
        }
        finally { NativeMemory.Free(scratch); }
    }
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern G2Affine* blst_p2_affine_generator();
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int blst_p2_affine_on_curve(in G2Affine p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int blst_p2_affine_in_g2(in G2Affine p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2_to_affine(out G2Affine r, in G2Projective p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2_add_or_double(out G2Projective r, in G2Projective p, in G2Projective q);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2_add_or_double_affine(out G2Projective r, in G2Projective p, in G2Affine q);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2_double(out G2Projective r, in G2Projective p);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2_mult(out G2Projective r, in G2Projective p, byte* scalar, nuint bits);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_map_to_g2(out G2Projective r, in Fp2 u, void* v);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint blst_p2s_mult_pippenger_scratch_sizeof(nuint n);
    [DllImport("blst", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void blst_p2s_mult_pippenger(out G2Projective r, G2Affine** points, nuint n, byte** scalars, nuint bits, ulong* scratch);
    internal static void Msm(ReadOnlySpan<G2Affine> points, ReadOnlySpan<byte> scalars, out G2Projective r)
    {
        G2Affine** ps = stackalloc G2Affine*[points.Length]; byte** ss = stackalloc byte*[points.Length];
        nuint size = blst_p2s_mult_pippenger_scratch_sizeof((nuint)points.Length);
        ulong* scratch = (ulong*)NativeMemory.Alloc(size);
        try
        {
            fixed (G2Affine* pp = points) fixed (byte* sp = scalars)
            {
                for (int i = 0; i < points.Length; i++) { ps[i] = pp + i; ss[i] = sp + i * 32; }
                blst_p2s_mult_pippenger(out r, ps, (nuint)points.Length, ss, 256, scratch);
            }
        }
        finally { NativeMemory.Free(scratch); }
    }
}
