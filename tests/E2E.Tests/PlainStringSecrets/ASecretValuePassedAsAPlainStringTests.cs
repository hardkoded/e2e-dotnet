// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using E2E.Engine;
using E2E.Reports;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.PlainStringSecrets;

/// <summary>
/// The first <c>describe</c> of upstream's <c>plain-string-secrets.test.ts</c>. Upstream runs one suite through the runner and reads
/// its outcome, events, and <c>.e2e</c> files; the port runs one session per test with the same recording executor and reads its
/// report and the files under its results directory. The port has no runner, so no run ids, no reporter files (<c>junit.xml</c>,
/// <c>summary.md</c>), and no <c>output</c> events: the tests that need them are <c>Partial_</c>. Upstream registers the secret in the
/// config, so it is known before any step. The port registers a secret when an <c>act</c> receives it in its params, so the act
/// that types it comes first here.
/// </summary>
public sealed class ASecretValuePassedAsAPlainStringTests
{
    private const string Value = "plain-Zr8Kq2Wm5Tx9";

    private const string Marker = "<secret:probe>";

    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Partial: the port has no runner, so no run id of the shape <c>file::suite::title</c>, and its session names one title where
    /// upstream names a path of two (the joined path). The error and the step label are checked as upstream's are. The test does not
    /// spell the secret in a URL, as upstream's does. Missing function: the runner and its collection (issue #1148 names the gap).
    /// </summary>
    [Fact]
    public async Task Partial_Runs_the_test_under_its_redacted_title_and_id_its_error_still_masked()
    {
        var run = await RunAsync(FailingScenario);

        var result = run.Report.Run.Results[0];
        Assert.Equal(E2E.Reports.ResultStatus.Failed, result.Status);
        Assert.Equal([$"suite {Marker} > title holds {Marker}"], result.TitlePath);
        Assert.Contains(Marker, result.TestId, StringComparison.Ordinal);
        Assert.DoesNotContain(Value, result.TestId, StringComparison.Ordinal);
        var attempt = result.Attempts[^1];
        Assert.Equal("ASSERTION_FAILED", attempt.Error?.Code);
        Assert.Contains($"missing {Marker}", attempt.Error?.Message, StringComparison.Ordinal);
        Assert.Contains(attempt.Steps, step => step.Label == $"inspect {Marker} with {{note}}");
    }

    /// <summary>
    /// Partial: the secret handle is declared by name alone, since the port's secret has no default purpose, and the act that
    /// types it runs before the act that spells the value as plain strings, so the secret is known to the redaction by then.
    /// </summary>
    [Fact]
    public async Task Partial_Hands_the_executor_the_instruction_params_agent_context_and_title_path_redacted()
    {
        var inputs = (await RunAsync(FailingScenario)).Inputs;

        Assert.Equal(2, inputs.Count);
        Assert.Equal("""{"key":{"kind":"secret","name":"probe"}}""", inputs[0].Params);
        Assert.Equal($"inspect {Marker} with {{note}}", inputs[1].Instruction);
        Assert.Equal($$"""{"note":"{{Marker}}","key {{Marker}}":["{{Marker}}"]}""", inputs[1].Params);
        // The title spells the secret too, as upstream's does: both steps read it redacted, apart from the context.
        Assert.All(inputs, input => Assert.Equal("context " + Marker, input.AgentContext));
        Assert.All(inputs, input => Assert.Equal(["suite " + Marker + " > title holds " + Marker], input.TitlePath));
        Assert.DoesNotContain(Value, JsonSerializer.Serialize(inputs), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Still_types_the_real_value_through_the_secret_handle()
    {
        var run = await RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("paste the key into the focus target", new ActOptions { Params = new Dictionary<string, object?> { ["key"] = Secret.Create("probe", Value) } });
            await Expect.That(session.Screen.GetByLabel("Focus target")).ToHaveValueAsync(Value);
        });

        Assert.Equal(E2E.Reports.ResultStatus.Passed, run.Report.Run.Results[0].Status);
    }

