using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

internal static class SecretRoots
{
    internal static ulong ReciprocalSqrt(out Fp r, in Fp a)
    {
        Fp input = a;
        FpCurveRoots.ReciprocalSqrt(out r, input); Fp.MultiplyComposite(out Fp t, r, input);
        CurveField<Fp>.Square(out t, t); return Fp.EqualMask(t, input);
    }

    private static ulong Align(out Fp2 r, in Fp2 candidate, in Fp2 a)
    {
        Fp2 c = candidate;
        Fp2.Square(out Fp2 t, c); ulong found = Fp2.EqualMask(t, a); r = c;
        Fp2.Negate(out Fp2 nt, t); ulong match = Fp2.EqualMask(nt, a);
        Fp.Negate(out Fp imaginaryX, c.C1);
        Fp2.Select(out r, new Fp2(imaginaryX, c.C0), r, match & ~found); found |= match;
        Fp.Negate(out Fp nc0, t.C0); match = Fp2.EqualMask(new Fp2(t.C1, nc0), a);
        ExtensionConstants.MultiplyConjugatePair(out Fp2 rotated, c, CurveConstants.frobenius_y.C0);
        Fp2.Select(out r, rotated, r, match & ~found); found |= match;
        Fp.Negate(out Fp nc1, t.C1); match = Fp2.EqualMask(new Fp2(nc1, t.C0), a);
        ExtensionConstants.MultiplyEqualPair(out rotated, c, CurveConstants.frobenius_y.C1);
        Fp2.Select(out r, rotated, r, match & ~found); return found | match;
    }

    private static void Candidate(out Fp2 r, in Fp2 a, in Fp normRoot)
    {
        Fp.Add(out Fp t, a.C0, normRoot); Fp.Subtract(out Fp alternative, a.C0, normRoot);
        Fp.Select(out t, alternative, t, Fp.IsZeroMask(t));
        Fp.Halve(out t, t); ReciprocalSqrt(out Fp ir, t);
        Fp.MultiplyComposite(out Fp x, ir, t); Fp.Halve(out Fp y, a.C1); Fp.MultiplyComposite(out y, y, ir);
        r = new(x, y);
    }

    internal static ulong Sqrt(out Fp2 r, in Fp2 a)
    {
        Fp2 input = a;
        Fp.MultiplyComposite(out Fp norm, input.C0, input.C0); Fp.MultiplyComposite(out Fp t, input.C1, input.C1);
        Fp.Add(out norm, norm, t); Fp.SqrtConstantTime(out norm, norm);
        Candidate(out Fp2 c, input, norm); return Align(out r, c, input);
    }

    internal static ulong ReciprocalSqrt(out Fp2 r, in Fp2 a)
    {
        Fp2 input = a;
        Fp.MultiplyComposite(out Fp norm, input.C0, input.C0); Fp.MultiplyComposite(out Fp t, input.C1, input.C1);
        Fp.Add(out norm, norm, t); ulong square = ReciprocalSqrt(out Fp ir, norm);
        Fp2.Multiply(out Fp2 adjusted, input, MapConstants2.recip_ZZZ);
        Fp2.Select(out adjusted, input, adjusted, square);
        Fp.MultiplyComposite(out Fp alternative, norm, MapConstants2.magic_ZZZ.C0);
        Fp.Select(out norm, norm, alternative, square);
        Fp.MultiplyComposite(out alternative, ir, MapConstants2.magic_ZZZ.C1);
        Fp.Select(out ir, ir, alternative, square);
        Fp.MultiplyComposite(out norm, norm, ir); Candidate(out Fp2 c, adjusted, norm); Align(out c, c, adjusted);
        Fp.MultiplyComposite(out Fp x, c.C0, ir); Fp.MultiplyComposite(out Fp y, c.C1, ir); Fp.Negate(out y, y);
        r = new(x, y); return square;
    }
}
