// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using System.Text.Json;
using E2E.Internal;

namespace E2E.Acp;

/// <summary>
/// Runs <c>agent.act</c> and <c>agent.assert</c> on any agent that speaks the Agent Client Protocol on stdio: one agent session per test
/// attempt, started by its first agent step and closed when the attempt ends. Upstream's <c>acp</c> (<c>@e2e-dev/acp</c>,
/// <c>executor.ts</c>); <see cref="ClaudeCode"/> and <see cref="Codex"/> start Claude Code and Codex with the step tools as their only tools (Codex can still start a subagent, which fails the step like any tool of its own).
/// </summary>
public static class AcpExecutor
{
    /// <summary>What the session is, before the built-in agent's rules for the same tools.</summary>
    private const string SessionRules =
        "This conversation drives a real application for an end-to-end test, one step per message: an action to perform, or an assertion to judge, with the current screen.\n" +
        "Use only the tools of this conversation. You have no access to the application's source, files, shell, or network.\n" +
        "The rules below are for each step. A step ends when you call done; then wait for the next message. For an assertion, judge the condition from the screen without changing anything.";

    private static readonly string Rules = $"{SessionRules}\n\n{DefaultAgent.BaseRules}\n\n{AgentTools.VerdictRules}";

    /// <summary>How long a step waits for the turn of a step the runner gave up on to stop.</summary>
    private static readonly TimeSpan LastTurnGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates an executor that runs agent steps on the agent <paramref name="command"/> starts in ACP mode on stdio, such as Cursor's
    /// <c>agent</c> with <c>["acp"]</c>. The agent signs in the way it does on its own (its CLI login or its own API key variable).
    /// Upstream's <c>acp({ command, args })</c>.
    /// </summary>
    /// <param name="command">The command that starts the agent. It is run without a shell, so on Windows it must be an executable, not a <c>.cmd</c> shim.</param>
    /// <param name="args">Its arguments, passed one by one.</param>
    /// <param name="options">The agent's environment, model, mode, and rules.</param>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c> when <paramref name="command"/> is empty.</exception>
    public static IStepExecutor Create(string command, IReadOnlyList<string>? args = null, AcpOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ConfigurationException("INVALID_CONFIG", "AcpExecutor.Create takes a command and its args, e.g. AcpExecutor.Create(\"agent\", [\"acp\"])");
        }

