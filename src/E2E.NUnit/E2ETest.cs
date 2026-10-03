// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using NUnit.Framework;

namespace E2E.NUnit;

/// <summary>
/// Base class for an NUnit test that drives the app. Setup starts one engine
/// session. Teardown commits the replay cache from the NUnit result: a pass
/// records verified acts, a failure deletes unverified ones, and a skip or a
/// cancelled test leaves the cache alone. The first attempt can replay. Retries
/// run live.
/// <para>
/// Settings come from <see cref="Config"/>, the nearest <c>e2e.config.json</c> above the
/// test assembly or the working directory. Override a property to change one value for a fixture.
/// </para>
/// </summary>
public abstract class E2ETest
{
    private static readonly Lazy<E2EConfig> DiscoveredConfig = new(() => E2EConfig.Discover(), LazyThreadSafetyMode.ExecutionAndPublication);

    private E2ESession? _session;

    protected App App => Session.App;

    protected Agent Agent => Session.Agent;

    protected Screen Screen => Session.Screen;

    protected E2E.TestContext Context => Session.Context;

    /// <summary>The discovered <c>e2e.config.json</c>, or the defaults when there is none. Loaded once per test run.</summary>
    protected virtual E2EConfig Config => DiscoveredConfig.Value;

    /// <summary>Secrets declared in the config, with <c>E2E_SECRET_{NAME}</c> overrides.</summary>
    protected Secrets Secrets => Config.Secrets;

    /// <summary>Browser engine. Override to supply a <see cref="DocumentEngine"/> or a custom engine.</summary>
    protected virtual IEngine CreateEngine() => new WebEngine();

    /// <summary>The model for <c>agents.default</c>, or null when the config sets no model.</summary>
    protected virtual IAgentModel? CreateModel() => Config.Agent.CreateModel();

    /// <summary><c>targets[0].app.url</c>.</summary>
    protected virtual string? BaseUrl => Config.Target.App.Url;

    /// <summary><c>cache.dir</c>, resolved against the config directory.</summary>
    protected virtual string CacheDirectory => Config.Cache.Directory;

    /// <summary><c>cache.mode</c>. Unset, it is read-write locally and read-only in CI.</summary>
    protected virtual CacheMode CacheMode => Config.Cache.Mode;

    /// <summary><c>cache.strict</c>.</summary>
    protected virtual bool CacheStrict => Config.Cache.Strict;

    protected virtual TimeSpan TestTimeout => Config.Timeout;

    protected virtual TimeSpan LaunchTimeout => Config.LaunchTimeout;

    protected virtual TimeSpan ActionTimeout => Config.ActionTimeout;

    protected virtual TimeSpan AssertionTimeout => Config.AssertionTimeout;

    protected virtual TimeSpan CleanupTimeout => Config.CleanupTimeout;

    protected virtual TimeSpan StepTimeout => TimeSpan.FromSeconds(30);

    /// <summary><c>agents.default.maxModelCalls</c>.</summary>
    protected virtual int MaxModelCalls => Config.Agent.MaxModelCalls;

    /// <summary>Cache identity for this test. The default is the NUnit full name, so each test keeps its own replay.</summary>
    protected virtual string CacheTitle(global::NUnit.Framework.TestContext.TestAdapter test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return string.IsNullOrWhiteSpace(test.FullName) ? test.Name : test.FullName;
    }

    private E2ESession Session => _session ?? throw new InvalidOperationException("The E2E session is available after setup, during the test.");

    [SetUp]
    public async Task StartE2ESessionAsync()
    {
        var current = global::NUnit.Framework.TestContext.CurrentContext;
        var attempt = current.CurrentRepeatCount + 1;
        var cacheMode = CacheMode;
        var engine = CreateEngine();
        var title = CacheTitle(current.Test);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("CacheTitle returned an empty test name.");
        }

        _session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = engine,
                Model = CreateModel(),
                BaseUrl = BaseUrl,
                Cache = cacheMode == CacheMode.Off ? null : new FileStepCache(CacheDirectory),
                CacheEnabled = cacheMode != CacheMode.Off && attempt == 1,
                CacheMode = cacheMode,
                CacheStrict = CacheStrict,
                TestTitle = title,
                TestTimeout = TestTimeout,
                LaunchTimeout = LaunchTimeout,
                CleanupTimeout = CleanupTimeout,
                ActionTimeout = ActionTimeout,
                AssertionTimeout = AssertionTimeout,
                StepTimeout = StepTimeout,
                MaxModelCalls = MaxModelCalls,
                Attempt = attempt,
            },
            current.CancellationToken).ConfigureAwait(false);
    }

    [TearDown]
    public async Task FinishE2ESessionAsync()
    {
        var session = _session;
        _session = null;
        if (session is null)
        {
            return;
        }

        var current = global::NUnit.Framework.TestContext.CurrentContext;
        try
        {
            session.Complete(ErrorForCache(current.Result), current.CancellationToken);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static Exception? ErrorForCache(global::NUnit.Framework.TestContext.ResultAdapter result)
    {
        var recorded = result.RecordedException;
        return result.Outcome.Status switch
        {
            global::NUnit.Framework.Interfaces.TestStatus.Passed or global::NUnit.Framework.Interfaces.TestStatus.Warning => null,
            global::NUnit.Framework.Interfaces.TestStatus.Skipped => recorded as SkipException ?? new SkipException(string.IsNullOrWhiteSpace(result.Message) ? "skipped" : result.Message),
            _ => recorded ?? new TestException("TEST_FAILED", string.IsNullOrWhiteSpace(result.Message) ? "The test failed." : result.Message),
        };
    }
}
