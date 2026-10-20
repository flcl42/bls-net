using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

// Optional test/benchmark exports. The production library never loads native code.
internal static class NativeKernelReference
{
    internal static bool Available { get; set; }

    [DllImport("blst-kernels", EntryPoint = "bls_net_mul_384", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Multiply(out FpWide result, in Fp a, in Fp b);

    [DllImport("blst-kernels", EntryPoint = "bls_net_redc_384", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Reduce(out Fp result, in FpWide value);

    [DllImport("blst-kernels", EntryPoint = "bls_net_sub_redc_384", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SubtractReduce(out Fp result, in FpWide a, in FpWide b);
}
