using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;

namespace Nethermind.Crypto.BlsNet.Hashing;

/// <summary>Single-input maps matching blst. See individual methods for timing contracts.</summary>
public static partial class MapToCurve
{
    /// <summary>Maps an Fp element to G1; variable-time.</summary>
    public static void MapToG1(in Fp u, out G1Projective r) => Hashing.MapToG1.Map(u, out r);
    /// <summary>Maps an Fp2 element to G2; variable-time.</summary>
    public static void MapToG2(in Fp2 u, out G2Projective r) => Hashing.MapToG2.Map(u, out r);
}
