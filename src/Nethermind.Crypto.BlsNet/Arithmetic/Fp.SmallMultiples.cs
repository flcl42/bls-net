using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // Canonical input; 8p < R, so shifting loses no bits. Three trial
    // subtractions (4p, 2p, p) suffice, with no intermediate canonical sums.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MulBy8Fast(out Fp result, in Fp value)
    {
        ulong r0 = (value._l0 << 3);
        ulong r1 = (value._l1 << 3) | (value._l0 >> 61);
        ulong r2 = (value._l2 << 3) | (value._l1 >> 61);
        ulong r3 = (value._l3 << 3) | (value._l2 >> 61);
        ulong r4 = (value._l4 << 3) | (value._l3 >> 61);
        ulong r5 = (value._l5 << 3) | (value._l4 >> 61);
        {
            ulong s0 = r0 + 0x1804000000015554UL;
            ulong carry = s0 < r0 ? 1UL : 0UL;
            ulong s1 = AddCarry(r1, 0x855000053ab00001UL, ref carry);
            ulong s2 = AddCarry(r2, 0x633cb57c253c276fUL, ref carry);
            ulong s3 = AddCarry(r3, 0x6e22d1ec31ebb502UL, ref carry);
            ulong s4 = AddCarry(r4, 0xd3916126f2d14ca2UL, ref carry);
            ulong s5 = AddCarry(r5, 0x97fbb8571a006596UL, ref carry);
            ulong mask = ChoiceToMask(carry);
            r0 = Select(s0, r0, mask);
            r1 = Select(s1, r1, mask);
            r2 = Select(s2, r2, mask);
            r3 = Select(s3, r3, mask);
            r4 = Select(s4, r4, mask);
            r5 = Select(s5, r5, mask);
        }
        {
            ulong s0 = r0 + 0x8c0200000000aaaaUL;
            ulong carry = s0 < r0 ? 1UL : 0UL;
            ulong s1 = AddCarry(r1, 0xc2a800029d580000UL, ref carry);
            ulong s2 = AddCarry(r2, 0x319e5abe129e13b7UL, ref carry);
            ulong s3 = AddCarry(r3, 0x371168f618f5da81UL, ref carry);
            ulong s4 = AddCarry(r4, 0x69c8b0937968a651UL, ref carry);
            ulong s5 = AddCarry(r5, 0xcbfddc2b8d0032cbUL, ref carry);
            ulong mask = ChoiceToMask(carry);
            r0 = Select(s0, r0, mask);
            r1 = Select(s1, r1, mask);
            r2 = Select(s2, r2, mask);
            r3 = Select(s3, r3, mask);
            r4 = Select(s4, r4, mask);
            r5 = Select(s5, r5, mask);
        }
        Reduce(out result, r0, r1, r2, r3, r4, r5);
    }

    // Canonical inputs; return a + p - b in [0,2p), without selecting a correction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SubtractBiased(out Fp result, in Fp a, in Fp b)
    {
        ulong borrow = 0;
        ulong r0 = SubBorrow(a._l0, b._l0, ref borrow);
        ulong r1 = SubBorrow(a._l1, b._l1, ref borrow);
        ulong r2 = SubBorrow(a._l2, b._l2, ref borrow);
        ulong r3 = SubBorrow(a._l3, b._l3, ref borrow);
        ulong r4 = SubBorrow(a._l4, b._l4, ref borrow);
        ulong r5 = SubBorrow(a._l5, b._l5, ref borrow);
        ulong carry = 0;
        r0 = AddCarry(r0, M0, ref carry);
        r1 = AddCarry(r1, M1, ref carry);
        r2 = AddCarry(r2, M2, ref carry);
        r3 = AddCarry(r3, M3, ref carry);
        r4 = AddCarry(r4, M4, ref carry);
        r5 = AddCarry(r5, M5, ref carry);
        result = new Fp(r0, r1, r2, r3, r4, r5);
    }
}
