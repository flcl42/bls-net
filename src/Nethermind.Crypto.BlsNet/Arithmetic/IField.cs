namespace Nethermind.Crypto.BlsNet.Arithmetic;

/// <summary>
/// Field operations shared by Fp and Fp2, so curve arithmetic can be written once for G1 and G2.
/// Generic code over a struct implementing this interface is specialized by the JIT, so the
/// calls are direct and inlinable. Every operation tolerates its result aliasing an input.
/// </summary>
public interface IField<T> where T : struct, IField<T>
{
    static abstract T Zero { get; }

    static abstract T One { get; }

    static abstract bool EqualsZero(in T value);

    static abstract bool AreEqual(in T left, in T right);

    static abstract void Add(out T result, in T left, in T right);

    static abstract void Subtract(out T result, in T left, in T right);

    static abstract void Negate(out T result, in T value);

    static abstract void Double(out T result, in T value);

    static abstract void MulBy3(out T result, in T value);

    static abstract void MulBy4(out T result, in T value);

    static abstract void MulBy8(out T result, in T value);

    static abstract void Multiply(out T result, in T left, in T right);

    static abstract void Square(out T result, in T value);

    /// <summary>1/value, and 0 for 0.</summary>
    static abstract void Inverse(out T result, in T value);
}
