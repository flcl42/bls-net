// Standalone harness for the MIT-licensed runtime opt/Add regression sources.
namespace Xunit
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class FactAttribute : System.Attribute { }
}

public static class RunnerMain
{
    public static int Main()
    {
        int failed = 0, passed = 0;
        foreach (var type in typeof(RunnerMain).Assembly.GetTypes())
        foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
        {
            if (!System.Attribute.IsDefined(method, typeof(Xunit.FactAttribute))) continue;
            try
            {
                object instance = method.IsStatic ? null : System.Activator.CreateInstance(type, nonPublic: true);
                var result = method.Invoke(instance, null);
                if (result is int code && code != 100 && code != 0)
                    throw new System.Exception("returned " + code);
                if (result is not null && result is not int)
                    throw new System.Exception("Unsupported test return type: " + result.GetType());
                passed++;
                System.Console.WriteLine($"PASS {type.Name}.{method.Name}");
            }
            catch (System.Exception error)
            {
                failed++;
                System.Console.WriteLine($"FAIL {type.Name}.{method.Name}: {error.InnerException ?? error}");
            }
        }
        System.Threading.Thread.Sleep(1000); // Allow background Tier1 compilation to finish.
        System.Console.WriteLine($"passed {passed}, failed {failed}");
        return passed > 0 && failed == 0 ? 100 : 1;
    }
}
