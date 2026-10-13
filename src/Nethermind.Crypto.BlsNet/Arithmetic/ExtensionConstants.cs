using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class ExtensionConstants
{
    // (a0+a1*u) * (c-c*u) = c*(a0+a1) + c*(a1-a0)*u.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyConjugatePair(out Fp2 result, in Fp2 a, in Fp c)
    {
        Fp.AddUnreduced(out Fp sum, a.C0, a.C1);
        Fp.SubtractComposite(out Fp difference, a.C1, a.C0);
        Fp.MultiplyComposite(out result.C0, sum, c);
        Fp.MultiplyComposite(out result.C1, difference, c);
    }
}
