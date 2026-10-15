using System.Runtime.InteropServices;
using Nethermind.Crypto.BlsNet.Arithmetic;

string[] allowed = ["--compact-kernels", "--expanded-kernels", "--no-native", "--kernel-cold-start", "--square-cold-start", "--kernel-policy", "--constant-time-only", "--constant-time-audit", "--constant-time-audit-warm"];
if (args.Contains("--help")) { Console.WriteLine("Options: " + string.Join(" ", allowed)); return; }
if (args.Any(a => !allowed.Contains(a)) || args.Contains("--compact-kernels") && args.Contains("--expanded-kernels"))
    throw new ArgumentException("Options: " + string.Join(" ", allowed) + "; choose at most one kernel policy.");
// Policy must be set before the first field use, including cold-start diagnostics.
if (args.Contains("--compact-kernels")) AppContext.SetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", false);
if (args.Contains("--expanded-kernels")) AppContext.SetSwitch("Nethermind.Crypto.BlsNet.UseExpandedKernels", true);
bool native = NativeBlstLoader.Load(args.Contains("--no-native"));
Console.WriteLine(RuntimeInformation.FrameworkDescription);
string? diagnostic = args.FirstOrDefault(a => a is "--kernel-cold-start" or "--square-cold-start" or "--kernel-policy");
if (diagnostic is not null)
{
    Console.WriteLine($"PASS: {TowerTests.Run(native, diagnostic):N0} diagnostic checks.");
    return;
}
Console.WriteLine($"Field kernels: {(Fp.UseExpandedKernels ? "expanded" : "compact")}");
if (args.Contains("--constant-time-audit") || args.Contains("--constant-time-audit-warm")) { ConstantTimeAudit.Run(args.Contains("--constant-time-audit-warm")); return; }
int secret = InverseTests.Run(native) + ConstantTimeTests.Run(native);
if (args.Contains("--constant-time-only") || args.Contains("--constant-time-audit")) return;
Console.WriteLine("Arithmetic");
int arithmetic = ArithmeticTests.Run();
Console.WriteLine($"Arithmetic: {arithmetic:N0} checks.");
int tower = TowerTests.Run(native, null);
int curves = CurveTests.Run(native);
Console.WriteLine($"PASS: {secret + arithmetic + tower + curves:N0} total checks.");
