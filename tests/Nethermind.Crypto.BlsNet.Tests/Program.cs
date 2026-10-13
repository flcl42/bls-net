using System.Numerics;
using System.Security.Cryptography;
using Nethermind.Crypto.BlsNet.Arithmetic;

const string FrModulusHex = "73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001";
const string FpModulusHex = "1a0111ea397fe69a4b1ba7b6434bacd764774b84f38512bf6730d2a0f6b0f6241eabfffeb153ffffb9feffffffffaaab";

BigInteger frModulus = BigInteger.Parse("0" + FrModulusHex, System.Globalization.NumberStyles.HexNumber);
BigInteger fpModulus = BigInteger.Parse("0" + FpModulusHex, System.Globalization.NumberStyles.HexNumber);

Run("Fr round-trips and rejects non-canonical input", TestFrParsing);
Run("Fp round-trips and rejects non-canonical input", TestFpParsing);
Run("Fr arithmetic matches BigInteger", () => TestFrArithmetic(frModulus));
Run("Fp arithmetic matches BigInteger", () => TestFpArithmetic(fpModulus));
Run("Fr arithmetic matches BigInteger on edge values", () => TestFrEdgeArithmetic(frModulus));
Run("Fp arithmetic matches BigInteger on edge values", () => TestFpEdgeArithmetic(fpModulus));
Run("Hot arithmetic path stays allocation-free", TestNoHotPathAllocations);

Console.WriteLine("All managed arithmetic tests passed.");

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"[pass] {name}");
}

static void TestFrParsing()
{
    Span<byte> bytes = stackalloc byte[Fr.ByteCount];
    Fr.One.ToBigEndian(bytes);
    Assert(Fr.TryFromBigEndian(bytes, out Fr one), "Fr one should parse.");
    Assert(one == Fr.One, "Fr one should round-trip.");

    HexToBytes(FrModulusHex, bytes);
    Assert(!Fr.TryFromBigEndian(bytes, out _), "Fr modulus must be rejected.");
}

static void TestFpParsing()
{
    Span<byte> bytes = stackalloc byte[Fp.ByteCount];
    Fp.One.ToBigEndian(bytes);
    Assert(Fp.TryFromBigEndian(bytes, out Fp one), "Fp one should parse.");
    Assert(one == Fp.One, "Fp one should round-trip.");

    HexToBytes(FpModulusHex, bytes);
    Assert(!Fp.TryFromBigEndian(bytes, out _), "Fp modulus must be rejected.");
}

static void TestFrArithmetic(BigInteger modulus)
{
    for (int i = 0; i < 250; i++)
    {
        Fr a = RandomFr(out BigInteger ai);
        Fr b = RandomFr(out BigInteger bi);

        AssertEqualFr(Mod(ai + bi, modulus), a.Add(b), modulus);
        AssertEqualFr(Mod(ai - bi, modulus), a.Subtract(b), modulus);
        AssertEqualFr(Mod(-ai, modulus), a.Negate(), modulus);
        AssertEqualFr(Mod(ai * bi, modulus), a.Multiply(b), modulus);
        AssertEqualFr(Mod(ai * ai, modulus), a.Square(), modulus);
    }
}

static void TestFpArithmetic(BigInteger modulus)
{
    for (int i = 0; i < 250; i++)
    {
        Fp a = RandomFp(out BigInteger ai);
        Fp b = RandomFp(out BigInteger bi);

        AssertEqualFp(Mod(ai + bi, modulus), a.Add(b), modulus);
        AssertEqualFp(Mod(ai - bi, modulus), a.Subtract(b), modulus);
        AssertEqualFp(Mod(-ai, modulus), a.Negate(), modulus);
        AssertEqualFp(Mod(ai * bi, modulus), a.Multiply(b), modulus);
        AssertEqualFp(Mod(ai * ai, modulus), a.Square(), modulus);
    }
}

// Values next to the modulus, plus values whose Montgomery limbs (value * R mod modulus) sit next to
// the modulus or to limb boundaries, so that the kernels' carry and borrow chains hit their edges.
static BigInteger[] EdgeValues(BigInteger modulus, int montgomeryBits)
{
    BigInteger limb = BigInteger.One << 64;
    BigInteger rInverse = BigInteger.ModPow(BigInteger.One << montgomeryBits, modulus - 2, modulus);
    BigInteger[] limbs = [0, 1, modulus - 1, modulus - 2, modulus / 2, limb - 1, limb, limb * limb - 1, limb * limb];
    return [0, 1, 2, modulus - 1, modulus - 2, modulus / 2, modulus / 2 + 1, .. limbs.Select(l => l * rInverse % modulus)];
}

static void TestFrEdgeArithmetic(BigInteger modulus)
{
    const int montgomeryBits = 256;
    Span<byte> bytes = stackalloc byte[Fr.ByteCount];
    foreach (BigInteger ai in EdgeValues(modulus, montgomeryBits))
    {
        foreach (BigInteger bi in EdgeValues(modulus, montgomeryBits))
        {
            WriteBigEndian(ai, bytes);
            Assert(Fr.TryFromBigEndian(bytes, out Fr a), "Fr edge value should parse.");
            WriteBigEndian(bi, bytes);
            Assert(Fr.TryFromBigEndian(bytes, out Fr b), "Fr edge value should parse.");

            AssertEqualFr(Mod(ai + bi, modulus), a.Add(b), modulus);
            AssertEqualFr(Mod(ai - bi, modulus), a.Subtract(b), modulus);
            AssertEqualFr(Mod(-ai, modulus), a.Negate(), modulus);
            AssertEqualFr(Mod(ai * bi, modulus), a.Multiply(b), modulus);
            AssertEqualFr(Mod(ai * ai, modulus), a.Square(), modulus);
        }
    }
}

