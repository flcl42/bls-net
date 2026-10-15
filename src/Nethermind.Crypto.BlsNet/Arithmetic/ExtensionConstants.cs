using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

internal static class ExtensionConstants
{
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyImaginary(out Fp2 result, in Fp2 a, in Fp c)
    {
        Fp coefficient = c;
        Fp.MultiplyComposite(out Fp imaginary, a.C0, coefficient);
        Fp.MultiplyComposite(out Fp real, a.C1, coefficient);
        Fp.Negate(out result.C0, real); result.C1 = imaginary;
    }

    // (a0+a1*u)*(c+c*u) = c*(a0-a1)+c*(a0+a1)*u.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyEqualPair(out Fp2 result, in Fp2 a, in Fp c)
    {
        Fp coefficient = c;
        Fp.SubtractComposite(out Fp difference, a.C0, a.C1);
        Fp.AddUnreduced(out Fp sum, a.C0, a.C1);
        Fp.MultiplyComposite(out result.C0, difference, coefficient);
        Fp.MultiplyComposite(out result.C1, sum, coefficient);
    }

    // The SSWU constant Z=-2-u needs only small field operations.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplySwuZ(out Fp2 result, in Fp2 a)
    {
        Fp.Double(out Fp twice0, a.C0); Fp.Double(out Fp twice1, a.C1);
        Fp.Add(out Fp imaginary, a.C0, twice1);
        Fp.Subtract(out result.C0, a.C1, twice0); Fp.Negate(out result.C1, imaginary);
    }

    // (a0+a1*u) * (c-c*u) = c*(a0+a1) + c*(a1-a0)*u.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void MultiplyConjugatePair(out Fp2 result, in Fp2 a, in Fp c)
    {
        // c may alias a coefficient of result; copy it before the first write.
        Fp cc = c;
        Fp.AddUnreduced(out Fp sum, a.C0, a.C1);
        Fp.SubtractComposite(out Fp difference, a.C1, a.C0);
        Fp.MultiplyComposite(out result.C0, sum, cc);
        Fp.MultiplyComposite(out result.C1, difference, cc);
    }
}
