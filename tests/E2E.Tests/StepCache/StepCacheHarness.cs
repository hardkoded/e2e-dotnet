// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.StepCache;

/// <summary>
/// Upstream's <c>makeHost</c>, <c>makeSession</c>, <c>fakeContext</c>, and <c>entryContext</c> of <c>step-cache.test.ts</c>. The host's
/// locations come from <c>paths</c> in order and its screens from <c>screens</c> in order, each repeating its last once spent
/// (upstream's spent location is a surface without a URL, which the port's screens never are). Every action the replay
/// performs is committed to the session's recorder, as the act's host does.
/// </summary>
internal static class StepCacheHarness
{
    public const string Key = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static SemanticNode SavedMarker { get; } = new() { Ref = "m1", Role = "status", Name = "Marker", Text = "saved" };

    public static RecordedTarget SavedAnchor { get; } = new() { Role = "status", Name = "Marker", Text = "saved" };

    /// <summary>The step every session here runs, as an entry records it.</summary>
    public static CacheEntry ExampleStep => new()
    {
        Schema = FileStepCache.SchemaVersion,
        Test = "tests/example > step",
        Instruction = "open billing",
        Engine = "web",
        ParamsDigest = CacheKeys.ParamsDigest(null),
        CallIndex = 0,
        Agent = "default",
    };

    public static StepHost MakeHost(string[] paths, params SemanticNode[][] screens) => new(paths, screens);

    /// <summary>A host on <paramref name="time"/>, which a test fakes as upstream fakes its timers.</summary>
    public static StepHost MakeHost(TimeProvider time, string[] paths, params SemanticNode[][] screens) => new(paths, screens, time);

    public static StepTraceSession MakeSession(CacheContext context, StepHost host, IReadOnlyDictionary<string, object?>? parameters = null, CacheEntry? step = null, int? stepIndex = null, string? appOrigin = null)
    {
        return new StepTraceSession(
            host,
            new StepCacheOptions
            {
                Cache = context.Store,
                Write = context.Write,
                Strict = context.Strict,
                ReplayEligible = context.ReplayEligible,
                Claim = new ClaimedKey(Key, step ?? ExampleStep),
                Instruction = "open billing",
                Params = parameters,
                Recorder = host.Recorder,
                StepIndex = stepIndex,
                AppOrigin = appOrigin,
            });
    }

    public static CacheContext FakeContext(Func<CacheLookup> read) => new(new StubCache(read));

    public static CacheContext NoEntry() => FakeContext(() => new CacheLookup { Reason = "no-entry" });

    /// <summary>A context whose store always hits with one navigate-opening entry.</summary>
    public static CacheContext EntryContext(
        string? startPath = null,
        string? endPath = null,
        RecordedTarget[]? endAnchors = null,
        RecordedTarget[]? goneAnchors = null,
        RecordedAction[]? actions = null,
        int? endWaitMs = null)
    {
        var entry = new CacheEntry
        {
            Schema = FileStepCache.SchemaVersion,
            Route = startPath,
            EndRoute = endPath,
            EndWaitMs = endWaitMs,
            Actions = [.. actions ?? [Navigate("/customers")]],
            Appeared = [.. endAnchors ?? []],
            Gone = [.. goneAnchors ?? []],
        };
        return FakeContext(() => new CacheLookup { Entry = entry });
    }

    public static RecordedAction Navigate(string url) => new() { Kind = "navigate", Url = url };

    public static RecordedAction Tap(string role, string name) => new() { Kind = "tap", Role = role, Name = name };

    public static SemanticNode Node(string reference, string role, string name, string? text = null) => new() { Ref = reference, Role = role, Name = name, Text = text };

    /// <summary>What a kind of change leaves in the cache after the attempt settles: upstream's <c>settleStagedTraces</c>.</summary>
    public static IReadOnlyDictionary<int, CacheWrite> Settle(CacheContext context, StepTraceSession session, bool verified, bool preserve = false)
    {
        session.Pending.Verified = verified && session.Pending.Completed;
        return E2ESession.Flush(context.Store, [session.Pending], preserve);
    }

    public static (string?, string?, string?) Anchor(RecordedTarget target) => (target.Role, target.Name, target.Text);
}

