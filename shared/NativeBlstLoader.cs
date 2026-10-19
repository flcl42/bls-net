using System.Runtime.InteropServices;

internal static class NativeBlstLoader
{
    internal static void PinBenchmarkProcessor()
    {
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("BLST_BENCH_PROCESSOR") is string processor)
            System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity = (nint)(1L << int.Parse(processor));
    }

    internal static bool Load(bool disabled = false, bool required = false)
    {
        if (disabled && required) throw new ArgumentException("Choose either --no-native or --require-native.");
        if (disabled) { Console.WriteLine("SKIP: native blst disabled."); return false; }
        if (!TryLoad(out nint handle))
        {
            if (required) throw new DllNotFoundException("Native blst is required for this run.");
            return false;
        }
        NativeLibrary.SetDllImportResolver(typeof(NativeBlstLoader).Assembly, (name, _, _) => name is "blst" or "blst.dll" ? handle : 0);
        return true;
    }

    private static bool TryLoad(out nint handle)
    {
        string fileName = OperatingSystem.IsWindows() ? "blst.dll" : OperatingSystem.IsMacOS() ? "libblst.dylib" : "libblst.so";
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "artifacts", "native", fileName);
            if (File.Exists(path))
            {
                handle = LoadPath(path);
                return true;
            }
            path = Path.Combine(directory.FullName, fileName);
            if (File.Exists(path))
            {
                handle = LoadPath(path);
                return true;
            }
        }
        handle = 0;
        string script = OperatingSystem.IsWindows() ? "scripts/build-native-blst.ps1" : "sh scripts/build-native-blst.sh";
        Console.WriteLine($"SKIP: native blst comparisons ({RuntimeInformation.ProcessArchitecture}); build artifacts/native/{fileName} with {script}. Managed checks and benchmarks still run.");
        return false;
    }

    private static nint LoadPath(string path)
    {
        try
        {
            nint handle = NativeLibrary.Load(path);
            Console.WriteLine($"Native blst: {path} ({RuntimeInformation.ProcessArchitecture})");
            return handle;
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException)
        {
            throw new InvalidOperationException($"Cannot load native blst at '{path}' for {RuntimeInformation.OSDescription}, process {RuntimeInformation.ProcessArchitecture}. Rebuild the native reference for this OS and process architecture.", exception);
        }
    }
}
