// Montgomery constants from blst map_to_g2.c (Apache-2.0).
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Hashing;

internal static class MapConstants2
{
    internal static readonly Fp2 Aprime_E2 = new Fp2(Fp.Zero, Fp.FromMontgomery(0xe53a000003135242UL, 0x01080c0fdef80285UL, 0xe7889edbe340f6bdUL, 0x0b51375126310601UL, 0x02d6985717c744abUL, 0x1220b4e979ea5467UL));
    internal static readonly Fp2 Bprime_E2 = new Fp2(Fp.FromMontgomery(0x22ea00000cf89db2UL, 0x6ec832df71380aa4UL, 0x6e1b94403db5a66eUL, 0x75bf3c53a79473baUL, 0x3dd3a569412c0a34UL, 0x125cdb5e74dc4fd1UL), Fp.FromMontgomery(0x22ea00000cf89db2UL, 0x6ec832df71380aa4UL, 0x6e1b94403db5a66eUL, 0x75bf3c53a79473baUL, 0x3dd3a569412c0a34UL, 0x125cdb5e74dc4fd1UL));
    internal static readonly Fp2 minus_A = new Fp2(Fp.Zero, Fp.FromMontgomery(0xd4c4fffffcec5869UL, 0x1da3f3eed25bfd79UL, 0x7fa833c5136fff67UL, 0x59261433cd540cbdUL, 0x48450f5f2b84682cUL, 0x07e05d00bf959233UL));
    internal static readonly Fp2 Z = new Fp2(Fp.FromMontgomery(0x87ebfffffff9555cUL, 0x656fffe5da8ffffaUL, 0x0fd0749345d33ad2UL, 0xd951e663066576f4UL, 0xde291a3d41e980d3UL, 0x0815664c7dfe040dUL), Fp.FromMontgomery(0x43f5fffffffcaaaeUL, 0x32b7fff2ed47fffdUL, 0x07e83a49a2e99d69UL, 0xeca8f3318332bb7aUL, 0xef148d1ea0f4c069UL, 0x040ab3263eff0206UL));
    internal static readonly Fp2 ZxA = new Fp2(Fp.FromMontgomery(0xe53a000003135242UL, 0x01080c0fdef80285UL, 0xe7889edbe340f6bdUL, 0x0b51375126310601UL, 0x02d6985717c744abUL, 0x1220b4e979ea5467UL), Fp.FromMontgomery(0xa989fffff9d8b0d2UL, 0x3b47e7dda4b7faf3UL, 0xff50678a26dffeceUL, 0xb24c28679aa8197aUL, 0x908a1ebe5708d058UL, 0x0fc0ba017f2b2466UL));
    internal static readonly Fp2 recip_ZZZ = new Fp2(Fp.FromMontgomery(0x65018f5c28f598ebUL, 0xe6020417f022d916UL, 0xd6327313288369c7UL, 0x622ded8eb447156fUL, 0xe52a2aee72c2a01fUL, 0x089812fb8481ffe4UL), Fp.FromMontgomery(0x2574eb851eb8619fUL, 0xdba2e97912925604UL, 0x67e495a909e7a18eUL, 0xdf2da23b8145b8f7UL, 0xcf5d3728310ebf6dUL, 0x11be446236f4c116UL));
    internal static readonly Fp2 magic_ZZZ = new Fp2(Fp.FromMontgomery(0xaa7eb851eb8508e0UL, 0x1c54fdf360989374UL, 0xc87f2fc6e716c62eUL, 0x0124aefb1f9efea7UL, 0xb2f8be63e844865cUL, 0x08b47f775a7ef35aUL), Fp.FromMontgomery(0xe4132bbd838cf70aUL, 0x01d769ac83772c19UL, 0xa83dd6e974c22e45UL, 0xbc8ec3e777b08dffUL, 0xc035c2042ecf5da3UL, 0x073929e97f0850bfUL));
    internal static readonly Fp2[] x_num =
    [
        new Fp2(Fp.FromMontgomery(0x47f671c71ce05e62UL, 0x06dd57071206393eUL, 0x7c80cd2af3fd71a2UL, 0x048103ea9e6cd062UL, 0xc54516acc8d037f6UL, 0x13808f550920ea41UL), Fp.FromMontgomery(0x47f671c71ce05e62UL, 0x06dd57071206393eUL, 0x7c80cd2af3fd71a2UL, 0x048103ea9e6cd062UL, 0xc54516acc8d037f6UL, 0x13808f550920ea41UL)),
        new Fp2(Fp.Zero, Fp.FromMontgomery(0x5fe55555554c71d0UL, 0x873fffdd236aaaa3UL, 0x6a6b4619b26ef918UL, 0x21c2888408874945UL, 0x2836cda7028cabc5UL, 0x0ac73310a7fd5abdUL)),
        new Fp2(Fp.FromMontgomery(0x0a0c5555555971c3UL, 0xdb0c00101f9eaaaeUL, 0xb1fb2f941d797997UL, 0xd3960742ef416e1cUL, 0xb70040e2c20556f4UL, 0x149d7861e581393bUL), Fp.FromMontgomery(0xaff2aaaaaaa638e8UL, 0x439fffee91b55551UL, 0xb535a30cd9377c8cUL, 0x90e144420443a4a2UL, 0x941b66d3814655e2UL, 0x0563998853fead5eUL)),
        new Fp2(Fp.FromMontgomery(0x40aac71c71c725edUL, 0x190955557a84e38eUL, 0xd817050a8f41abc3UL, 0xd86485d4c87f6fb1UL, 0x696eb479f885d059UL, 0x198e1a74328002d2UL), Fp.Zero),
    ];
    internal static readonly Fp2[] x_den =
    [
        new Fp2(Fp.Zero, Fp.FromMontgomery(0x1f3affffff13ab97UL, 0xf25bfc611da3ff3eUL, 0xca3757cb3819b208UL, 0x3e6427366f8cec18UL, 0x03977bc86095b089UL, 0x04f69db13f39a952UL)),
        new Fp2(Fp.FromMontgomery(0x447600000027552eUL, 0xdcb8009a43480020UL, 0x6f7ee9ce4a6e8b59UL, 0xb10330b7c0a95bc6UL, 0x6140b1fcfb1e54b7UL, 0x0381be097f0bb4e1UL), Fp.FromMontgomery(0x7588ffffffd8557dUL, 0x41f3ff646e0bffdfUL, 0xf7b1e8d2ac426acaUL, 0xb3741acd32dbb6f8UL, 0xe9daf5b9482d581fUL, 0x167f53e0ba7431b8UL)),
    ];
    internal static readonly Fp2[] y_num =
    [
        new Fp2(Fp.FromMontgomery(0x96d8f684bdfc77beUL, 0xb530e4f43b66d0e2UL, 0x184a88ff379652fdUL, 0x57cb23ecfae804e1UL, 0x0fd2e39eada3eba9UL, 0x08c8055e31c5d5c3UL), Fp.FromMontgomery(0x96d8f684bdfc77beUL, 0xb530e4f43b66d0e2UL, 0x184a88ff379652fdUL, 0x57cb23ecfae804e1UL, 0x0fd2e39eada3eba9UL, 0x08c8055e31c5d5c3UL)),
        new Fp2(Fp.Zero, Fp.FromMontgomery(0xbf0a71c71c91b406UL, 0x4d6d55d28b7638fdUL, 0x9d82f98e5f205aeeUL, 0xa27aa27b1d1a18d5UL, 0x02c3b2b2d2938e86UL, 0x0c7d13420b09807fUL)),
        new Fp2(Fp.FromMontgomery(0xd7f9555555531c74UL, 0x21cffff748daaaa8UL, 0x5a9ad1866c9bbe46UL, 0x4870a2210221d251UL, 0x4a0db369c0a32af1UL, 0x02b1ccc429ff56afUL), Fp.FromMontgomery(0xe205aaaaaaac8e37UL, 0xfcdc000768795556UL, 0x0c96011a8a1537ddUL, 0x1c06a963f163406eUL, 0x010df44c82a881e6UL, 0x174f45260f808febUL)),
        new Fp2(Fp.FromMontgomery(0xa470bda12f67f35cUL, 0xc0fe38e23327b425UL, 0xc9d3d0f2c6f0678dUL, 0x1c55c9935b5a982eUL, 0x27f6c0e2f0746764UL, 0x117c5e6e28aa9054UL), Fp.Zero),
    ];
    internal static readonly Fp2[] y_den =
    [
        new Fp2(Fp.FromMontgomery(0x0162fffffa765adfUL, 0x8f7bea480083fb75UL, 0x561b3c2259e93611UL, 0x11e19fc1a9c875d5UL, 0xca713efc00367660UL, 0x03c6a03d41da1151UL), Fp.FromMontgomery(0x0162fffffa765adfUL, 0x8f7bea480083fb75UL, 0x561b3c2259e93611UL, 0x11e19fc1a9c875d5UL, 0xca713efc00367660UL, 0x03c6a03d41da1151UL)),
        new Fp2(Fp.Zero, Fp.FromMontgomery(0x5db0fffffd3b02c5UL, 0xd713f52358ebfdbaUL, 0x5ea60761a84d161aUL, 0xbb2c75a34ea6c44aUL, 0x0ac6735921c1119bUL, 0x0ee3d913bdacfbf6UL)),
        new Fp2(Fp.FromMontgomery(0x66b10000003affc5UL, 0xcb1400e764ec0030UL, 0xa73e5eb56fa5d106UL, 0x8984c913a0fe09a9UL, 0x11e10afb78ad7f13UL, 0x05429d0e3e918f52UL), Fp.FromMontgomery(0x534dffffffc4aae6UL, 0x5397ff174c67ffcfUL, 0xbff273eb870b251dUL, 0xdaf2827152870915UL, 0x393a9cbaca9e2dc3UL, 0x14be74dbfaee5748UL)),
    ];
}
