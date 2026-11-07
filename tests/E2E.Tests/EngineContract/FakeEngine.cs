// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.EngineContract;

/// <summary>One engine call as the fake engine saw it: the member, whether the caller's token was already cancelled, and what the call carried about itself.</summary>
internal sealed record EngineCall(string Method, bool Cancelled, OperationContext? Context, TimeSpan? Timeout);

/// <summary>
/// The tests' <c>createFakeEngine</c> (<c>tests/helpers/fake-engine.ts</c>): a document engine behind a recording session, with the hooks upstream's
/// fake takes (<c>onNavigate</c>, <c>onDispose</c>) and the counts its <c>stats()</c> keeps. Every call goes through <see cref="DispatchProxy"/>, so a
/// member the document engine overrides (and one it leaves to the default) is recorded as the harness called it.
/// </summary>
internal sealed class FakeEngine(IEngine inner, byte shade = 255) : IEngine
{
    private readonly List<EngineCall> _calls = [];
    private readonly byte _shade = shade;
    private int _started;
    private int _disposed;

    /// <summary>Runs before the engine opens a URL (upstream's <c>onNavigate</c>); throw to fail the navigation.</summary>
    public Action<string>? OnNavigate { get; init; }

    /// <summary>Runs when an attempt's engine session is disposed (upstream's <c>onEndAttempt</c>); throw to fail the release.</summary>
    public Action? OnDispose { get; init; }

    /// <summary>The attempt id of each start, in order.</summary>
    public List<string?> AttemptIds { get; } = [];

    /// <summary>The run id of each start, in order.</summary>
    public List<string?> RunIds { get; } = [];

    /// <summary>The engine calls so far, in order.</summary>
    public IReadOnlyList<EngineCall> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The engine sessions started (upstream's <c>attemptsStarted</c>).</summary>
    public int Started => Volatile.Read(ref _started);

    /// <summary>The engine sessions disposed (upstream's <c>attemptsEnded</c>).</summary>
    public int Disposed => Volatile.Read(ref _disposed);

    public string Name => inner.Name;

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities | EngineCapabilities.Screenshot;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        var session = await inner.StartAsync(options, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _started);
        lock (AttemptIds)
        {
            AttemptIds.Add(options.AttemptId);
            RunIds.Add(options.RunId);
        }

        var proxy = DispatchProxy.Create<IEngineSession, Recorder>();
        ((Recorder)(object)proxy).Attach(this, session, options.ArtifactsDir);
        return proxy;
    }

    internal void Record(EngineCall call)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }
    }

    internal void Disposing() => Interlocked.Increment(ref _disposed);

    public class Recorder : DispatchProxy
    {
        private FakeEngine? _engine;
        private IEngineSession? _session;
        private string? _artifactsDir;

        internal void Attach(FakeEngine engine, IEngineSession session, string? artifactsDir)
        {
            _engine = engine;
            _session = session;
            _artifactsDir = artifactsDir;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var engine = _engine!;
            var method = targetMethod!;
            var arguments = args ?? [];
            if (method.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                return Forward(method, arguments);
            }

            var label = method.Name == nameof(IEngineSession.ScreenshotArtifactAsync) ? (string?)arguments[0] : null;
            engine.Record(new EngineCall(
                label is null ? method.Name : $"{method.Name}({label})",
                arguments.OfType<CancellationToken>().FirstOrDefault().IsCancellationRequested,
                arguments.OfType<OperationContext>().FirstOrDefault(),
                arguments.OfType<TimeSpan>().Select(timeout => (TimeSpan?)timeout).FirstOrDefault()));

            try
            {
                switch (method.Name)
                {
                    case nameof(IEngineSession.OpenAsync):
                        engine.OnNavigate?.Invoke((string)arguments[0]!);
                        break;
                    case nameof(IAsyncDisposable.DisposeAsync):
                        // The hook runs around the real teardown, not instead of it (the document session releases synchronously).
                        engine.Disposing();
                        var released = Forward(method, arguments);
                        engine.OnDispose?.Invoke();
                        return released;
                    case nameof(IEngineSession.ScreenshotAsync):
                        return Task.FromResult(new EngineScreenshot(Agent.ScreenshotEngine.Png(engine._shade), 1, 0));
                    case nameof(IEngineSession.ScreenshotArtifactAsync):
                        return Task.FromResult(WriteScreenshot(label));
                    default:
                        break;
                }
            }
#pragma warning disable CA1031 // A hook's failure is the engine call's failure, as a rejected promise is upstream's.
            catch (Exception ex) when (method.ReturnType == typeof(Task))
#pragma warning restore CA1031
            {
                return Task.FromException(ex);
            }
#pragma warning disable CA1031 // As above, for a call that returns a value task.
            catch (Exception ex) when (method.ReturnType == typeof(ValueTask))
#pragma warning restore CA1031
            {
                return ValueTask.FromException(ex);
            }

            return Forward(method, arguments);
        }

        private string WriteScreenshot(string? label)
        {
            var relative = $"screenshots/{label ?? "screenshot"}.png";
            var absolute = Path.Combine(_artifactsDir!, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, Agent.ScreenshotEngine.Png(_engine!._shade));
            return relative;
        }

        private object? Forward(MethodInfo method, object?[] arguments)
        {
            try
            {
                return method.Invoke(_session, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerException);
                throw;
            }
        }
    }
}
