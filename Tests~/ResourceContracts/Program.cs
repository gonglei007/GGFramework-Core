using System;
using GGFramework.Core.Tests;

internal static class Program
{
    private static int Main()
    {
        int passed = 0;
        int failed = 0;
        foreach (var test in ResourceManagementScenarios.Cases())
        {
            try
            {
                test.Value();
                passed++;
                Console.WriteLine("PASS " + test.Key);
            }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + test.Key + Environment.NewLine + error);
            }
        }
        Console.WriteLine("Resource contracts: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
}