    /// <summary>
    /// Partial: the port has no output events, no <c>junit.xml</c> or <c>summary.md</c>, and does not capture what the test prints, so
    /// it reads the report, every file the session wrote under its results directory, and the names of those files and directories,
    /// for the value in each of its forms. Missing function: the runner's output events and reporters (issue #1148 names the gap).
    /// </summary>
    [Fact]
    public async Task Partial_Keeps_the_value_out_of_every_output_event_report_reporter_file_and_file_name_under_e2e()
    {
        var run = await RunAsync(FailingScenario);

        Assert.DoesNotContain(Value, JsonSerializer.Serialize(run.Report, Relaxed), StringComparison.Ordinal);
        var names = Directory.EnumerateFileSystemEntries(run.ResultsDirectory, "*", SearchOption.AllDirectories)
            .Select(entry => Path.GetRelativePath(run.ResultsDirectory, entry).Replace('\\', '/'))
            .ToList();
        Assert.Contains(names, name => name.EndsWith("trace.md", StringComparison.Ordinal));
        foreach (var name in names)
        {
            Assert.False(name.Contains(Value, StringComparison.Ordinal), name);
        }

        var standard = Convert.ToBase64String(Encoding.UTF8.GetBytes(Value));
        var forms = new[] { Value, standard, standard.TrimEnd('=').Replace('+', '-').Replace('/', '_') }.Select(form => form.ToLowerInvariant()).ToList();
        foreach (var file in Directory.EnumerateFiles(run.ResultsDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(file).ToLowerInvariant();
            foreach (var form in forms)
            {
                Assert.False(text.Contains(form, StringComparison.Ordinal), file);
            }
        }
    }

    private static async Task FailingScenario(E2ESession session)
    {
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("paste the key into the focus target", new ActOptions { Params = new Dictionary<string, object?> { ["key"] = Secret.Create("probe", Value) } });
        await session.Screen.GetByLabel("Focus target").FillAsync(Value);
        await session.Agent.ActAsync(
            $"inspect {Value} with {{note}}",
            new ActOptions { Params = new Dictionary<string, object?> { ["note"] = Value, [$"key {Value}"] = new[] { Value } } });
        await Expect.That(session.Screen.GetByText($"missing {Value}")).ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(500));
    }

    private sealed record Run(Report Report, string ResultsDirectory, List<(string Instruction, string Params, string? AgentContext, string[] TitlePath)> Inputs);

    private static async Task<Run> RunAsync(Func<E2ESession, Task> scenario)
    {
        var inputs = new List<(string Instruction, string Params, string? AgentContext, string[] TitlePath)>();
        var recording = new TestExecutor("recording", async (context, _) =>
        {
            inputs.Add((context.Step.Instruction, JsonSerializer.Serialize(context.Step.Params, Relaxed), context.AgentContext, [.. context.Attempt.TitlePath]));
            if (context.Step.Secrets.Count > 0)
            {
                var observation = await context.ObserveAsync();
                await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Focus target\""), context.Step.Secrets[0].Name);
            }

            return Passed("done");
        })
        {
            Version = "1",
            Cache = ExecutorCache.Off,
        };
        var results = Path.Combine(Path.GetTempPath(), "e2e-plain-string-secrets-" + Guid.NewGuid().ToString("N"), "results");
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(AgentAct.CounterPage.World()),
            AgentExecutor = recording,
            AgentContext = "context " + Value,
            BaseUrl = "https://counter.test",
            TestTitle = "suite " + Value + " > title holds " + Value,
            TestFile = "PlainStringSecrets",
            ResultsDirectory = results,
            AssertionTimeout = TimeSpan.FromSeconds(2),
            CacheEnabled = false,
        });
        try
        {
            await scenario(session);
            session.Complete();
        }
        catch (Exception ex)
        {
            session.Complete(ex);
        }

        await session.DisposeAsync();
        return new Run(session.Report() ?? throw new InvalidOperationException("the session left no report"), results, inputs);
    }
}