        options ??= new AcpOptions();
        var launch = new AgentLaunch(command, args ?? [], options.Env, options.Model, options.Mode, options.SessionMeta);
        return Build(options.Name ?? "acp", options, _ => Task.FromResult(launch));
    }

    /// <summary>
    /// Creates an executor that runs agent steps on Claude Code through Zed's adapter (<c>@agentclientprotocol/claude-agent-acp</c>), signed in
    /// with your <c>claude</c> login. The adapter must be installed in the project (<c>npm install -D @agentclientprotocol/claude-agent-acp</c>)
    /// and found from the working directory up, and Node must be on the <c>PATH</c>. Upstream's <c>claudeCode()</c>.
    /// </summary>
    /// <param name="options">The agent's environment, model, and rules.</param>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c> when the adapter is not installed.</exception>
    public static IStepExecutor ClaudeCode(AcpAgentOptions? options = null)
    {
        options ??= new AcpAgentOptions();
        return Build(options.Name ?? "claude-code", options, Presets.ClaudeCodeLaunch(options));
    }

    /// <summary>
    /// Creates an executor that runs agent steps on Codex through its adapter (<c>@agentclientprotocol/codex-acp</c>), signed in with your
    /// <c>codex</c> login. The adapter must be installed in the project (<c>npm install -D @agentclientprotocol/codex-acp</c>) and found from the
    /// working directory up, and Node must be on the <c>PATH</c>. Upstream's <c>codex()</c>.
    /// </summary>
    /// <param name="options">The agent's environment, model, and rules.</param>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c> when the adapter is not installed.</exception>
    public static IStepExecutor Codex(AcpAgentOptions? options = null)
    {
        options ??= new AcpAgentOptions();
        return Build(options.Name ?? "codex", options, Presets.CodexLaunch(options));
    }

    /// <summary>Upstream's <c>executor</c>: the step executor over how to start an agent.</summary>
    internal static IStepExecutor Build(string name, AcpAgentOptions options, Func<CancellationToken, Task<AgentLaunch>> launch) => new Runner(name, options, launch);

    private sealed class Held
    {
        public required Task<AcpSession> Session { get; init; }

        public required StepSlot Slot { get; init; }

        /// <summary>The tools the session lists, fixed for the attempt.</summary>
        public required IReadOnlyList<ServedTool> Tools { get; init; }

        public bool Introduced { get; set; }

        /// <summary>The turn in flight, settled or not: one the runner gave up on may still be running.</summary>
        public Task<TurnReport>? Turn { get; set; }
    }

    private sealed class Runner(string name, AcpAgentOptions options, Func<CancellationToken, Task<AgentLaunch>> launch) : IStepExecutor
    {
        // Where an attempt's session lives in the attempt's memory: one key per executor, so two ACP agents never share a session.
        private readonly string _memoryKey = "E2E.Acp.session." + Guid.NewGuid().ToString("N");

        public string Name => name;

        public string? Version => "1";

        public ExecutorCache Cache => ExecutorCache.Inherit;

        public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
        {
            var (held, session) = await ReadyAsync(context, cancellationToken).ConfigureAwait(false);
            var active = new ActiveStep(context);
            var text = await StepMessageAsync(active, held.Tools, held.Introduced ? null : [Rules, options.System, context.AgentContext], cancellationToken).ConfigureAwait(false);

            // A step that ended while its screen was read sends no prompt, which no cancel would reach.
            cancellationToken.ThrowIfCancellationRequested();
            held.Introduced = true;
            held.Slot.Active = active;
            TurnReport report;
            try
            {
                var turn = session.PromptAsync(text);
                held.Turn = turn;
                report = await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // The step ended: stop the agent's turn, which the next step waits for.
                    session.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                session.Close();
                context.Attempt.Memory.Remove(_memoryKey);
                throw new AgentException("MODEL_PROVIDER_FAILED", $"The ACP agent session failed: {Redact(context, error.Message)}", error);
            }
            finally
            {
                if (ReferenceEquals(held.Slot.Active, active))
                {
                    held.Slot.Active = null;
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
                context.Attempt.Memory.Remove(_memoryKey);
                return OwnToolUsed(denied);
            }

            return StepTools.VerdictOf(active) ?? NoConclusion(context, report);
        }

        /// <summary>The attempt's session once the turn of the step before it stopped, or a fresh one when that turn's end closed it.</summary>
        private async Task<(Held Held, AcpSession Session)> ReadyAsync(StepExecutorContext context, CancellationToken token)
        {
            while (true)
            {
                var held = Hold(context, token);
                AcpSession session;
                try
                {
                    session = await held.Session.WaitAsync(token).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    // The next step starts a fresh agent rather than wait on this one.
                    context.Attempt.Memory.Remove(_memoryKey);
                    token.ThrowIfCancellationRequested();
                    if (error is ConfigurationException)
                    {
                        throw;
                    }

                    throw new AgentException("MODEL_PROVIDER_FAILED", $"The ACP agent did not start: {Redact(context, error.Message)}", error);
                }

                if (!await SettledAsync(held.Turn, LastTurnGrace, token).ConfigureAwait(false))
                {
                    session.Close();
                    context.Attempt.Memory.Remove(_memoryKey);
                    throw new AgentException("MODEL_PROVIDER_FAILED", "The ACP agent did not stop the turn of a step that already ended");
                }

                // A tool of the agent's own that ran as that turn stopped closed the session and dropped it from the attempt.
                if (context.Attempt.Memory.TryGetValue(_memoryKey, out var current) && ReferenceEquals(current, held))
                {
                    if (!session.IsClosed)
                    {
                        return (held, session);
                    }

                    context.Attempt.Memory.Remove(_memoryKey);
                }
            }
        }

        /// <summary>The attempt's session, started on its first step and closed when the attempt ends.</summary>
        private Held Hold(StepExecutorContext context, CancellationToken token)
        {
            if (context.Attempt.Memory.TryGetValue(_memoryKey, out var existing) && existing is Held kept)
            {
                return kept;
            }

            var gate = new Lock();
            AcpSession? session = null;
            var closed = false;
            var slot = new StepSlot(() => session?.Cancel());
            var tools = StepTools.Create(slot, context);
            Held? held = null;
            void OwnTool(string title)
            {
                if (slot.Active is not { } active)
                {
                    // Run in the tail of a step that already ended: no step to fail, so no later step gets this conversation.
                    ForgetIfCurrent();
                    session?.Close();
                    return;
                }

                active.Deny(title);
                session?.Cancel();
            }

            void ForgetIfCurrent()
            {
                if (context.Attempt.Memory.TryGetValue(_memoryKey, out var current) && ReferenceEquals(current, held))
                {
                    context.Attempt.Memory.Remove(_memoryKey);
                }
            }

            async Task<AcpSession> StartAsync()
            {
                // The first step's end stops a startup still in progress; an open session lives until the attempt ends.
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(context.Attempt.CancellationToken, token);
                var how = await launch(startup.Token).ConfigureAwait(false);
                var started = await AcpSession.StartAsync(how, tools, startup.Token, OwnTool).ConfigureAwait(false);
                lock (gate)
                {
                    session = started;
                    if (closed)
                    {
                        started.Close();
                    }
                }

                return started;
            }

            held = new Held { Slot = slot, Tools = tools, Session = StartAsync() };
            _ = held.Session.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            context.Attempt.Memory[_memoryKey] = held;
            context.Attempt.CancellationToken.Register(() =>
            {
                lock (gate)
                {
                    closed = true;
                    session?.Close();
                }

                // Serial members share one memory, each with its own attempt: the next one starts its own session.
                ForgetIfCurrent();
            });
            return held;
        }

        /// <summary>Counts one prompt turn as one model call, with what the agent reported of it. Upstream's <c>recordTurn</c>.</summary>
        private static void RecordTurn(StepExecutorContext context, AcpSession session, TurnReport report)
        {
            var usage = report.Usage;
            context.Budgets.RecordModelCall(new ExecutorModelCall
            {
                StartedAt = report.StartedAt,
                DurationMs = report.DurationMs,
                Provider = session.AgentName,
                ModelId = session.ModelId,
                InputTokens = usage is null ? null : InputTokens(usage),
                OutputTokens = usage is null ? null : Clamp(usage.OutputTokens),
                CacheReadTokens = usage?.CachedReadTokens is { } read ? Clamp(read) : null,
                CacheWriteTokens = usage?.CachedWriteTokens is { } write ? Clamp(write) : null,
                EstimatedCostUsd = report.CostUsd,
            });
        }

        /// <summary>
        /// The turn's input tokens with the cached ones included, as e2e counts them. ACP leaves open whether an agent's <c>inputTokens</c>
        /// include them: the Claude adapter's do not (its total adds the cache on top), Codex's do (its total is input and output alone).
        /// </summary>
        private static int InputTokens(TurnUsage usage)
        {
            var cached = (usage.CachedReadTokens ?? 0) + (usage.CachedWriteTokens ?? 0);
            var cacheApart = cached > 0 && usage.TotalTokens >= usage.InputTokens + usage.OutputTokens + cached;
            return Clamp(cacheApart ? usage.InputTokens + cached : usage.InputTokens);
        }

        private static int Clamp(long tokens) => (int)Math.Clamp(tokens, 0, int.MaxValue);

        /// <summary>Whether <paramref name="turn"/> settles within <paramref name="grace"/>; no turn has nothing to wait for.</summary>
        private static async Task<bool> SettledAsync(Task? turn, TimeSpan grace, CancellationToken token)
        {
            if (turn is null)
            {
                return true;
            }

            try
            {
                await turn.WaitAsync(grace, token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (Exception) when (turn.IsCompleted)
            {
                // A turn that failed has stopped.
            }

            return true;
        }

        /// <summary>The message for one step: the rules on the first, then the step, what already ran, and the screen. Upstream's <c>stepMessage</c>.</summary>
        private static async Task<string> StepMessageAsync(ActiveStep active, IReadOnlyList<ServedTool> tools, string?[]? introduction, CancellationToken token)
        {
            var context = active.Context;
            var parts = new List<string>();
            if (introduction is not null)
            {
                parts.AddRange(introduction.Where(part => !string.IsNullOrWhiteSpace(part))!);
            }

            var step = context.Step;
            parts.Add($"{(step.Kind == ExecutorStepKind.Assert ? "Assertion" : "Step")}: {step.Instruction}");
            if (PlainParams(step.Params) is { } parameters)
            {
                parts.Add($"Parameters: {JsonSerializer.Serialize(parameters)}");
            }

            if (step.Secrets.Count > 0)
            {
                parts.Add($"Secrets to fill with fill_secret, by name: {string.Join(", ", step.Secrets.Select(secret => secret.Purpose is null ? secret.Name : $"{secret.Name} ({secret.Purpose})"))}");
            }

            var missing = StepTools.MissingTools(active, tools);
            if (missing.Count > 0)
            {
                parts.Add($"Not offered in this step: {string.Join(", ", missing)}.");
            }

            if (context.Ledger != "")
            {
                parts.Add($"Steps completed so far in this test, including any replayed without you:\n{context.Ledger}");
            }

            if (context.ReplayedPrefix is { } prefix)
            {
                parts.Add($"A cached replay already performed these actions for this step: {string.Join("; ", prefix.ReplayedActions)}. It stopped ({ReplayHandOffReasons.Name(prefix.StopReason)}). Continue from the current screen; do not redo them.");
                if (prefix.UncertainAction is not null)
                {
                    parts.Add($"This replayed action may have taken effect although it failed: {prefix.UncertainAction}. Check the screen before doing anything like it again.");
                }
            }

            parts.Add(await StepTools.OpeningScreenAsync(active, token).ConfigureAwait(false));
            return string.Join("\n\n", parts);
        }

        /// <summary>The params without secret placeholders, which are listed by name instead.</summary>
        private static Dictionary<string, JsonElement>? PlainParams(IReadOnlyDictionary<string, JsonElement>? parameters)
        {
            var plain = parameters?.Where(pair => !IsSecretPlaceholder(pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value);
            return plain is { Count: > 0 } ? plain : null;
        }

        /// <summary>A secret in the params, as the runner projects it: <c>{ "kind": "secret", "name", "purpose" }</c>.</summary>
        private static bool IsSecretPlaceholder(JsonElement value) =>
            value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "secret";

        /// <summary>The verdict of a step where the agent acted with a tool of its own, which the runner neither saw nor recorded. Upstream's <c>ownToolUsed</c>.</summary>
        private static StepVerdict OwnToolUsed(string title) => new(
            StepVerdictStatus.Failed,
            $"The agent ran a tool of its own without asking ({title}); only the step tools may act, so the step was stopped.",
            "POLICY_DENIED");

        private static StepVerdict NoConclusion(StepExecutorContext context, TurnReport report)
        {
            var said = report.Text.Trim().Split('\n')[^1].TrimEnd('\r');
            said = Redact(context, said.Length > 300 ? said[..300] : said);
            return new StepVerdict(
                StepVerdictStatus.Failed,
                $"The agent ended its turn ({report.StopReason}) without done{(said == "" ? "" : $": {said}")}",
                "STEP_NO_CONCLUSION");
        }

        private static string Transcript(string prompt, ActiveStep active, TurnReport report) =>
            string.Join(
                '\n',
                [
                    $"> {prompt}",
                    $"tools: {(active.Calls.Count == 0 ? "none" : string.Join(", ", active.Calls))}",
                    .. report.Rejected.Count == 0 ? (string[])[] : [$"rejected tools of the agent's own: {string.Join(", ", report.Rejected)}"],
                    .. report.Ran.Count == 0 ? (string[])[] : [$"tools of the agent's own that ran without asking: {string.Join(", ", report.Ran)}"],
                    $"stop: {report.StopReason}",
                    report.Text,
                ]);

        /// <summary>Text from the agent or its process goes through the step's redactor before it reaches an error or a verdict.</summary>
        private static string Redact(StepExecutorContext context, string text) => context.Host?.Scope.Redactor.Redact(text) ?? text;
    }
}
