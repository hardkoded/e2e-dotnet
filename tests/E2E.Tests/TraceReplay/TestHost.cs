// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// Upstream's <c>makeHost</c> of <c>trace-replay.test.ts</c>: a host whose every look, however far it settles, reads
/// <see cref="Capture"/>; a test swaps the screen sequence in through it. Every action is logged under upstream's name for its verb.
/// </summary>
internal sealed class TestHost : IReplayHost
{
    private readonly Action<string, object?>? _onAction;

    private TestHost(IReadOnlyList<SemanticNode> nodes, Action<string, object?>? onAction)
    {
        _onAction = onAction;
        Actions = new Grammar(this);
        Capture = () =>
        {
            Observations++;
            return Screen(nodes);
        };
    }

    public static SemanticNode Upgrade { get; } = new() { Ref = "n1", Role = "button", Name = "Upgrade" };

    public static SemanticNode Email { get; } = new() { Ref = "n2", Role = "textbox", Name = "Email" };

    public static RecordedAction TapUpgrade { get; } = new() { Kind = "tap", Role = "button", Name = "Upgrade" };

    public static RecordedAction TypeEmail { get; } = new() { Kind = "fill", Role = "textbox", Name = "Email", Value = "x" };

    public List<string> Calls { get; } = [];

    public int Observations { get; set; }

    /// <summary>How far each look asked to settle, in order.</summary>
    public List<SettleMode> Looks { get; } = [];

    /// <summary>The actions the replay recorded, in order.</summary>
    public List<RecordedAction> Committed { get; } = [];

    public Func<Observation> Capture { get; set; }

    public IExecutorActions Actions { get; }

    public CancellationToken Signal { get; set; } = CancellationToken.None;

    public Redactor Redact => Redactor.None;

    public TimeSpan ReplayTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Upstream's <c>remainingMs</c> option of <c>makeHost</c>: what is left of the step's clock, in milliseconds.</summary>
    public int Remaining { get; set; } = 60_000;

    public int RemainingMs() => Remaining;

    /// <summary>The clock the replay reads; a test swaps in a fake one, as upstream's fake timers.</summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    public static TestHost MakeHost(IReadOnlyList<SemanticNode>? nodes = null, Action<string, object?>? onAction = null) =>
        new(nodes ?? [Upgrade, Email], onAction);

    /// <summary>One observed screen over the given nodes.</summary>
    public static Observation Screen(IReadOnlyList<SemanticNode> nodes) => new() { Route = "/plan", Roots = nodes };

    public Task<Observation> ObserveAsync(SettleMode mode)
    {
        Looks.Add(mode);
        return Task.FromResult(Capture());
    }

    public bool TryReserve() => true;

    public void MarkActing()
    {
    }

    public Task CommitAsync(RecordedAction action)
    {
        Committed.Add(action);
        return Task.CompletedTask;
    }

    /// <summary>The options of each tap and double tap, in call order.</summary>
    public List<ClickOptions?> Clicks { get; } = [];

    private Task Click(string name, ExecutorTarget target, ClickOptions? options)
    {
        Clicks.Add(options);
        return Act(name, target.Id);
    }

    private Task Act(string name, object? detail = null)
    {
        Calls.Add(name);
        _onAction?.Invoke(name, detail);
        return Task.CompletedTask;
    }

    private sealed class Grammar(TestHost host) : IExecutorActions
    {
        public Task TapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => host.Click("tap", target, options);

        public Task DoubleTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => host.Click("doubleTap", target, options);

        public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) => host.Act("type", (target.Id, value));

        public Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default) => host.Act("typeSecret", (target.Id, secretName));

        public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => host.Act("clear", target.Id);

        public Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default) => host.Act("press", (target?.Id, key));

        public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) => host.Act("select", (target.Id, value));

        public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) => host.Act("check", (target.Id, check));

        public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => host.Act("scrollTo", target.Id);

        public Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default) =>
            host.Act("scrollUntil", (text, direction, list?.Id));

        public Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default) =>
            host.Act("scroll", (direction, list?.Id));

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default) => host.Act("navigate", url);

        public Task BackAsync(CancellationToken cancellationToken = default) => host.Act("back");
    }
}
