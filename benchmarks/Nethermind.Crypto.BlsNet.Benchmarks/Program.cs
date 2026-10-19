using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

if (args.Contains("--help"))
{
    Console.WriteLine("Options: --filter <substring[|substring]> --quick --no-native/--require-native --compact-kernels/--expanded-kernels");
    Console.WriteLine("Timing experiment: --timing-leak --timing-samples <samples-per-class> (default 20000)");
    return;
}
BenchmarkOptions.Parse(args);
NativeBlstLoader.PinBenchmarkProcessor();
bool native = NativeBlstLoader.Load(BenchmarkOptions.NoNative, BenchmarkOptions.RequireNative);
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; process {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Field kernels: {(Fp.UseExpandedKernels ? "expanded" : "compact")}; {(BenchmarkOptions.Quick ? "short" : "full")} rounds.");
if (BenchmarkOptions.TimingLeak) { TimingLeakTests.Run(BenchmarkOptions.TimingSamples); return; }
SecretBenchmarks.Run(native);
MulAddBenchmarks.Run(native);
TowerBenchmarks.RunKernels(native);
TowerBenchmarks.Run(native);
CurveBenchmarks.Run(native);

internal static class BenchmarkOptions
{
    internal static bool TimingLeak { get; private set; }
    internal static int TimingSamples { get; private set; } = 20000;
    internal static bool Quick { get; private set; }
    internal static bool NoNative { get; private set; }
    internal static bool RequireNative { get; private set; }
    private static string[]? _filters;

    internal static bool Matches(string name) => _filters is null ||
        Array.Exists(_filters, filter => name.Contains(filter, StringComparison.OrdinalIgnoreCase));

    internal static void Parse(string[] args)
    {
        bool? expanded = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--timing-leak": TimingLeak = true; break;
                case "--timing-samples" when i + 1 < args.Length: TimingSamples = int.Parse(args[++i]); break;
                case "--quick": Quick = true; break;
                case "--no-native": NoNative = true; break;
                case "--require-native": RequireNative = true; break;
                case "--filter" when i + 1 < args.Length: _filters = args[++i].Split('|'); break;
                case "--compact-kernels" when expanded is null: expanded = false; break;
                case "--expanded-kernels" when expanded is null: expanded = true; break;
                default: throw new ArgumentException("Options: --filter <substring[|substring]> --quick --no-native/--require-native --compact-kernels/--expanded-kernels");
            }
        }
        if (expanded is bool policy) AppContext.SetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", policy);
    }
}
