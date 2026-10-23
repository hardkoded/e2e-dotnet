// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.FailureEvidence;
using E2E.Tests.ReportJson;

namespace E2E.Tests.AgentObservationReport;

/// <summary>
/// Upstream's <c>agent-observation-report.test.ts</c>: agent steps report when any semantic observation was truncated. The port runs
/// the two steps in one session over an engine that marks its observation as cut, and reads <c>report.json</c> after the session is
/// disposed. Upstream's <c>it.each</c> becomes one test method for each row, as the port's other tests do.
/// </summary>
public sealed class AgentObservationReportFieldsTests : IAsyncLifetime
{
    private readonly ReportDirectory _report = new();

    public async Task InitializeAsync()
    {
        var model = new ScriptedModel(request =>
            request.Messages[0].Content?.StartsWith("Statement:", StringComparison.Ordinal) == true
                ? ModelResponses.Done("passed", "the Save button is visible")
                : ModelResponses.Done("passed", "the result is visible"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new EvidenceEngine("Save") { Truncated = true },
            Model = model,
            BaseUrl = "https://fixture.test",
            CacheEnabled = false,
            ResultsDirectory = _report.Results,
            TestTitle = "reports a truncated observation",
        });
        await session.Context.Agent.ActAsync("inspect the visible result");
        await session.Context.Agent.AssertAsync("the Save button is visible");
        session.Complete(null);
    }

    public Task DisposeAsync()
    {
        _report.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Marks_reports_a_truncated_act_observation()
    {
        Assert.Equal("passed", (string?)_report.LastAttempt()["status"]);
        Assert.True((bool?)_report.Step("agent.act")["observationTruncated"]);
    }

    [Fact]
    public void Marks_reports_a_truncated_judgment_observation()
    {
        Assert.Equal("passed", (string?)_report.LastAttempt()["status"]);
        Assert.True((bool?)_report.Step("agent.assert")["observationTruncated"]);
    }

    [Fact]
    public void Writes_a_valid_report_1_document() => ReportSchema.AssertValidReport(_report.Read());
}
