// Montgomery constants from blst e1.c and e2.c (Apache-2.0).
// Copyright Supranational LLC. Adapted for this C# rewrite; see NOTICE and LICENSE.
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Curves;

internal static class CurveConstants
{
    internal static readonly Fp Beta = Fp.FromMontgomery(0xcd03c9e48671f071UL, 0x5dab22461fcda5d2UL, 0x587042afd3851b95UL, 0x8eb60ebe01bacb9eUL, 0x03f97d6e83d050d2UL, 0x18f0206554638741UL);
    internal static readonly Fp2 frobenius_x = new Fp2(Fp.Zero, Fp.FromMontgomery(0x890dc9e4867545c3UL, 0x2af322533285a5d5UL, 0x50880866309b7e2cUL, 0xa20d1b8c7e881024UL, 0x14e4f04fe2db9068UL, 0x14e56d3f1564853aUL));
    internal static readonly Fp2 frobenius_y = new Fp2(Fp.FromMontgomery(0x3e2f585da55c9ad1UL, 0x4294213d86c18183UL, 0x382844c88b623732UL, 0x92ad2afd19103e18UL, 0x1d794e4fac7cf0b9UL, 0x0bd592fc7d825ec8UL), Fp.FromMontgomery(0x7bcfa7a25aa30fdaUL, 0xdc17dec12a927e7cUL, 0x2f088dd86b4ebef1UL, 0xd1ca2087da74d4a7UL, 0x2da2596696cebc1dUL, 0x0e2b7eedbbfd87d2UL));
    internal static readonly G1Affine Generator1 = new(Fp.FromMontgomery(0x5cb38790fd530c16UL, 0x7817fc679976fff5UL, 0x154f95c7143ba1c1UL, 0xf0ae6acdf3d0e747UL, 0xedce6ecc21dbf440UL, 0x120177419e0bfb75UL), Fp.FromMontgomery(0xbaac93d50ce72271UL, 0x8c22631a7918fd8eUL, 0xdd595f13570725ceUL, 0x51ac582950405194UL, 0x0e1c8c3fad0059c0UL, 0x0bbc3efc5008a26aUL));
    internal static readonly G2Affine Generator2 = new(new Fp2(Fp.FromMontgomery(0xf5f28fa202940a10UL, 0xb3f5fb2687b4961aUL, 0xa1a893b53e2ae580UL, 0x9894999d1a3caee9UL, 0x6f67b7631863366bUL, 0x058191924350bcd7UL), Fp.FromMontgomery(0xa5a9c0759e23f606UL, 0xaaa0c59dbccd60c3UL, 0x3bb17e18e2867806UL, 0x1b1ab6cc8541b367UL, 0xc2b6ed0ef2158547UL, 0x11922a097360edf3UL)), new Fp2(Fp.FromMontgomery(0x4c730af860494c4aUL, 0x597cfa1f5e369c5aUL, 0xe7e6856caa0a635aUL, 0xbbefb5e96e0d495fUL, 0x07d3a975f0ef25a2UL, 0x0083fd8e7e80dae5UL), Fp.FromMontgomery(0xadc0fc92df64b05dUL, 0x18aa270a2b1461dcUL, 0x86adac6a3be4eba0UL, 0x79495c4ec93da33aUL, 0xe7175850a43ccaedUL, 0x0b2bc2a163de1bf2UL)));
}