static void TestFpEdgeArithmetic(BigInteger modulus)
{
    const int montgomeryBits = 384;
    Span<byte> bytes = stackalloc byte[Fp.ByteCount];
    foreach (BigInteger ai in EdgeValues(modulus, montgomeryBits))
    {
        foreach (BigInteger bi in EdgeValues(modulus, montgomeryBits))
        {
            WriteBigEndian(ai, bytes);
            Assert(Fp.TryFromBigEndian(bytes, out Fp a), "Fp edge value should parse.");
            WriteBigEndian(bi, bytes);
            Assert(Fp.TryFromBigEndian(bytes, out Fp b), "Fp edge value should parse.");

            AssertEqualFp(Mod(ai + bi, modulus), a.Add(b), modulus);
            AssertEqualFp(Mod(ai - bi, modulus), a.Subtract(b), modulus);
            AssertEqualFp(Mod(-ai, modulus), a.Negate(), modulus);
            AssertEqualFp(Mod(ai * bi, modulus), a.Multiply(b), modulus);
            AssertEqualFp(Mod(ai * ai, modulus), a.Square(), modulus);
        }
    }
}

static void TestNoHotPathAllocations()
{
    Fr frA = Fr.FromUInt64(3);
    Fr frB = Fr.FromUInt64(5);
    Fr fr = Fr.One;
    Fp fpA = Fp.FromUInt64(7);
    Fp fpB = Fp.FromUInt64(11);
    Fp fp = Fp.One;

    for (int i = 0; i < 1_000; i++)
    {
        Fr.Multiply(out fr, fr, frA);
        Fr.Add(out fr, fr, frB);
        Fp.Multiply(out fp, fp, fpA);
        Fp.Add(out fp, fp, fpB);
    }

    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10_000; i++)
    {
        Fr.Multiply(out fr, fr, frA);
        Fr.Add(out fr, fr, frB);
        Fp.Multiply(out fp, fp, fpA);
        Fp.Add(out fp, fp, fpB);
    }

    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert(allocated == 0, $"Expected zero hot-path allocations, got {allocated} bytes.");
}

static Fr RandomFr(out BigInteger value)
{
    Span<byte> bytes = stackalloc byte[Fr.ByteCount];
    while (true)
    {
        RandomNumberGenerator.Fill(bytes);
        bytes[0] &= 0x7f;
        if (Fr.TryFromBigEndian(bytes, out Fr element))
        {
            value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            return element;
        }
    }
}

static Fp RandomFp(out BigInteger value)
{
    Span<byte> bytes = stackalloc byte[Fp.ByteCount];
    while (true)
    {
        RandomNumberGenerator.Fill(bytes);
        bytes[0] &= 0x1f;
        if (Fp.TryFromBigEndian(bytes, out Fp element))
        {
            value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            return element;
        }
    }
}

static void AssertEqualFr(BigInteger expected, Fr actual, BigInteger modulus)
{
    Span<byte> bytes = stackalloc byte[Fr.ByteCount];
    actual.ToBigEndian(bytes);
    BigInteger actualInteger = new(bytes, isUnsigned: true, isBigEndian: true);
    Assert(actualInteger == Mod(expected, modulus), $"Fr mismatch: expected {expected}, got {actualInteger}.");

    // Equality compares the Montgomery limbs, so a result that is congruent but not fully reduced fails.
    WriteBigEndian(Mod(expected, modulus), bytes);
    Assert(Fr.TryFromBigEndian(bytes, out Fr canonical) && actual == canonical, $"Fr result for {expected} is not reduced.");
}

static void AssertEqualFp(BigInteger expected, Fp actual, BigInteger modulus)
{
    Span<byte> bytes = stackalloc byte[Fp.ByteCount];
    actual.ToBigEndian(bytes);
    BigInteger actualInteger = new(bytes, isUnsigned: true, isBigEndian: true);
    Assert(actualInteger == Mod(expected, modulus), $"Fp mismatch: expected {expected}, got {actualInteger}.");

    // Equality compares the Montgomery limbs, so a result that is congruent but not fully reduced fails.
    WriteBigEndian(Mod(expected, modulus), bytes);
    Assert(Fp.TryFromBigEndian(bytes, out Fp canonical) && actual == canonical, $"Fp result for {expected} is not reduced.");
}

static void WriteBigEndian(BigInteger value, Span<byte> destination)
{
    destination.Clear();
    bool written = value.TryWriteBytes(
        destination[^value.GetByteCount(isUnsigned: true)..], out _, isUnsigned: true, isBigEndian: true);
    Assert(written, "Value does not fit.");
}

static BigInteger Mod(BigInteger value, BigInteger modulus)
{
    value %= modulus;
    return value.Sign < 0 ? value + modulus : value;
}

static void HexToBytes(string hex, Span<byte> destination)
{
    byte[] decoded = Convert.FromHexString(hex);
    Assert(decoded.Length == destination.Length, "Hex decode length mismatch.");
    decoded.CopyTo(destination);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
