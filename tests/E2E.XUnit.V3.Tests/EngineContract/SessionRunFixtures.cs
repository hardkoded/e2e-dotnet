// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Xml.Linq;
using E2E.Engine;

namespace E2E.XUnit.V3.Tests.EngineContract;

/// <summary>What one test of a nested run did: xUnit's result and the first failure message.</summary>
internal sealed record NestedResult(string Result, string Message);

/// <summary>
/// Runs the explicit tests of a fixture class in a test process of their own (xUnit has no in-process runner to nest) and returns their results
/// by method name, with what the process printed.
/// </summary>
internal static class NestedSessionRun
{
    public static async Task<(IReadOnlyDictionary<string, NestedResult> Results, string Output)> RunAsync(Type fixture)
    {
        var xml = Path.Combine(Path.GetTempPath(), $"e2e-927-{Guid.NewGuid():N}.xml");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(typeof(NestedSessionRun).Assembly.Location);
        start.ArgumentList.Add("-class");
        start.ArgumentList.Add(fixture.FullName!);
        start.ArgumentList.Add("-explicit");
        start.ArgumentList.Add("on");
        start.ArgumentList.Add("-xml");
        start.ArgumentList.Add(xml);

        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var text = await output + await error;
            if (!File.Exists(xml))
            {
                throw new InvalidOperationException($"The nested run of {fixture.Name} wrote no result (exit code {process.ExitCode}): {text}");
            }

            var results = XDocument.Load(xml).Descendants("test").ToDictionary(
                test => (string)test.Attribute("method")!,
                test => new NestedResult((string)test.Attribute("result")!, ((string?)test.Element("failure")?.Element("message") ?? string.Empty).Replace("\\\"", "\"", StringComparison.Ordinal)),
                StringComparer.Ordinal);
            return (results, text);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            File.Delete(xml);
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

    internal abstract StateSpyEngine Spy { get; }

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

/// <summary>The setup and the consumer of upstream's engine-contract tests, on an engine with no state capability.</summary>
public sealed class NoStateCapabilityFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()), state: false);

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("capture session", Saves = ["acct"], Explicit = true)]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Fact(Explicit = true)]
    [E2ESession("acct")]
    public Task Consumes_session() => Task.CompletedTask;
}

/// <summary>The setup and the consumer of upstream's round trip: the consumer checks what the engine saw and what the page holds.</summary>
public sealed class RoundTripFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("capture session", Saves = ["acct"], Explicit = true)]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Fact(Explicit = true)]
    [E2ESession("acct")]
    public async Task Consumes_session()
    {
        Assert.Equal("admin", await SignedInAsync());
        Assert.Single(Spy.Captured);
        Assert.Single(Spy.Restored);
        Assert.Equal("playwright-storage-state", Spy.Restored[0].Format);
    }
}

/// <summary>The setup and the consumer of upstream's failing restore. The process prints how many engine sessions started and ended.</summary>
public sealed class FailingRestoreFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = Create();

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("capture session", Saves = ["acct"], Explicit = true)]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Fact(Explicit = true)]
    [E2ESession("acct")]
    public Task Consumes_session() => Task.CompletedTask;

    private static StateSpyEngine Create()
    {
        var engine = new StateSpyEngine(new WebEngine(new WebEngineOptions()), failRestore: true);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Console.Out.WriteLine($"engine-sessions started={engine.Started} ended={engine.Ended}");
        return engine;
    }
}

/// <summary>A setup that breaks the contract in each way upstream's runner checks.</summary>
public sealed class SessionContractBreaksFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("saves nothing", Saves = ["acct"], Explicit = true)]
    public Task SavesNothing() => Task.CompletedTask;

    [E2ESetup("saves twice", Saves = ["twice"], Explicit = true)]
    public async Task SavesTwice()
    {
        await SaveSessionAsync("twice");
        await SaveSessionAsync("twice");
    }

    [E2ESetup("saves an undeclared name", Saves = ["declared"], Explicit = true)]
    public async Task SavesUndeclared()
    {
        await SaveSessionAsync("other");
    }

    [Fact(Explicit = true)]
    public async Task Saves_outside_a_setup_test()
    {
        await SaveSessionAsync("acct");
    }

    [Fact(Explicit = true)]
    [E2ESession("never-saved")]
    public Task Restores_a_session_nobody_saved() => Task.CompletedTask;
}
