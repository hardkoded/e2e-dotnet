// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli;

/// <summary>
/// .NET-only: a test framework <c>e2e init</c> can scaffold a project for. Upstream has one runner, so it has no such choice.
/// </summary>
/// <param name="Id">The <c>--framework</c> value.</param>
/// <param name="Label">The framework's name.</param>
/// <param name="AdapterSuffix">What follows <c>E2E.</c> in the package of an engine for this framework: <c>NUnit</c>, <c>XUnit.V3</c>, or <c>TUnit</c>.</param>
/// <param name="Dependencies">The framework's own packages and their versions, as the samples pin them.</param>
/// <param name="Marker">A package name that shows a project already uses the framework.</param>
/// <param name="Namespace">The namespace of the engine's base class.</param>
/// <param name="FrameworkUsing">The namespace of the framework's test attributes.</param>
/// <param name="TestAttribute">The attribute of a test method.</param>
/// <param name="TrueAssertion">A format string for an assertion that its argument is true.</param>
/// <param name="Executable">Whether the test project is an executable (xUnit v3 and TUnit are).</param>
/// <param name="TestCommand">The command that runs the tests: <c>dotnet test</c>, except for TUnit. TUnit runs on Microsoft.Testing.Platform, and on .NET SDK 10 and newer <c>dotnet test</c> refuses such a project unless a <c>global.json</c> opts in, so <c>dotnet run</c> is the command that always works.</param>
internal sealed record FrameworkPreset(
    string Id,
    string Label,
    string AdapterSuffix,
    IReadOnlyList<KeyValuePair<string, string>> Dependencies,
    string Marker,
    string Namespace,
    string FrameworkUsing,
    string TestAttribute,
    string TrueAssertion,
    bool Executable,
    string TestCommand = "dotnet test");

/// <summary>.NET-only: the test frameworks of the scaffold. NUnit is the default, as the get-started pages lead with it.</summary>
internal static class Frameworks
{
    /// <summary>The framework a new project gets when no flag and no existing project names one.</summary>
    public const string DefaultFrameworkId = "nunit";

    /// <summary>Every framework, in the order <c>--framework</c> lists them.</summary>
    public static readonly IReadOnlyList<FrameworkPreset> All =
    [
        new FrameworkPreset(
            "nunit",
            "NUnit",
            "NUnit",
            [
                new("Microsoft.NET.Test.Sdk", "17.14.1"),
                new("NUnit", "5.0.0"),
                new("NUnit3TestAdapter", "6.3.0"),
            ],
            "NUnit",
            "E2E.NUnit",
            "NUnit.Framework",
            "Test",
            "Assert.That({0}, Is.True)",
            false),
        new FrameworkPreset(
            "xunit",
            "xUnit v3",
            "XUnit.V3",
            [
                new("Microsoft.NET.Test.Sdk", "17.14.1"),
                new("xunit.v3.mtp-off", "4.0.1"),
                new("xunit.runner.visualstudio", "4.0.0"),
            ],
            "xunit",
            "E2E.XUnit.V3",
            "Xunit",
            "Fact",
            "Assert.True({0})",
            true),
        new FrameworkPreset(
            "tunit",
            "TUnit",
            "TUnit",
            [
                new("TUnit", "1.83.8"),
            ],
            "TUnit",
            "E2E.TUnit",
            "TUnit.Core",
            "Test",
            "await Assert.That({0}).IsTrue()",
            true,
            "dotnet run"),
    ];

    /// <summary>Resolves a framework by id.</summary>
    public static FrameworkPreset Get(string id) =>
        All.FirstOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("unknown framework: " + id, nameof(id));

    /// <summary>The framework a project already uses, judged by the packages it names, or null when it names neither.</summary>
    public static FrameworkPreset? Detect(IEnumerable<string> packages)
    {
        var names = packages.ToList();
        return All.FirstOrDefault(entry => names.Any(name => name.StartsWith(entry.Marker, StringComparison.OrdinalIgnoreCase) || (name.StartsWith("E2E.", StringComparison.OrdinalIgnoreCase) && name.Contains(entry.Marker, StringComparison.OrdinalIgnoreCase))));
    }
}
