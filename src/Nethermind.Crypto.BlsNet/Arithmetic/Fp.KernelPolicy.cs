using System.Runtime.InteropServices;

namespace Nethermind.Crypto.BlsNet.Arithmetic;

public readonly partial struct Fp
{
    // Once initialized, optimized JIT code folds this field and removes the
    // unused kernel. Select by the executing runtime, independently of the TFM.
    internal static readonly bool UseExpandedKernels = SelectExpandedKernels();

    private static bool SelectExpandedKernels()
    {
        if (AppContext.TryGetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", out bool enabled))
            return enabled;
        string? setting = Environment.GetEnvironmentVariable("BLSNET_EXPANDED_KERNELS");
        if (setting == "1") return true;
        if (setting == "0") return false;
        if (bool.TryParse(setting, out enabled)) return enabled;
        // Stock .NET 10 x64 runs the expanded formulas slower; on ARM64 they
        // measured faster on every runtime (docs/performance.md#arm64-hardware).
        return Environment.Version.Major >= 11 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
