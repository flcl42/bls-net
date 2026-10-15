using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

public static partial class MapToCurve
{
    /// <summary>Maps a secret field element to G1, matching blst_map_to_g1(u, NULL). All intermediates are secret.</summary>
    public static void MapToG1ConstantTime(in Fp u, out G1Projective r) => Hashing.MapToG1.MapConstantTime(u, out r);
    /// <summary>Maps a secret Fp2 element to G2, matching blst_map_to_g2(u, NULL). All intermediates are secret.</summary>
    public static void MapToG2ConstantTime(in Fp2 u, out G2Projective r) => Hashing.MapToG2.MapConstantTime(u, out r);
}
