// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E;

/// <summary>
/// The exports of upstream's <c>e2e/agent</c> entrypoint (<c>agent/public.ts</c>, from <c>agent/primitives.ts</c>) that an executor
/// keeping the built-in actions and verdicts builds on: <c>CreateGrammarTools</c> and <c>CreateVerdictTool</c>, with
/// <c>VerdictRules</c>. <c>DefaultAgent.BaseRules</c> is upstream's <c>BASE_RULES</c> and <c>StepExecutors.IsRuntimeHardStop</c> its
/// <c>isRuntimeHardStop</c>. .NET-only: the holder class, which in upstream is a module.
/// </summary>
public static class AgentPrimitives
{
    /// <summary>
    /// The verdict rules the built-in agent appends to its instructions. Upstream's <c>VERDICT_RULES</c>, for the port's
    /// <c>done</c> tool where upstream names <c>complete_step</c>.
    /// </summary>
    public const string VerdictRules =
        "Verdict rules:\n" +
        "- When the step's goal is achieved, or you are certain it cannot be, call done exactly once.\n" +
        "- Conclude from the newest screen and changes already in this conversation; never guess success. Observe again before concluding only when the newest result shows work still in progress (a spinner, \"Saving…\", a pending state).\n" +
        "- \"passed\" means the application behaved as the step required. \"failed\" means it did not. \"blocked\" means credentials, the environment, or test setup prevented a product verdict — blocked says nothing about the product and requires a code.";

    /// <summary>
    /// The built-in agent's action tools for the verbs the context's target supports, each run through the step's own
    /// <see cref="IExecutorActions"/>, where the runtime authorizes, counts, and records the action so the step replays from the
    /// cache. Upstream's <c>createGrammarTools</c>. The tools drive the context the runtime builds for a step, and throw
    /// <see cref="InvalidOperationException"/> on one built by hand.
    /// </summary>
    public static IReadOnlyList<GrammarTool> CreateGrammarTools(StepExecutorContext context, GrammarToolOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var host = context.Host
            ?? throw new InvalidOperationException("The grammar tools drive the context of a step the runtime opened; they cannot run on a context built by hand.");
        var act = new ActTools(host.Scope, host.Feed);
        var guard = options?.Guard;
        return
        [
            .. AgentTools.CreateGrammarTools(host.Scope.EngineCapabilities).Select(definition =>
                new GrammarTool(
                    definition.Name,
                    definition.Description,
                    definition.Parameters,
                    string.Equals(definition.Name, "observe", StringComparison.Ordinal) || definition.Name.StartsWith("scroll", StringComparison.Ordinal),
                    async (input, token) =>
                    {
                        async Task<string> Run()
                        {
                            var outcome = await act.ExecuteAsync(
                                new ModelToolCall { Id = definition.Name, Name = definition.Name, Arguments = input },
                                context.Actions,
                                host.Accounting,
                                null,
                                token,
                                context).ConfigureAwait(false);
                            if (!outcome.Succeeded)
                            {
                                var text = outcome.Content.StartsWith("failed: ", StringComparison.Ordinal) ? outcome.Content["failed: ".Length..] : outcome.Content;
                                throw new AgentException(outcome.Code ?? "ACTION_FAILED", text);
                            }

                            return outcome.Content;
                        }

                        return guard is null ? await Run().ConfigureAwait(false) : await guard(Run).ConfigureAwait(false);
                    })),
        ];
    }

    /// <summary>
    /// The conclusion tool and the closed policy that accepts or refuses its verdict. Upstream's <c>createVerdictTool</c>; the tool
    /// is <c>done</c>, where upstream's is <c>complete_step</c>.
    /// </summary>
    public static VerdictTool CreateVerdictTool() => new();
}

/// <summary>Upstream's <c>GrammarToolOptions</c>.</summary>
public sealed class GrammarToolOptions
{
    /// <summary>
    /// Wraps the body of every tool call when its turn in the step's call order comes, so a call queued behind a verdict or a stop
    /// can answer instead of running. Upstream's <c>guard</c>.
    /// </summary>
    public Func<Func<Task<string>>, Task<string>>? Guard { get; init; }
}

/// <summary>One action tool of the built-in agent's grammar: what a model is told about it, and what runs when it calls it.</summary>
public sealed class GrammarTool
{
    private readonly Func<JsonElement, CancellationToken, Task<string>> _run;

    internal GrammarTool(string name, string description, JsonElement inputSchema, bool readOnly, Func<JsonElement, CancellationToken, Task<string>> run)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        ReadOnly = readOnly;
        _run = run;
    }

    public string Name { get; }

    public string Description { get; }

    /// <summary>The JSON schema of the tool's input object.</summary>
    public JsonElement InputSchema { get; }

    /// <summary>True for a tool that changes nothing in the app, so an assertion may use it.</summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// Runs the tool and answers the screen after it. A refusal or a failed action is thrown as an <see cref="E2EException"/>, and a
    /// hard stop of the step as an <see cref="AgentException"/> with a runtime code.
    /// </summary>
    public Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default) => _run(input, cancellationToken);
}

/// <summary>Upstream's <c>VerdictTool</c>: the <c>done</c> tool and the verdict it collected.</summary>
public sealed class VerdictTool
{
    private readonly VerdictPolicy _policy = AgentTools.CreateVerdictTool();
    private StepVerdict? _verdict;

    internal VerdictTool()
    {
    }

    public string Name => VerdictPolicy.Name;

    public string Description => _policy.Tool.Description;

    public JsonElement InputSchema => _policy.Tool.Parameters;

    /// <summary>The verdict once the model called the tool with an accepted one.</summary>
    public StepVerdict? Verdict => _verdict;

    /// <summary>True once a verdict landed.</summary>
    public bool Concluded => _verdict is not null;

    /// <summary>Takes a call. A refused verdict answers <c>Rejected: </c> and why, so the model can call again.</summary>
    public string Execute(JsonElement input)
    {
        var outcome = _policy.Execute(new ModelToolCall { Id = Name, Name = Name, Arguments = input });
        if (!outcome.Succeeded)
        {
            return "Rejected: " + (outcome.Content.StartsWith("failed: ", StringComparison.Ordinal) ? outcome.Content["failed: ".Length..] : outcome.Content);
        }

        _verdict = new StepVerdict(
            outcome.Status switch
            {
                "passed" => StepVerdictStatus.Passed,
                "blocked" => StepVerdictStatus.Blocked,
                _ => StepVerdictStatus.Failed,
            },
            outcome.Summary ?? "",
            outcome.Code);
        return "Step concluded.";
    }
}
