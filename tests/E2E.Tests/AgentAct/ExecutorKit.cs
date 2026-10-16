// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.AgentAct;

/// <summary>
/// A hand-rolled step executor for the tests, the C# shape of the object literals upstream's tests write:
/// <c>{ name, version, cache, model, judge, runStep }</c>.
/// </summary>
internal sealed class TestExecutor(string name, Func<StepExecutorContext, CancellationToken, Task<StepVerdict>> run) : IStepExecutor
{
    public string Name => name;

    public string? Version { get; init; }

    public ExecutorCache Cache { get; init; } = ExecutorCache.Inherit;

    public IAgentModel? Model { get; init; }

    public IAgentModel? Judge { get; init; }

    public Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken) => run(context, cancellationToken);
}

internal static class ExecutorKit
{
    /// <summary>
    /// The home page of upstream's fixture app that the secret tests use: the counter, an Email and a Focus target field, and a
    /// Password field the engine masks. <paramref name="typed"/> reads what was typed into the password field.
    /// </summary>
    public static DocumentWorld WorldWithAPassword(Action<string> typed)
    {
        return new DocumentWorld().Map("/", page =>
        {
            var count = 0;
            var counter = page.Status("Counter");
            counter.Text = "0";
            page.Button("Increment", () => counter.Text = (++count).ToString(System.Globalization.CultureInfo.InvariantCulture));
            page.Textbox("Email");
            page.Textbox("Focus target");
            page.Roots.Add(new DocumentElement
            {
                Role = "textbox",
                Name = "Password",
                Secure = true,
                InputPurpose = "password",
                OnFill = typed,
            });
        });
    }

    public static StepVerdict Passed(string summary) => new(StepVerdictStatus.Passed, summary);

    public static StepVerdict Failed(string summary, string? code = null) => new(StepVerdictStatus.Failed, summary, code);

    public static StepVerdict Blocked(string summary, string code) => new(StepVerdictStatus.Blocked, summary, code);

    /// <summary>
    /// Upstream's <c>nodeIdFor</c>: the id of the first node whose line reads as <paramref name="description"/>, such as
    /// <c>button "Increment"</c>. The port's id is the ref the line ends its name with.
    /// </summary>
    public static ExecutorTarget TargetFor(string text, string description)
    {
        var match = Regex.Match(text, Regex.Escape(description) + @"[^\n]*?\[ref=(?<id>[^\]]+)\]");
        Assert.True(match.Success, "the screen lists no node reading " + description + ":\n" + text);
        return new ExecutorTarget(match.Groups["id"].Value);
    }
}
