// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace E2E.Acp;

/// <summary>
/// The executors of upstream's <c>@e2e-dev/acp</c> (<c>executor.ts</c>): <c>acp</c>, <c>claudeCode</c>, and <c>codex</c>, which run
/// <c>agent.act</c> and <c>agent.assert</c> on a coding agent you already use. .NET-only: the holder class, which in upstream is a module;
/// the three functions are <see cref="Acp"/>, <see cref="ClaudeCode"/>, and <see cref="Codex"/>.
/// </summary>
public static class AcpExecutors
{
    /// <summary>
    /// Runs <c>agent.act</c> and <c>agent.assert</c> on any agent that speaks ACP on stdio: one agent session per test attempt, started by
    /// its first agent step and closed when the attempt ends.
    /// </summary>
    public static IStepExecutor Acp(AcpOptions options)
    {
        if (options is null || string.IsNullOrEmpty(options.Command))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                "acp() takes { Command, Args }, e.g. Acp(new AcpOptions { Command = \"agent\", Args = [\"acp\"] }), or use ClaudeCode() or Codex()");
        }

        var launch = new AgentLaunch
        {
            Command = options.Command,
            Args = options.Args ?? [],
            Env = options.Env,
            Model = options.Model,
            Mode = options.Mode,
            SessionMeta = options.SessionMeta,
        };
        return new AcpStepExecutor(options.Name ?? "acp", options, () => Task.FromResult(launch));
    }

    /// <summary>Claude Code through Zed's adapter (<c>@agentclientprotocol/claude-agent-acp</c>), signed in with your <c>claude</c> login. The agent runs the step tools as its only tools.</summary>
    public static IStepExecutor ClaudeCode(AcpAgentOptions? options = null)
    {
        options ??= new AcpAgentOptions();
        return new AcpStepExecutor(options.Name ?? "claude-code", options, Presets.ClaudeCodeLaunch(options));
    }

    /// <summary>Codex through its adapter (<c>@agentclientprotocol/codex-acp</c>), signed in with your <c>codex</c> login. The agent runs the step tools as its only tools.</summary>
    public static IStepExecutor Codex(AcpAgentOptions? options = null)
    {
        options ??= new AcpAgentOptions();
        return new AcpStepExecutor(options.Name ?? "codex", options, Presets.CodexLaunch(options));
    }
}

internal sealed class AcpStepExecutor : IStepExecutor
{
    /// <summary>What the session is, before the built-in agent's rules for the same tools.</summary>
    private const string SessionRules =
        "This conversation drives a real application for an end-to-end test, one step per message: an action to perform, or an assertion to judge, with the current screen.\n" +
        "Use only the tools of this conversation. You have no access to the application's source, files, shell, or network.\n" +
        "The rules below are for each step. A step ends when you call done; then wait for the next message. For an assertion, judge the condition from the screen without changing anything.";

    private static readonly string Rules = SessionRules + "\n\n" + DefaultAgent.BaseRules + "\n\n" + AgentPrimitives.VerdictRules;

    /// <summary>How long a step waits for the turn of a step the runner gave up on to stop.</summary>
    private static readonly TimeSpan LastTurnGrace = TimeSpan.FromSeconds(5);

    private readonly AcpAgentOptions _options;
    private readonly Func<Task<AgentLaunch>> _launch;

    // Where an attempt's session lives in the attempt's memory: one key per executor, so two ACP agents never share a session.
    private readonly string _memoryKey = "@e2e-dev/acp.session." + Guid.NewGuid().ToString("D");

    public AcpStepExecutor(string name, AcpAgentOptions options, Func<Task<AgentLaunch>> launch)
    {
        Name = name;
        _options = options;
        _launch = launch;
    }

    public string Name { get; }

    public string? Version => "1";

    public ExecutorCache Cache => ExecutorCache.Inherit;