/// <summary>Upstream's <c>AgentCacheContext</c>, as far as the test needs to vary it.</summary>
internal sealed class CacheContext(IStepCache store)
{
    public IStepCache Store { get; } = store;

    public bool Write { get; init; } = true;

    public bool Strict { get; init; }

    public bool ReplayEligible { get; init; } = true;

    public CacheContext ReadOnly() => new(Store) { Write = false, Strict = Strict, ReplayEligible = ReplayEligible };

    public CacheContext Under(bool strict) => new(Store) { Write = !strict && Write, Strict = strict, ReplayEligible = ReplayEligible };

    public CacheContext Retry() => new(Store) { Write = Write, Strict = Strict, ReplayEligible = false };

    public int Reads => (Store as StubCache)?.Reads ?? 0;

    public List<string> Deleted => (Store as StubCache)?.Deleted ?? [];

    public List<string> Written => (Store as StubCache)?.Written ?? [];
}

internal sealed class StubCache(Func<CacheLookup> read) : IStepCache
{
    public int Reads { get; private set; }

    public List<string> Deleted { get; } = [];

    public List<string> Written { get; } = [];

    public CacheLookup Read(string key)
    {
        Reads++;
        return read();
    }

    public void Write(string key, CacheEntry entry) => Written.Add(key);

    public bool DeleteFails { get; set; }

    public void Delete(string key)
    {
        if (DeleteFails)
        {
            throw new InvalidOperationException("store unavailable");
        }

        Deleted.Add(key);
    }
}

/// <summary>A scripted host for one step: its looks, its grammar, and the actions the replay committed.</summary>
internal sealed class StepHost : IStepCacheHost
{
    private readonly Queue<string> _paths;
    private readonly Queue<SemanticNode[]> _screens;
    private string _path = "/";
    private SemanticNode[] _nodes = [];

    public StepHost(string[] paths, SemanticNode[][] screens, TimeProvider? time = null)
    {
        _paths = new Queue<string>(paths);
        _screens = new Queue<SemanticNode[]>(screens);
        Time = time ?? TimeProvider.System;
        Recorder = new(() => Redactor.None, Time);
        Actions = new Grammar(this);
    }

    public E2E.Cache.TraceRecorder Recorder { get; }

    /// <summary>Runs for each navigate the replay performs; a throw is the action's failure.</summary>
    public Action<string>? OnNavigate { get; set; }

    /// <summary>How far each look asked to settle, in order.</summary>
    public List<SettleMode> Looks { get; } = [];

    /// <summary>When set, every look is this one, whatever the script says.</summary>
    public Func<SettleMode, Observation>? Capture { get; set; }

    public IExecutorActions Actions { get; }

    public CancellationToken Signal => CancellationToken.None;

    public Redactor Redact { get; set; } = Redactor.None;

    /// <summary>Short enough that a missing anchor is not waited for: the end check takes one look.</summary>
    public TimeSpan ReplayTimeout { get; set; } = TimeSpan.Zero;

    /// <summary>What is left of the step's clock. Zero leaves no room to wait for a recording's end wait.</summary>
    public int Remaining { get; set; }

    public int RemainingMs() => Remaining;

    public TimeProvider Time { get; }

    public Observation? Latest { get; private set; }

    public Observation? FirstActedOn { get; private set; }

    public Task<Observation> ObserveAsync(SettleMode mode)
    {
        Looks.Add(mode);
        if (Capture is { } capture)
        {
            return Task.FromResult(Latest = capture(mode));
        }

        if (_paths.Count > 0)
        {
            _path = _paths.Dequeue();
        }

        if (_screens.Count > 0)
        {
            _nodes = _screens.Count > 1 ? _screens.Dequeue() : _screens.Peek();
        }

        return Task.FromResult(Latest = new Observation { Route = _path, Roots = _nodes });
    }

    public bool TryReserve() => true;

    public void MarkActing() => FirstActedOn ??= Latest;

    public Task CommitAsync(RecordedAction action)
    {
        Recorder.Record(action);
        return Task.CompletedTask;
    }

    private sealed class Grammar(StepHost host) : IExecutorActions
    {
        public Task TapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DoubleTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UploadAsync(ExecutorTarget target, IReadOnlyList<string> paths, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
        {
            host.OnNavigate?.Invoke(url);
            return Task.CompletedTask;
        }

        public Task BackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
