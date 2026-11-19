using System;
using System.Collections.Generic;
using NUnit.Framework;
using GGFramework.Core.Tests;

public class ResourceManagementTests
{
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var pair in ResourceManagementScenarios.Cases())
            yield return new TestCaseData(pair.Value).SetName(pair.Key);
    }

    [TestCaseSource(nameof(Cases))]
    public void ResourceContract(Action run) { run(); }
}
