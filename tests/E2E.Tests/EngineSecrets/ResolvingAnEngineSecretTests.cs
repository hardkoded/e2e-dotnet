// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.EngineContract;

namespace E2E.Tests.EngineSecrets;

/// <summary>
/// Ported from upstream's <c>engine-secrets.test.ts</c>, <c>describe('resolving an engine secret')</c>. Partial: upstream runs a project of two
/// tests through <c>runProject</c> and reads the run's results; the port starts one session per attempt (the first asks for the declared secret,
/// the second for the undeclared one) and reads the error the second start throws, since the port has no runner to collect it as an attempt's
/// error. The fake engine's <c>onStartAttempt</c> is the engine's <c>StartAsync</c>, which asks <see cref="EngineStartOptions.ResolveSecret"/>
/// (the port's <c>context.resolveSecret</c>).
/// </summary>
public sealed class ResolvingAnEngineSecretTests
{
    [Fact]
    public async Task Partial_Hands_the_engine_a_declared_value_and_refuses_one_it_did_not_declare()
    {
        var declared = Secret.Create("declared", "declared-value");
        var undeclared = Secret.Create("undeclared", "undeclared-value");
        var resolved = new List<string>();
        var engine = new AskingEngine(new DocumentEngine(BillingWorld.Create()), [declared], async (options, attemptIndex) =>
            resolved.Add(await options.ResolveSecret!(attemptIndex == 0 ? declared : undeclared, null)));

        await using (await StartAsync(engine, "first"))
        {
        }

        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => StartAsync(engine, "second"));

        Assert.Equal(["declared-value"], resolved);
        Assert.Equal("SECRET_UNAVAILABLE", refused.Code);
        Assert.Contains("asked for secret \"undeclared\", which it did not declare in its secrets", refused.Message, StringComparison.Ordinal);
    }

    private static Task<E2ESession> StartAsync(IEngine engine, string title) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            BaseUrl = "https://billing.test",
            TestTitle = title,
        });

    /// <summary>A document engine that declares secrets and asks for one when an attempt starts (upstream's fake engine with <c>secrets</c> and <c>onStartAttempt</c>).</summary>
    private sealed class AskingEngine(IEngine inner, IReadOnlyList<Secret> secrets, Func<EngineStartOptions, int, Task> onStartAttempt) : IEngine
    {
        private int _started = -1;

        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public IReadOnlyList<Secret> Secrets => secrets;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            await onStartAttempt(options, Interlocked.Increment(ref _started)).ConfigureAwait(false);
            return await inner.StartAsync(options, cancellationToken).ConfigureAwait(false);
        }
    }
}
