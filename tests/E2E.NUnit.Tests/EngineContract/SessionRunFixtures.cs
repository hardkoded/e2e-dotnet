// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit.Tests.EngineContract;

/// <summary>Runs the named tests of an explicit fixture in a runner of their own and returns their results by test name.</summary>
internal static class NestedSessionRun
{
    public static IReadOnlyDictionary<string, ITestResult> Run(params string[] testNames)
    {
        return Task.Run(() =>
        {
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            runner.Load(typeof(NestedSessionRun).Assembly, new Dictionary<string, object>());
            var names = string.Concat(testNames.Select(name => $"<name>{name}</name>"));
            var result = runner.Run(TestListener.NULL, TestFilter.FromXml($"<filter><or>{names}</or></filter>"));
            var leaves = new Dictionary<string, ITestResult>(StringComparer.Ordinal);
            Collect(result, leaves);
            return (IReadOnlyDictionary<string, ITestResult>)leaves;
        }).GetAwaiter().GetResult();
    }

    private static void Collect(ITestResult result, Dictionary<string, ITestResult> leaves)
    {
        if (!result.HasChildren)
        {
            leaves[result.Test.Name] = result;
            return;
        }

        foreach (var child in result.Children)
        {
            Collect(child, leaves);
        }
    }
}

/// <summary>The shared shape of the fixtures: the web engine behind <see cref="StateSpyEngine"/>, on the testbed, with no cache.</summary>
public abstract class SessionFixtureBase : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    internal static StateSpyEngine Spy { get; set; } = null!;

    protected override IEngine CreateEngine() => Spy;

    protected async Task SignInAsync(string value)
    {
        await App.OpenAsync("/browser");
        await Browser.SetCookiesAsync([new BrowserCookie { Name = "account", Value = value, Url = await Browser.UrlAsync() }]);
    }

    protected async Task<string?> SignedInAsync()
    {
        await App.OpenAsync("/browser");
        return (await Browser.CookiesAsync()).FirstOrDefault(cookie => cookie.Name == "account")?.Value;
    }
}

/// <summary>The setup and the consumer of upstream's engine-contract tests of saved sessions.</summary>
[Explicit("Run by RunnerEngineContractTests")]
public sealed class SessionContractFixture : SessionFixtureBase
{
    public static string? Seen;

    [E2ESetup("capture session", Saves = ["acct"])]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Test]
    [E2ESession("acct")]
    public async Task Consumes_session()
    {
        Seen = await SignedInAsync();
    }
}

/// <summary>A setup that breaks the contract in each way upstream's runner checks.</summary>
[Explicit("Run by RunnerEngineContractTests")]
public sealed class SessionContractBreaksFixture : SessionFixtureBase
{
    [E2ESetup("saves nothing", Saves = ["acct"])]
    public Task SavesNothing() => Task.CompletedTask;

    [E2ESetup("saves twice", Saves = ["twice"])]
    public async Task SavesTwice()
    {
        await SaveSessionAsync("twice");
        await SaveSessionAsync("twice");
    }

    [E2ESetup("saves an undeclared name", Saves = ["declared"])]
    public async Task SavesUndeclared()
    {
        await SaveSessionAsync("other");
    }

    [Test]
    public async Task Saves_outside_a_setup_test()
    {
        await SaveSessionAsync("acct");
    }

    [Test]
    [E2ESession("never-saved")]
    public Task Restores_a_session_nobody_saved() => Task.CompletedTask;
}
