// ClaudeCodePlus — the dependency rules between modules, pinned. MSBuild already rejects
// project-reference cycles; this test pins DIRECTION.
// There is no Core.Contracts project: a module's contract is its public surface, so the allowed
// edges are an explicit table per module.
// Keep both tables in sync with this project's references.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Tests.Architecture;

public class DependencyRulesTests
{
    // Module name = assembly name = root namespace (src/Directory.Build.props).
    private static readonly string[] Modules =
    {
        "Feature.Catalog",
        "Feature.ChipButton",
        "Feature.ContextBar",
        "Feature.DeepLink",
        "Feature.Picker",
        "Core.Infrastructure",
    };

    // The declared depends-on edges (module docs). App.Host is the exe: nothing may depend on it,
    // and it is not loaded here.
    private static readonly Dictionary<string, string[]> AllowedDependencies = new()
    {
        ["Feature.Catalog"] = Array.Empty<string>(),
        ["Feature.ChipButton"] = Array.Empty<string>(),
        ["Feature.ContextBar"] = Array.Empty<string>(),
        ["Feature.DeepLink"] = Array.Empty<string>(),
        ["Feature.Picker"] = new[] { "Feature.Catalog", "Feature.DeepLink" },
        ["Core.Infrastructure"] = new[] { "Feature.Catalog", "Feature.ChipButton", "Feature.ContextBar", "Feature.DeepLink", "Feature.Picker" },
    };

    [Fact]
    public void Every_module_depends_only_on_its_declared_modules()
    {
        foreach (string name in Modules)
        {
            string[] forbidden = Modules
                .Where(m => m != name && !AllowedDependencies[name].Contains(m))
                .Append("App.Host")
                .ToArray();
            TestResult result = Types.InAssembly(Assembly.Load(name))
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();
            Assert.True(
                result.IsSuccessful,
                $"{name} -> {string.Join(", ", result.FailingTypeNames ?? new List<string>())}");
        }
    }

    [Fact]
    public void Every_module_exports_types_in_its_own_namespace_only()
    {
        foreach (string name in Modules)
        {
            Assembly assembly = Assembly.Load(name);
            Assert.All(
                assembly.GetExportedTypes(),
                type => Assert.StartsWith(name, type.Namespace ?? string.Empty, StringComparison.Ordinal));
        }
    }
}
