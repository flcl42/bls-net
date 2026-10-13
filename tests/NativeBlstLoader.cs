using System.Runtime.InteropServices;

internal static class NativeBlstLoader
{
    internal static bool TryLoad(out nint handle)
    {
        string fileName = OperatingSystem.IsWindows() ? "blst.dll" : OperatingSystem.IsMacOS() ? "libblst.dylib" : "libblst.so";
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "artifacts", "native", fileName);
            if (File.Exists(path))
            {
                handle = NativeLibrary.Load(path);
                return true;
            }
            path = Path.Combine(directory.FullName, fileName);
            if (File.Exists(path))
            {
                handle = NativeLibrary.Load(path);
                return true;
            }
        }
        handle = 0;
        Console.WriteLine("SKIP: native blst comparisons; build artifacts/native/blst.dll with scripts/build-native-blst.ps1. Managed checks and benchmarks still run.");
        return false;
    }
}
