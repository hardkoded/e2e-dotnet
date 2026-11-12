// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Xml.Linq;
using E2E.Engine;

namespace E2E.TUnit.Tests.EngineContract;

/// <summary>What one test of a nested run did: TUnit's outcome (<c>Passed</c> or <c>Failed</c>, from the TRX report) and the failure message with its stack trace.</summary>
internal sealed record NestedResult(string Result, string Message);

/// <summary>
/// Runs the explicit tests of a fixture class in a test process of their own (TUnit has no in-process runner to nest) and returns their results
/// by method name, with what the process printed and what the tests wrote.
/// </summary>
internal static class NestedSessionRun
{
    /// <summary>The file a nested process writes to when it exits: a test's console is not the process's.</summary>
    internal const string ExitFileVariable = "E2E_NESTED_EXIT_FILE";

    public static async Task<(IReadOnlyDictionary<string, NestedResult> Results, string Output)> RunAsync(Type fixture)
    {
        var directory = Directory.CreateTempSubdirectory("e2e-tunit-nested-").FullName;
        var exitFile = Path.Combine(directory, "exit.txt");
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment[ExitFileVariable] = exitFile;
        string[] arguments =
        [
            "--treenode-filter", $"/*/{fixture.Namespace}/{fixture.Name}/*",
            "--results-directory", directory,
            "--report-trx", "--report-trx-filename", "nested.trx",
        ];
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var text = await output + await error;
            var trx = Directory.EnumerateFiles(directory, "nested.trx", SearchOption.AllDirectories).FirstOrDefault();
            if (trx is null)
            {
                throw new InvalidOperationException($"The nested run of {fixture.Name} wrote no result (exit code {process.ExitCode}): {text}");
            }

            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var document = XDocument.Load(trx);
            var methods = document.Descendants(ns + "UnitTest").ToDictionary(
                test => (string)test.Attribute("id")!,
                test => (string)test.Element(ns + "TestMethod")!.Attribute("name")!,
                StringComparer.Ordinal);
            var results = new Dictionary<string, NestedResult>(StringComparer.Ordinal);
            // What the tests wrote is in the report (a failed test's output is also on the console, so the console is not added), and what the process wrote when it exited is in the file it was given.
            var stdout = new System.Text.StringBuilder(File.Exists(exitFile) ? File.ReadAllText(exitFile) : string.Empty);
            foreach (var result in document.Descendants(ns + "UnitTestResult"))
            {
                var outcome = (string)result.Attribute("outcome")!;
                // The stack trace names the exception types a hook failure wraps.
                var message = ((string?)result.Descendants(ns + "Message").FirstOrDefault() ?? string.Empty) + Environment.NewLine + (string?)result.Descendants(ns + "StackTrace").FirstOrDefault();
                results[methods[(string)result.Attribute("testId")!]] = new NestedResult(outcome == "NotExecuted" ? "Skipped" : outcome, message);
                stdout.Append((string?)result.Descendants(ns + "StdOut").FirstOrDefault());
            }

            return (results, stdout.ToString());
        }
        finally
        {
            // The child holds files in the directory: it is gone before the directory is, and a cleanup that throws must not hide the failure above.
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(10));
                }

                Directory.Delete(directory, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Console.Error.WriteLine("e2e: could not remove " + directory + ": " + ex.Message);
            }
        }
    }
}

/// <summary>The shared shape of the fixtures: the web engine behind <see cref="StateSpyEngine"/>, on the testbed, with no cache. A fixture runs its tests one at a time, so a setup test never overlaps the test that restores its session.</summary>
[NotInParallel]
public abstract class SessionFixtureBase : E2E.TUnit.E2ETest
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

    [Test, Explicit, E2ESetup("capture session", Saves = ["acct"])]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Test, Explicit]
    [E2ESession("acct")]
    public Task Consumes_session() => Task.CompletedTask;
}

/// <summary>The setup and the consumer of upstream's round trip: the consumer checks what the engine saw and what the page holds.</summary>
public sealed class RoundTripFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit, E2ESetup("capture session", Saves = ["acct"])]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Test, Explicit]
    [E2ESession("acct")]
    public async Task Consumes_session()
    {
        await Assert.That(await SignedInAsync()).IsEqualTo("admin");
        await Assert.That(Spy.Captured).Count().IsEqualTo(1);
        await Assert.That(Spy.Restored).Count().IsEqualTo(1);
        await Assert.That(Spy.Restored[0].Format).IsEqualTo("playwright-storage-state");
    }
}

/// <summary>The setup and the consumer of upstream's failing restore. The process prints how many engine sessions started and ended.</summary>
public sealed class FailingRestoreFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = Create();

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit, E2ESetup("capture session", Saves = ["acct"])]
    public async Task CaptureSession()
    {
        await SignInAsync("admin");
        await SaveSessionAsync("acct");
    }

    [Test, Explicit]
    [E2ESession("acct")]
    public Task Consumes_session() => Task.CompletedTask;

    private static StateSpyEngine Create()
    {
        var engine = new StateSpyEngine(new WebEngine(new WebEngineOptions()), failRestore: true);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Environment.GetEnvironmentVariable(NestedSessionRun.ExitFileVariable) is { Length: > 0 } file)
            {
                File.AppendAllText(file, $"engine-sessions started={engine.Started} ended={engine.Ended}{Environment.NewLine}");
            }
        };
        return engine;
    }
}

/// <summary>A setup that breaks the contract in each way upstream's runner checks.</summary>
public sealed class SessionContractBreaksFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit, E2ESetup("saves nothing", Saves = ["acct"])]
    public Task SavesNothing() => Task.CompletedTask;

    [Test, Explicit, E2ESetup("saves twice", Saves = ["twice"])]
    public async Task SavesTwice()
    {
        await SaveSessionAsync("twice");
        await SaveSessionAsync("twice");
    }

    [Test, Explicit, E2ESetup("saves an undeclared name", Saves = ["declared"])]
    public async Task SavesUndeclared()
    {
        await SaveSessionAsync("other");
    }

    [Test, Explicit]
    public async Task Saves_outside_a_setup_test()
    {
        await SaveSessionAsync("acct");
    }

    [Test, Explicit]
    [E2ESession("never-saved")]
    public Task Restores_a_session_nobody_saved() => Task.CompletedTask;
}
