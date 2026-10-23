// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.PlainStringSecrets;

/// <summary>
/// The first <c>describe</c> of upstream's <c>plain-string-secrets.test.ts</c>. Only its executor test is ported. The others need the
/// runner, its report and reporter files, its titles and ids, and its output events, which the port does not have. Upstream
/// registers the secret in the config, so it is known before any step. The port registers a secret when an <c>act</c> receives it in
/// its params, so the act that types it comes first here.
/// </summary>
public sealed class ASecretValuePassedAsAPlainStringTests
{
    private const string Value = "plain-Zr8Kq2Wm5Tx9";

    private const string Marker = "<secret:probe>";

    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Partial: the secret handle is declared by name alone, since the port's secret has no default purpose, and the act that
    /// types it runs before the act that spells the value as plain strings, so the secret is known to the redaction by then.
    /// </summary>
    [Fact]
    public async Task Partial_Hands_the_executor_the_instruction_params_and_agent_context_redacted()
    {
        var inputs = new List<(string Instruction, string Params, string? AgentContext)>();
        var recording = new TestExecutor("recording", async (context, _) =>
        {
            inputs.Add((context.Step.Instruction, JsonSerializer.Serialize(context.Step.Params, Relaxed), context.AgentContext));
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
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(AgentAct.CounterPage.World()),
            AgentExecutor = recording,
            AgentContext = "context " + Value,
            BaseUrl = "https://counter.test",
            TestTitle = "suite " + Value + " > title holds " + Value,
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync("paste the key into the focus target", new ActOptions { Params = new Dictionary<string, object?> { ["key"] = Secret.Create("probe", Value) } });
        await session.Agent.ActAsync(
            $"inspect {Value} with {{note}}",
            new ActOptions { Params = new Dictionary<string, object?> { ["note"] = Value, [$"key {Value}"] = new[] { Value } } });
        session.Complete();

        Assert.Equal(2, inputs.Count);
        Assert.Equal("""{"key":{"kind":"secret","name":"probe"}}""", inputs[0].Params);
        Assert.Equal($"inspect {Marker} with {{note}}", inputs[1].Instruction);
        Assert.Equal($$"""{"note":"{{Marker}}","key {{Marker}}":["{{Marker}}"]}""", inputs[1].Params);
        // The title spells the secret too, as upstream's does: both steps' context opens with it redacted.
        Assert.All(inputs, input => Assert.Equal("Test: suite " + Marker + " > title holds " + Marker + "\ncontext " + Marker, input.AgentContext));
        Assert.DoesNotContain(Value, JsonSerializer.Serialize(inputs), StringComparison.Ordinal);
    }
}
