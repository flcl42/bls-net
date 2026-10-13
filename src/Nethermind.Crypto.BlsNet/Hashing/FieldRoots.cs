using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

/// <summary>Variable-time square roots and reciprocal roots used by the maps.</summary>
public static class FieldRoots
{
    internal static bool ReciprocalSqrt(out Fp r, in Fp a)
    {
        FpCurveRoots.ReciprocalSqrt(out r, a); Fp.Multiply(out Fp t, r, a); Fp.Square(out t, t);
        return Fp.AreEqual(t, a);
    }

    private static bool Align(out Fp2 r, in Fp2 candidate, in Fp2 a)
    {
        Fp2.Square(out Fp2 t, candidate);
        if (Fp2.AreEqual(t, a)) { r = candidate; return true; }
        Fp2.Negate(out Fp2 nt, t);
        if (Fp2.AreEqual(nt, a)) { Fp2.Multiply(out r, candidate, new Fp2(Fp.Zero, Fp.One)); return true; }
        Fp.Negate(out Fp nc0, t.C0);
        if (Fp2.AreEqual(new Fp2(t.C1, nc0), a))
        { Fp2.Multiply(out r, candidate, CurveConstants.frobenius_y); return true; }
        Fp.Negate(out Fp nc1, t.C1);
        if (Fp2.AreEqual(new Fp2(nc1, t.C0), a))
        { Fp2.Multiply(out r, candidate, new Fp2(CurveConstants.frobenius_y.C1, CurveConstants.frobenius_y.C1)); return true; }
        r = candidate; return false;
    }

    private static void Candidate(out Fp2 r, in Fp2 a, in Fp normRoot)
    {
        Fp.Add(out Fp t, a.C0, normRoot);
        if (t.IsZero) Fp.Subtract(out t, a.C0, normRoot);
        Fp.Halve(out t, t); ReciprocalSqrt(out Fp ir, t);
        Fp.Multiply(out Fp x, ir, t); Fp.Halve(out Fp y, a.C1); Fp.Multiply(out y, y, ir);
        r = new(x, y);
    }

    /// <summary>Computes a square root in Fp2 if one exists; variable-time.</summary>
    public static bool Sqrt(out Fp2 r, in Fp2 a)
    {
        Fp.Square(out Fp norm, a.C0); Fp.Square(out Fp t, a.C1); Fp.Add(out norm, norm, t);
        Fp.Sqrt(out norm, norm); Candidate(out Fp2 c, a, norm); return Align(out r, c, a);
    }

    internal static bool ReciprocalSqrt(out Fp2 r, in Fp2 a)
    {
        Fp.Square(out Fp norm, a.C0); Fp.Square(out Fp t, a.C1); Fp.Add(out norm, norm, t);
        bool square = ReciprocalSqrt(out Fp ir, norm);
        Fp2 adjusted = a;
        if (!square)
        {
            Fp2.Multiply(out adjusted, a, MapConstants2.recip_ZZZ);
            Fp.Multiply(out norm, norm, MapConstants2.magic_ZZZ.C0);
            Fp.Multiply(out ir, ir, MapConstants2.magic_ZZZ.C1);
        }
        Fp.Multiply(out norm, norm, ir); Candidate(out Fp2 c, adjusted, norm); Align(out c, c, adjusted);
        Fp.Multiply(out Fp x, c.C0, ir); Fp.Multiply(out Fp y, c.C1, ir); Fp.Negate(out y, y);
        r = new(x, y); return square;
    }
}