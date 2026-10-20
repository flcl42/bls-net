using System.Runtime.CompilerServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // Internal only: count is public and nonnegative. Inputs may be in [0,2p).
    // All six input words are consumed before writing the canonical output.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SquareNThenMultiply(out Fp result, in Fp value, int count, in Fp factor)
    {
        if (System.Runtime.Intrinsics.Arm.ArmBase.Arm64.IsSupported)
        {
            SquareNThenMultiplyArm64(out result, value, count, factor);
            return;
        }
        ulong a0 = value._l0, a1 = value._l1, a2 = value._l2;
        ulong a3 = value._l3, a4 = value._l4, a5 = value._l5;
        ref ulong modulus = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(ModulusLimbs);
        for (int i = 0; i < count; i++)
        {
            ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
            ulong r6 = 0, r7 = 0, r8 = 0, r9 = 0, r10 = 0, r11 = 0;
            // Accumulate each of the fifteen off-diagonal products once.
            // Row i starts at word 2i+1 and drains into word i+6.
            ulong carry;
            carry = MulAdd(a0, a1, r1, 0, out r1);
            carry = MulAdd(a0, a2, r2, carry, out r2);
            carry = MulAdd(a0, a3, r3, carry, out r3);
            carry = MulAdd(a0, a4, r4, carry, out r4);
            carry = MulAdd(a0, a5, r5, carry, out r5);
            r6 = carry;
            carry = MulAdd(a1, a2, r3, 0, out r3);
            carry = MulAdd(a1, a3, r4, carry, out r4);
            carry = MulAdd(a1, a4, r5, carry, out r5);
            carry = MulAdd(a1, a5, r6, carry, out r6);
            r7 = carry;
            carry = MulAdd(a2, a3, r5, 0, out r5);
            carry = MulAdd(a2, a4, r6, carry, out r6);
            carry = MulAdd(a2, a5, r7, carry, out r7);
            r8 = carry;
            carry = MulAdd(a3, a4, r7, 0, out r7);
            carry = MulAdd(a3, a5, r8, carry, out r8);
            r9 = carry;
            carry = MulAdd(a4, a5, r9, 0, out r9);
            r10 = carry;

            // Double the cross terms.
            ulong crossCarry = 0;
            r1 = AddCarry(r1, r1, ref crossCarry);
            r2 = AddCarry(r2, r2, ref crossCarry);
            r3 = AddCarry(r3, r3, ref crossCarry);
            r4 = AddCarry(r4, r4, ref crossCarry);
            r5 = AddCarry(r5, r5, ref crossCarry);
            r6 = AddCarry(r6, r6, ref crossCarry);
            r7 = AddCarry(r7, r7, ref crossCarry);
            r8 = AddCarry(r8, r8, ref crossCarry);
            r9 = AddCarry(r9, r9, ref crossCarry);
            r10 = AddCarry(r10, r10, ref crossCarry);
            // Cross products end at a4*a5, below bit 704 even after doubling.
            // The final diagonal addition supplies the top word and its carry.
            ulong diagonalCarry = 0;
            ulong h0 = Math.BigMul(a0, a0, out r0);
            r1 = AddCarry(r1, h0, ref diagonalCarry);
            ulong h1 = Math.BigMul(a1, a1, out ulong d1);
            r2 = AddCarry(r2, d1, ref diagonalCarry);
            r3 = AddCarry(r3, h1, ref diagonalCarry);
            ulong h2 = Math.BigMul(a2, a2, out ulong d2);
            r4 = AddCarry(r4, d2, ref diagonalCarry);
            r5 = AddCarry(r5, h2, ref diagonalCarry);
            ulong h3 = Math.BigMul(a3, a3, out ulong d3);
            r6 = AddCarry(r6, d3, ref diagonalCarry);
            r7 = AddCarry(r7, h3, ref diagonalCarry);
            ulong h4 = Math.BigMul(a4, a4, out ulong d4);
            r8 = AddCarry(r8, d4, ref diagonalCarry);
            r9 = AddCarry(r9, h4, ref diagonalCarry);
            ulong h5 = Math.BigMul(a5, a5, out ulong d5);
            r10 = AddCarry(r10, d5, ref diagonalCarry);
            r11 = h5 + diagonalCarry;
            // REDC low half, then add the high half. For a < 2p,
            // a*a < 4p*p < pR, hence the next value stays below 2p.
            for (int j = 0; j < 6; j += 2)
            {
                ulong q = r0 * N0;
                carry = MulAdd(q, Unsafe.Add(ref modulus, 0), r0, 0, out _);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 1), r1, carry, out r0);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 2), r2, carry, out r1);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 3), r3, carry, out r2);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 4), r4, carry, out r3);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 5), r5, carry, out r4);
                r5 = carry;
                q = r0 * N0;
                carry = MulAdd(q, Unsafe.Add(ref modulus, 0), r0, 0, out _);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 1), r1, carry, out r0);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 2), r2, carry, out r1);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 3), r3, carry, out r2);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 4), r4, carry, out r3);
                carry = MulAdd(q, Unsafe.Add(ref modulus, 5), r5, carry, out r4);
                r5 = carry;
            }
            ulong highCarry = 0;
            a0 = AddCarry(r0, r6, ref highCarry);
            a1 = AddCarry(r1, r7, ref highCarry);
            a2 = AddCarry(r2, r8, ref highCarry);
            a3 = AddCarry(r3, r9, ref highCarry);
            a4 = AddCarry(r4, r10, ref highCarry);
            a5 = AddCarry(r5, r11, ref highCarry);
        }
        Fp power = new Fp(a0, a1, a2, a3, a4, a5);
        Multiply(out result, power, factor);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SquareNThenMultiplyArm64(out Fp result, in Fp value, int count, in Fp factor)
    {
        Fp power = value;
        for (int i = 0; i < count; i++) SquareLazyArm64(out power, power);
        Multiply(out result, power, factor);
    }

    // Multiplying by Montgomery one canonicalizes the lazy result while sharing the kernel.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SquareN(out Fp result, in Fp value, int count)
    {
        SquareNThenMultiply(out result, value, count, One);
    }
}
