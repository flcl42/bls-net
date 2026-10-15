using System.Buffers.Binary;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fr
{
    /// <summary>Reduces exactly 32 secret little-endian bytes modulo r, in constant time. Length is public.</summary>
    public static Fr FromLittleEndianConstantTime(ReadOnlySpan<byte> source)
    {
        if (source.Length != ByteCount) throw new ArgumentException("A scalar must contain exactly 32 bytes.", nameof(source));
        ulong a = BinaryPrimitives.ReadUInt64LittleEndian(source);
        ulong b = BinaryPrimitives.ReadUInt64LittleEndian(source[8..]);
        ulong c = BinaryPrimitives.ReadUInt64LittleEndian(source[16..]);
        ulong d = BinaryPrimitives.ReadUInt64LittleEndian(source[24..]);
        // 2^256 < 3r: two unconditional trial subtractions suffice.
        Reduce(out Fr raw, a, b, c, d);
        Reduce(out raw, raw._l0, raw._l1, raw._l2, raw._l3);
        Fr r2 = new(0xc999e990f3f29c6d, 0x2b6cedcb87925c23, 0x05d314967254398f, 0x0748d9d99f59ff11);
        Multiply(out Fr result, raw, r2);
        return result;
    }

    /// <summary>Serializes a secret scalar as 32 canonical little-endian bytes. Destination length is public.</summary>
    public void ToLittleEndianConstantTime(Span<byte> destination)
    {
        if (destination.Length < ByteCount) throw new ArgumentException("Destination must contain at least 32 bytes.", nameof(destination));
        Multiply(out Fr raw, this, new Fr(1, 0, 0, 0));
        BinaryPrimitives.WriteUInt64LittleEndian(destination, raw._l0);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], raw._l1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], raw._l2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[24..], raw._l3);
    }

}