    public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
    {
        var (held, session) = await ReadyAsync(context, cancellationToken).ConfigureAwait(false);
        var active = new ActiveStep(context);
        var text = await StepMessageAsync(active, held.Tools, held.Introduced ? null : [Rules, _options.System, context.AgentContext], cancellationToken).ConfigureAwait(false);
        // A step that ended while its screen was read sends no prompt, which no cancel would reach.
        cancellationToken.ThrowIfCancellationRequested();
        held.Introduced = true;
        held.Slot.Active = active;
        TurnReport report;
        using (cancellationToken.Register(session.Cancel))
        {
            try
            {
                var turn = session.PromptAsync(text);
                held.Turn = turn;
                report = await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // The wait may have ended before the registration ran: the agent still hears the turn is over.
                    session.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                session.Close();
                Forget(context, held);
                throw new AgentException("MODEL_PROVIDER_FAILED", "The ACP agent session failed: " + error.Message, error);
            }
            finally
            {
                if (ReferenceEquals(held.Slot.Active, active))
                {
                    held.Slot.Active = null;
                }
            }
        }

        context.AttachTranscript(Transcript(text, active, report));
        if (!cancellationToken.IsCancellationRequested)
        {
            RecordTurn(context, session, report);
        }

        if (active.Halted is { } halted)
        {
            ExceptionDispatchInfo.Capture(halted).Throw();
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (active.Denied is { } denied)
        {
            // The conversation holds what the tool read: the next step starts a fresh agent.
            session.Close();
            Forget(context, held);
            return OwnToolUsed(denied);
        }

        return StepTools.VerdictOf(active) ?? NoConclusion(report);
    }

    /// <summary>The attempt's session once the turn of the step before it stopped, or a fresh one when that turn's end closed it.</summary>
    private async Task<(Held Held, IAcpSession Session)> ReadyAsync(StepExecutorContext context, CancellationToken cancellationToken)
    {
        while (true)
        {
            var held = Hold(context, cancellationToken);
            IAcpSession session;
            try
            {
                session = await held.Session.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // The next step starts a fresh agent rather than wait on this one; a session still starting closes when it is up.
                Forget(context, held);
                _ = held.Session.ContinueWith(static task => task.Result.Close(), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
                cancellationToken.ThrowIfCancellationRequested();
                if (error is ConfigurationException)
                {
                    throw;
                }

                throw new AgentException("MODEL_PROVIDER_FAILED", "The ACP agent did not start: " + error.Message, error);
            }

            if (!await SettledAsync(held.Turn, LastTurnGrace).ConfigureAwait(false))
            {
                session.Close();
                Forget(context, held);
                throw new AgentException("MODEL_PROVIDER_FAILED", "The ACP agent did not stop the turn of a step that already ended");
            }

            // A tool of the agent's own that ran as that turn stopped closed the session and dropped it from the attempt.
            if (!IsHeld(context, held))
            {
                continue;
            }

            return (held, session);
        }
    }

    /// <summary>Counts one prompt turn as one model call, with what the agent reported of it.</summary>
    private static void RecordTurn(StepExecutorContext context, IAcpSession session, TurnReport report)
    {
        var usage = report.Usage;
        context.Budgets.RecordModelCall(new ExecutorModelCall
        {
            StartedAt = report.StartedAt,
            DurationMs = report.DurationMs,
            Provider = session.AgentName,
            ModelId = session.ModelId,
            InputTokens = usage is null ? null : InputTokens(usage),
            OutputTokens = usage is null ? null : (int)usage.OutputTokens,
            CacheReadTokens = (int?)usage?.CachedReadTokens,
            CacheWriteTokens = (int?)usage?.CachedWriteTokens,
            EstimatedCostUsd = report.CostUsd,
        });
    }

    /// <summary>The attempt's session, started on its first step and closed when the attempt ends.</summary>
    private Held Hold(StepExecutorContext context, CancellationToken stepToken)
    {
        var memory = context.Attempt.Memory;
        lock (memory)
        {
            if (memory.TryGetValue(_memoryKey, out var existing) && existing is Held current)
            {
                return current;
            }

            IAcpSession? session = null;
            var closed = false;
            var slot = new StepSlot();
            slot.Halt = () => session?.Cancel();
            var tools = StepTools.Create(slot, context);
            Held held = null!;
            void OwnTool(string title)
            {
                if (slot.Active is not { } step)
                {
                    // Run in the tail of a step that already ended: no step to fail, so no later step gets this conversation.
                    session?.Close();
                    lock (memory)
                    {
                        if (IsHeld(context, held))
                        {
                            memory.Remove(_memoryKey);
                        }
                    }

                    return;
                }

                step.Denied ??= title;
                session?.Cancel();
            }

            async Task<IAcpSession> StartAsync()
            {
                // The first step's end stops a startup still in progress; an open session lives until the attempt ends.
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.Attempt.CancellationToken, stepToken);
                var how = await _launch().ConfigureAwait(false);
                var started = await AcpSession.StartAsync(how, tools, linked.Token, OwnTool).ConfigureAwait(false);
                session = started;
                if (Volatile.Read(ref closed))
                {
                    started.Close();
                }

                return started;
            }

            held = new Held(slot, tools, Task.Run(StartAsync));
            _ = held.Session.ContinueWith(static task => task.Exception, TaskScheduler.Default);
            memory[_memoryKey] = held;
            var heldNow = held;
            context.Attempt.CancellationToken.Register(() =>
            {
                Volatile.Write(ref closed, true);
                session?.Close();
                // Serial members share one memory, each with its own attempt: the next one starts its own session.
                lock (memory)
                {
                    if (IsHeld(context, heldNow))
                    {
                        memory.Remove(_memoryKey);
                    }
                }
            });
            return held;
        }
    }

    private bool IsHeld(StepExecutorContext context, Held held)
    {
        lock (context.Attempt.Memory)
        {
            return context.Attempt.Memory.TryGetValue(_memoryKey, out var value) && ReferenceEquals(value, held);
        }
    }

    private void Forget(StepExecutorContext context, Held held)
    {
        lock (context.Attempt.Memory)
        {
            if (IsHeld(context, held))
            {
                context.Attempt.Memory.Remove(_memoryKey);
            }
        }
    }

    /// <summary>The message for one step: the rules on the first, then the step, what already ran, and the screen.</summary>
    private static async Task<string> StepMessageAsync(ActiveStep active, IReadOnlyList<ServedTool> tools, string?[]? introduction, CancellationToken cancellationToken)
    {
        var context = active.Context;
        var parts = new List<string>();
        if (introduction is not null)
        {
            parts.AddRange(introduction.Where(part => !string.IsNullOrWhiteSpace(part))!);
        }

        var step = context.Step;
        parts.Add((step.Kind == ExecutorStepKind.Assert ? "Assertion" : "Step") + ": " + step.Instruction);
        if (PlainParams(step.Params) is { } parameters)
        {
            parts.Add("Parameters: " + JsonSerializer.Serialize(parameters));
        }

        if (step.Secrets.Count > 0)
        {
            parts.Add("Secrets to fill with fill_secret, by name: " + string.Join(", ", step.Secrets.Select(secret => secret.Name + " (" + secret.Purpose + ")")));
        }

        var missing = StepTools.MissingTools(active, tools);
        if (missing.Count > 0)
        {
            parts.Add("Not offered in this step: " + string.Join(", ", missing) + ".");
        }

        if (context.Ledger.Length > 0)
        {
            parts.Add("Steps completed so far in this test, including any replayed without you:\n" + context.Ledger);
        }

        if (context.ReplayedPrefix is { } prefix)
        {
            parts.Add(
                "A cached replay already performed these actions for this step: " + string.Join("; ", prefix.ReplayedActions) +
                ". It stopped (" + ReplayHandOffReasons.Name(prefix.StopReason) + "). Continue from the current screen; do not redo them.");
            if (prefix.UncertainAction is not null)
            {
                parts.Add("This replayed action may have taken effect although it failed: " + prefix.UncertainAction + ". Check the screen before doing anything like it again.");
            }
        }

        parts.Add(await StepTools.OpeningScreenAsync(active, cancellationToken).ConfigureAwait(false));
        return string.Join("\n\n", parts);
    }

    /// <summary>The params without secret placeholders, which are listed by name instead.</summary>
    private static Dictionary<string, JsonElement>? PlainParams(IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var plain = parameters.Where(pair => !IsSecretPlaceholder(pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return plain.Count == 0 ? null : plain;
    }

    /// <summary>A secret in the params, as the runner projects it: <c>{ kind: "secret", name, purpose }</c>.</summary>
    private static bool IsSecretPlaceholder(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "secret";

    /// <summary>
    /// The turn's input tokens with the cached ones included, as e2e counts them. ACP leaves open whether an agent's <c>inputTokens</c>
    /// include them: the Claude adapter's do not (its total adds the cache on top), Codex's do (its total is input and output alone).
    /// </summary>
    private static int InputTokens(AcpUsage usage)
    {
        var cached = (usage.CachedReadTokens ?? 0) + (usage.CachedWriteTokens ?? 0);
        var cacheApart = cached > 0 && usage.TotalTokens >= usage.InputTokens + usage.OutputTokens + cached;
        return (int)(cacheApart ? usage.InputTokens + cached : usage.InputTokens);
    }

    /// <summary>Whether <paramref name="turn"/> settles within <paramref name="limit"/>; no turn has nothing to wait for.</summary>
    private static async Task<bool> SettledAsync(Task? turn, TimeSpan limit)
    {
        if (turn is null)
        {
            return true;
        }

        try
        {
            await turn.WaitAsync(limit).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception)
        {
            // A turn that failed has stopped.
        }

        return true;
    }

    /// <summary>The verdict of a step where the agent acted with a tool of its own, which the runner neither saw nor recorded.</summary>
    private static StepVerdict OwnToolUsed(string title) =>
        new(
            StepVerdictStatus.Failed,
            "The agent ran a tool of its own without asking (" + title + "); only the step tools may act, so the step was stopped.",
            "POLICY_DENIED");

    private static StepVerdict NoConclusion(TurnReport report)
    {
        var lines = report.Text.Trim().Split('\n');
        var said = lines[^1];
        said = said.Length > 300 ? said[..300] : said;
        return new StepVerdict(
            StepVerdictStatus.Failed,
            "The agent ended its turn (" + report.StopReason + ") without done" + (said.Length == 0 ? "" : ": " + said),
            "STEP_NO_CONCLUSION");
    }

    private static string Transcript(string prompt, ActiveStep active, TurnReport report)
    {
        var lines = new List<string>
        {
            "> " + prompt,
            "tools: " + (active.Calls.Count == 0 ? "none" : string.Join(", ", active.Calls)),
        };
        if (report.Rejected.Count > 0)
        {
            lines.Add("rejected tools of the agent's own: " + string.Join(", ", report.Rejected));
        }

        if (report.Ran.Count > 0)
        {
            lines.Add("tools of the agent's own that ran without asking: " + string.Join(", ", report.Ran));
        }

        lines.Add("stop: " + report.StopReason);
        lines.Add(report.Text);
        return string.Join("\n", lines);
    }

    private sealed class Held(StepSlot slot, IReadOnlyList<ServedTool> tools, Task<IAcpSession> session)
    {
        public StepSlot Slot { get; } = slot;

        /// <summary>The tools the session lists, fixed for the attempt.</summary>
        public IReadOnlyList<ServedTool> Tools { get; } = tools;

        public Task<IAcpSession> Session { get; } = session;

        public bool Introduced { get; set; }

        /// <summary>The turn in flight, settled or not: one the runner gave up on may still be running.</summary>
        public volatile Task? Turn;
    }
}
