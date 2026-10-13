using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Nethermind.Crypto.BlsNet.Hashing;

internal static unsafe partial class Program
{
    private static void TestIsogenyPolynomials()
    {
        for (int i = 0; i < 128; i++)
        {
            CheckIsogeny(RandomFp(), RandomFp(), Fp.One, MapConstants1.x_num, MapConstants1.x_den, MapConstants1.y_num, MapConstants1.y_den);
            CheckIsogeny(RandomFp2(), RandomFp2(), Fp2.One, MapConstants2.x_num, MapConstants2.x_den, MapConstants2.y_num, MapConstants2.y_den);
            Fp scale1; do { scale1 = RandomFp(); } while (scale1.IsZero);
            Fp2 scale2; do { scale2 = RandomFp2(); } while (scale2.IsZero);
            CheckIsogeny(RandomFp(), RandomFp(), scale1, MapConstants1.x_num, MapConstants1.x_den, MapConstants1.y_num, MapConstants1.y_den);
            CheckIsogeny(RandomFp2(), RandomFp2(), scale2, MapConstants2.x_num, MapConstants2.x_den, MapConstants2.y_num, MapConstants2.y_den);
        }
    }

    private static void CheckIsogeny<T>(in T x, in T y, in T scale, ReadOnlySpan<T> xn, ReadOnlySpan<T> xd, ReadOnlySpan<T> yn, ReadOnlySpan<T> yd)
        where T : unmanaged, IField<T>
    {
        T.Square(out T s2, scale); T.Multiply(out T s3, s2, scale);
        T.Multiply(out T px, x, s2); T.Multiply(out T py, y, s3);
        Jacobian<T> p = new(px, py, scale);
        Isogeny<T>.Apply(out Jacobian<T> actual, p, xn, xd, yn, yd);
        EvaluateAffine(out T nx, x, xn, false); EvaluateAffine(out T dx, x, xd, true);
        EvaluateAffine(out T ny, x, yn, false); EvaluateAffine(out T dy, x, yd, true);
        if (T.EqualsZero(dx) || T.EqualsZero(dy)) { Check(actual.IsInfinity, "isogeny pole"); return; }
        T.Inverse(out dx, dx); T.Inverse(out dy, dy);
        T.Multiply(out nx, nx, dx); T.Multiply(out ny, ny, dy); T.Multiply(out ny, ny, y);
        Check(Jacobian<T>.AreEqual(actual, new(nx, ny, T.One)), "isogeny affine polynomial identity");
        Isogeny<T>.Apply(out p, p, xn, xd, yn, yd);
        Check(Jacobian<T>.AreEqual(actual, p), "isogeny projective alias");
    }

    private static void EvaluateAffine<T>(out T r, in T x, ReadOnlySpan<T> coefficients, bool monic)
        where T : unmanaged, IField<T>
    {
        int degree = monic ? coefficients.Length : coefficients.Length - 1;
        r = monic ? T.One : coefficients[^1];
        for (int i = degree - 1; i >= 0; i--) { T.Multiply(out r, r, x); T.Add(out r, r, coefficients[i]); }
    }
}
