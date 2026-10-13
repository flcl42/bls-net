// Montgomery constants from blst map_to_g1.c (Apache-2.0).
using Nethermind.Crypto.BlsNet.Arithmetic;

namespace Nethermind.Crypto.BlsNet.Hashing;

internal static class MapConstants1
{
    internal static readonly Fp Aprime_E1 = Fp.FromMontgomery(0x2f65aa0e9af5aa51UL, 0x86464c2d1e8416c3UL, 0xb85ce591b7bd31e2UL, 0x27e11c91b5f24e7cUL, 0x28376eda6bfc1835UL, 0x155455c3e5071d85UL);
    internal static readonly Fp Bprime_E1 = Fp.FromMontgomery(0xfb996971fe22a1e0UL, 0x9aa93eb35b742d6fUL, 0x8c476013de99c5c4UL, 0x873e27c3a221e571UL, 0xca72b5e45a52d888UL, 0x06824061418a386bUL);
    internal static readonly Fp minus_A = Fp.FromMontgomery(0x8a9955f1650a005aUL, 0x9865b3d192cfe93cUL, 0xaed3ed0f3ef3c441UL, 0x3c962ef33d92c442UL, 0x22e438dbd74f94a2UL, 0x04acbc265478c915UL);
    internal static readonly Fp Z = Fp.FromMontgomery(0x886c00000023ffdcUL, 0x0f70008d3090001dUL, 0x77672417ed5828c3UL, 0x9dac23e943dc1740UL, 0x50553f1b9c131521UL, 0x078c712fbe0ab6e8UL);
    internal static readonly Fp ZxA = Fp.FromMontgomery(0x7f674ea0a8915178UL, 0xb0f945fc13b8fa65UL, 0x4b46759a38e87d76UL, 0x2e7a929641bbb6a1UL, 0x1668ddfa462bf6b6UL, 0x00960e2ed1cf294cUL);
    internal static readonly Fp sqrt_minus_ZZZ = Fp.FromMontgomery(0x43b571cad3215f1fUL, 0xccb460ef1c702dc2UL, 0x742d884f4f97100bUL, 0xdb2c3e3238a3382bUL, 0xe40f3fa13fce8f88UL, 0x0073a2af9892a2ffUL);
    internal static readonly Fp[] x_num =
    [
        Fp.FromMontgomery(0x4d18b6f3af00131cUL, 0x19fa219793fee28cUL, 0x3f2885f1467f19aeUL, 0x23dcea34f2ffb304UL, 0xd15b58d2ffc00054UL, 0x0913be200a20bef4UL),
        Fp.FromMontgomery(0x898985385cdbbd8bUL, 0x3c79e43cc7d966aaUL, 0x1597e193f4cd233aUL, 0x8637ef1e4d6623adUL, 0x11b22deed20d827bUL, 0x07097bc5998784adUL),
        Fp.FromMontgomery(0xa542583a480b664bUL, 0xfc7169c026e568c6UL, 0x5ba2ef314ed8b5a6UL, 0x5b5491c05102f0e7UL, 0xdf6e99707d2a0079UL, 0x0784151ed7605524UL),
        Fp.FromMontgomery(0x494e212870f72741UL, 0xab9be52fbda43021UL, 0x26f5577994e34c3dUL, 0x049dfee82aefbd60UL, 0x65dadd7828505289UL, 0x0e93d431ea011aebUL),
        Fp.FromMontgomery(0x90ee774bd6a74d45UL, 0x7ada1c8a41bfb185UL, 0x0f1a8953b325f464UL, 0x104c24211be4805cUL, 0x169139d319ea7a8fUL, 0x09f20ead8e532bf6UL),
        Fp.FromMontgomery(0x6ddd93e2f43626b7UL, 0xa5482c9aa1ccd7bdUL, 0x143245631883f4bdUL, 0x2e0a94ccf77ec0dbUL, 0xb0282d480e56489fUL, 0x18f4bfcbb4368929UL),
        Fp.FromMontgomery(0x23c5f0c953402dfdUL, 0x7a43ff6958ce4fe9UL, 0x2c390d3d2da5df63UL, 0xd0df5c98e1f9d70fUL, 0xffd89869a572b297UL, 0x1277ffc72f25e8feUL),
        Fp.FromMontgomery(0x79f4f0490f06a8a6UL, 0x85f894a88030fd81UL, 0x12da3054b18b6410UL, 0xe2a57f6505880d65UL, 0xbba074f260e400f1UL, 0x08b76279f621d028UL),
        Fp.FromMontgomery(0xe67245ba78d5b00bUL, 0x8456ba9a1f186475UL, 0x7888bff6e6b33bb4UL, 0xe21585b9a30f86cbUL, 0x05a69cdcef55feeeUL, 0x09e699dd9adfa5acUL),
        Fp.FromMontgomery(0x0de5c357bff57107UL, 0x0a0db4ae6b1a10b2UL, 0xe256bb67b3b3cd8dUL, 0x8ad456574e9db24fUL, 0x0443915f50fd4179UL, 0x098c4bf7de8b6375UL),
        Fp.FromMontgomery(0xe6b0617e7dd929c7UL, 0xfe6e37d442537375UL, 0x1dafdeda137a489eUL, 0xe4efd1ad3f767cebUL, 0x4a51d8667f0fe1cfUL, 0x054fdf4bbf1d821cUL),
        Fp.FromMontgomery(0x72db2a50658d767bUL, 0x8abf91faa257b3d5UL, 0xe969d6833764ab47UL, 0x464170142a1009ebUL, 0xb14f01aadb30be2fUL, 0x18ae6a856f40715dUL),
    ];
    internal static readonly Fp[] x_den =
    [
        Fp.FromMontgomery(0xb962a077fdb0f945UL, 0xa6a9740fefda13a0UL, 0xc14d568c3ed6c544UL, 0xb43fc37b908b133eUL, 0x9c0b3ac929599016UL, 0x0165aa6c93ad115fUL),
        Fp.FromMontgomery(0x23279a3ba506c1d9UL, 0x92cfca0a9465176aUL, 0x3b294ab13755f0ffUL, 0x116dda1c5070ae93UL, 0xed4530924cec2045UL, 0x083383d6ed81f1ceUL),
        Fp.FromMontgomery(0x9885c2a6449fecfcUL, 0x4a2b54ccd37733f0UL, 0x17da9ffd8738c142UL, 0xa0fba72732b3fafdUL, 0xff364f36e54b6812UL, 0x0f29c13c660523e2UL),
        Fp.FromMontgomery(0xe349cc118278f041UL, 0xd487228f2f3204fbUL, 0xc9d325849ade5150UL, 0x43a92bd69c15c2dfUL, 0x1c2c7844bc417be4UL, 0x12025184f407440cUL),
        Fp.FromMontgomery(0x587f65ae6acb057bUL, 0x1444ef325140201fUL, 0xfbf995e71270da49UL, 0xccda066072436a42UL, 0x7408904f0f186bb2UL, 0x13b93c63edf6c015UL),
        Fp.FromMontgomery(0xfb918622cd141920UL, 0x4a4c64423ecaddb4UL, 0x0beb232927f7fb26UL, 0x30f94df6f83a3dc2UL, 0xaeedd424d780f388UL, 0x06cc402dd594bbebUL),
        Fp.FromMontgomery(0xd41f761151b23f8fUL, 0x32a92465435719b3UL, 0x64f436e888c62cb9UL, 0xdf70a9a1f757c6e4UL, 0x6933a38d5b594c81UL, 0x0c6f7f7237b46606UL),
        Fp.FromMontgomery(0x693c08747876c8f7UL, 0x22c9850bf9cf80f0UL, 0x8e9071dab950c124UL, 0x89bc62d61c7baf23UL, 0xbc6be2d8dad57c23UL, 0x17916987aa14a122UL),
        Fp.FromMontgomery(0x1be3ff439c1316fdUL, 0x9965243a7571dfa7UL, 0xc7f7f62962f5cd81UL, 0x32c6aa9af394361cUL, 0xbbc2ee18e1c227f4UL, 0x0c102cbac531bb34UL),
        Fp.FromMontgomery(0x997614c97bacbf07UL, 0x61f86372b99192c0UL, 0x5b8c95fc14353fc3UL, 0xca2b066c2a87492fUL, 0x16178f5bbf698711UL, 0x12a6dcd7f0f4e0e8UL),
    ];
    internal static readonly Fp[] y_num =
    [
        Fp.FromMontgomery(0x2b567ff3e2837267UL, 0x1d4d9e57b958a767UL, 0xce028fea04bd7373UL, 0xcc31a30a0b6cd3dfUL, 0x7d7b18a682692693UL, 0x0d300744d42a0310UL),
        Fp.FromMontgomery(0x99c2555fa542493fUL, 0xfe7f53cc4874f878UL, 0x5df0608b8f97608aUL, 0x14e03832052b49c8UL, 0x706326a6957dd5a4UL, 0x0a8dadd9c2414555UL),
        Fp.FromMontgomery(0x13d942922a5cf63aUL, 0x357e33e36e261e7dUL, 0xcf05a27c8456088dUL, 0x0000bd1de7ba50f0UL, 0x83d0c7532f8c1fdeUL, 0x13f70bf38bbf2905UL),
        Fp.FromMontgomery(0x5c57fd95bfafbdbbUL, 0x28a359a65e541707UL, 0x3983ceb4f6360b6dUL, 0xafe19ff6f97e6d53UL, 0xb3468f4550192bf7UL, 0x0bb6cde49d8ba257UL),
        Fp.FromMontgomery(0x590b62c7ff8a513fUL, 0x314b4ce372cacefdUL, 0x6bef32ce94b8a800UL, 0x6ddf84a095713d5fUL, 0x64eace4cb0982191UL, 0x0386213c651b888dUL),
        Fp.FromMontgomery(0xa5310a31111bbcddUL, 0xa14ac0f5da148982UL, 0xf9ad9cc95423d2e9UL, 0xaa6ec095283ee4a7UL, 0xcf5b1f022e1c9107UL, 0x01fddf5aed881793UL),
        Fp.FromMontgomery(0x65a572b0d7a7d950UL, 0xe25c2d8183473a19UL, 0xc2fcebe7cb877dbdUL, 0x05b2d36c769a89b0UL, 0xba12961be86e9efbUL, 0x07eb1b29c1dfde1fUL),
        Fp.FromMontgomery(0x93e09572f7c4cd24UL, 0x364e929076795091UL, 0x8569467e68af51b5UL, 0xa47da89439f5340fUL, 0xf4fa918082e44d64UL, 0x0ad52ba3e6695a79UL),
        Fp.FromMontgomery(0x911429844e0d5f54UL, 0xd03f51a3516bb233UL, 0x3d587e5640536e66UL, 0xfa86d2a3a9a73482UL, 0xa90ed5adf1ed5537UL, 0x149c9c326a5e7393UL),
        Fp.FromMontgomery(0x462bbeb03c12921aUL, 0xdc9af5fa0a274a17UL, 0x9a558ebde836ebedUL, 0x649ef8f11a4fae46UL, 0x8100e1652b3cdc62UL, 0x1862bd62c291dacbUL),
        Fp.FromMontgomery(0x05c9b8ca89f12c26UL, 0x0194160fa9b9ac4fUL, 0x6a643d5a6879fa2cUL, 0x14665bdd8846e19dUL, 0xbb1d0d53af3ff6bfUL, 0x12c7e1c3b28962e5UL),
        Fp.FromMontgomery(0xb55ebf900b8a3e17UL, 0xfedc77ec1a9201c4UL, 0x1f07db10ea1a4df4UL, 0x0dfbd15dc41a594dUL, 0x389547f2334a5391UL, 0x02419f98165871a4UL),
        Fp.FromMontgomery(0xb416af000745fc20UL, 0x8e563e9d1ea6d0f5UL, 0x7c763e17763a0652UL, 0x01458ef0159ebbefUL, 0x8346fe421f96bb13UL, 0x0d2d7b829ce324d2UL),
        Fp.FromMontgomery(0x93096bb538d64615UL, 0x6f2a2619951d823aUL, 0x8f66b3ea59514fa4UL, 0xf563e63704f7092fUL, 0x724b136c4cf2d9faUL, 0x046959cfcfd0bf49UL),
        Fp.FromMontgomery(0xea748d4b6e405346UL, 0x91e9079c2c02d58fUL, 0x41064965946d9b59UL, 0xa06731f1d2bbe1eeUL, 0x07f897e267a33f1bUL, 0x1017290919210e5fUL),
        Fp.FromMontgomery(0x872aa6c17d985097UL, 0xeecc53161264562aUL, 0x07afe37afff55002UL, 0x54759078e5be6838UL, 0xc4b92d15db8acca8UL, 0x106d87d1b51d13b9UL),
    ];
    internal static readonly Fp[] y_den =
    [
        Fp.FromMontgomery(0xeb6c359d47e52b1cUL, 0x18ef5f8a10634d60UL, 0xddfa71a0889d5b7eUL, 0x723e71dcc5fc1323UL, 0x52f45700b70d5c69UL, 0x0a8b981ee47691f1UL),
        Fp.FromMontgomery(0x616a3c4f5535b9fbUL, 0x6f5f037395dbd911UL, 0xf25f4cc5e35c65daUL, 0x3e50dffea3c62658UL, 0x6a33dca523560776UL, 0x0fadeff77b6bfe3eUL),
        Fp.FromMontgomery(0x2be9b66df470059cUL, 0x24a2c159a3d36742UL, 0x115dbe7ad10c2a37UL, 0xb6634a652ee5884dUL, 0x04fe8bb2b8d81af4UL, 0x01c2a7a256fe9c41UL),
        Fp.FromMontgomery(0xf27bf8ef3b75a386UL, 0x898b367476c9073fUL, 0x24482e6b8c2f4e5fUL, 0xc8e0bbd6fe110806UL, 0x59b0c17f7631448aUL, 0x11037cd58b3dbfbdUL),
        Fp.FromMontgomery(0x31c7912ea267eec6UL, 0x1dbf6f1c5fcdb700UL, 0xd30d4fe3ba86fdb1UL, 0x3cae528fbee9a2a4UL, 0xb1cce69b6aa9ad9aUL, 0x044393bb632d94fbUL),
        Fp.FromMontgomery(0xc66ef6efeeb5c7e8UL, 0x9824c289dd72bb55UL, 0x71b1a4d2f119981dUL, 0x104fc1aafb0919ccUL, 0x0e49df01d942a628UL, 0x096c3a09773272d4UL),
        Fp.FromMontgomery(0x9abc11eb5fadeff4UL, 0x32dca50a885728f0UL, 0xfb1fa3721569734cUL, 0xc4b76271ea6506b3UL, 0xd466a75599ce728eUL, 0x0c81d4645f4cb6edUL),
        Fp.FromMontgomery(0x4199f10e5b8be45bUL, 0xda64e495b1e87930UL, 0xcb353efe9b33e4ffUL, 0x9e9efb24aa6424c6UL, 0xf08d33680a237465UL, 0x0d3378023e4c7406UL),
        Fp.FromMontgomery(0x7eb4ae92ec74d3a5UL, 0xc341b4aa9fac3497UL, 0x5be603899e907687UL, 0x03bfd9cca75cbdebUL, 0x564c2935a96bfa93UL, 0x0ef3c33371e2fdb5UL),
        Fp.FromMontgomery(0x7ee91fd449f6ac2eUL, 0xe5d5bd5cb9357a30UL, 0x773a8ca5196b1380UL, 0xd0fda172174ed023UL, 0x6cb95e0fa776aeadUL, 0x0d22d5a40cec7cffUL),
        Fp.FromMontgomery(0xf727e09285fd8519UL, 0xdc9d55a83017897bUL, 0x7549d8bd057894aeUL, 0x178419613d90d8f8UL, 0xfce95ebdeb5b490aUL, 0x0467ffaef23fc49eUL),
        Fp.FromMontgomery(0xc1769e6a7c385f1bUL, 0x79bc930deac01c03UL, 0x5461c75a23ede3b5UL, 0x6e20829e5c230c45UL, 0x828e0f1e772a53cdUL, 0x116aefa749127bffUL),
        Fp.FromMontgomery(0x101c10bf2744c10aUL, 0xbbf18d053a6a3154UL, 0xa0ecf39ef026f602UL, 0xfc009d4996dc5153UL, 0xb9000209d5bd08d3UL, 0x189e5fe4470cd73cUL),
        Fp.FromMontgomery(0x7ebd546ca1575ed2UL, 0xe47d5a981d081b55UL, 0x57b2b625b6d4ca21UL, 0xb0a1ba04228520ccUL, 0x98738983c2107ff3UL, 0x13dddbc4799d81d6UL),
        Fp.FromMontgomery(0x09319f2e39834935UL, 0x039e952cbdb05c21UL, 0x55ba77a9a2f76493UL, 0xfd04e3dfc6086467UL, 0xfb95832e7d78742eUL, 0x0ef9c24eccaf5e0eUL),
    ];
}
