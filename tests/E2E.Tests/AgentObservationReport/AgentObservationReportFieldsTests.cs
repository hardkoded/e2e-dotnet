// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.ReportJson;

namespace E2E.Tests.AgentObservationReport;

/// <summary>
/// Upstream's <c>describe('agent observation report fields')</c> (<c>tests/integration/agent-observation-report.test.ts</c>):
/// agent steps report when any semantic observation was truncated. The port has no fixture-engine <c>runProject</c>, so
/// each test runs its own session over an engine that marks every observation as cut, as a large page would be, and reads
/// <c>report.json</c> after the session is disposed. Upstream runs both titled tests in one project and validates that run's
/// report; here each test validates its own session's report, and the last one runs both calls in one session.
/// </summary>
public sealed class AgentObservationReportFieldsTests
{
    [Theory]
    [InlineData("reports a truncated act observation", "agent.act")]
    [InlineData("reports a truncated judgment observation", "agent.assert")]
    public async Task Partial_Marks_a_truncated_observation(string title, string api)
    {
        using var report = await RunAsync(title, api == "agent.act" ? InspectAsync : JudgeAsync);

        Assert.Equal("passed", (string?)report.LastAttempt()["status"]);
        Assert.True((bool?)report.Step(api)["observationTruncated"]);
    }

    [Fact]
    public async Task Partial_Writes_a_valid_report_1_document()
    {
        using var report = await RunAsync("reports a truncated observation", async session =>
        {
            await InspectAsync(session);
            await JudgeAsync(session);
        });

        Assert.NotNull(report.Read());
    }

    private static Task InspectAsync(E2ESession session) => session.Context.Agent.ActAsync("inspect the visible result");

    private static Task JudgeAsync(E2ESession session) => session.Context.Agent.AssertAsync("the Save button is visible");

    private static async Task<ReportDirectory> RunAsync(string title, Func<E2ESession, Task> calls)
    {
        var report = new ReportDirectory();
        try
        {
            var model = new ScriptedModel(request => request.Messages[0].Content?.StartsWith("Statement:", StringComparison.Ordinal) == true
                ? ModelResponses.Done("passed", "the Save button is visible")
                : ModelResponses.Done("passed", "the result is visible"));
            var engine = new TruncatedEngine(new DocumentEngine(new DocumentWorld().Map("/result", page => page.Paragraph("Save"))));
            await using (var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = engine,
                Model = model,
                BaseUrl = "https://fixture.test",
                CacheEnabled = false,
                ResultsDirectory = report.Results,
                TestTitle = title,
            }))
            {
                await session.Context.App.OpenAsync("/result");
                await calls(session);
                session.Complete();
            }

            return report;
        }
        catch
        {
            report.Dispose();
            throw;
        }
    }

    /// <summary>An engine over another that marks every observation as stopped at a limit.</summary>
    private sealed class TruncatedEngine(IEngine inner) : IEngine
    {
        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(IEngineSession session) : IEngineSession
        {
            public string Route => session.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

            public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            {
                var observation = await session.ObserveAsync(cancellationToken).ConfigureAwait(false);
                return new Observation { Route = observation.Route, Roots = observation.Roots, Truncated = true };
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => session.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

            public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => session.SwipeAsync(direction, cancellationToken);

            public Task BackAsync(CancellationToken cancellationToken) => session.BackAsync(cancellationToken);

            public Task RestartAsync(CancellationToken cancellationToken) => session.RestartAsync(cancellationToken);

            public Task ClearStateAsync(CancellationToken cancellationToken) => session.ClearStateAsync(cancellationToken);

            public ValueTask DisposeAsync() => session.DisposeAsync();
        }
    }
}
