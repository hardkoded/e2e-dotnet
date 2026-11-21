// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using E2E.Tests.SessionSecrecy;
using E2E.Tests.Traces;

namespace E2E.Tests.ReportJson;

/// <summary>
/// .NET-only: upstream builds <c>report.json</c> once, when its runner ends the run, and has no per-result cache. The port writes the file
/// after every attempt and keeps each result's redacted form, which must follow what the sessions beside it learn.
/// </summary>
[Collection(SharedSecretsCollection.Name)]
public sealed class ReportFileSharedSecretsTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-report-shared-").FullName;

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static Report1Document DocumentOf(string runId, string id, string message) =>
        new(
            new ReportRun(
                runId,
                "1.0.0",
                [new ReportTarget("web", 0, "web", BuildReport.DescribeTarget(null))],
                [
                    ReportBuilders.Result(
                        id: id,
                        attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error(message: message))]
                    ) with
                    {
                        Status = ResultStatus.Failed,
                    },
                ]
            )
        );

    [Fact]
    public async Task Redacts_a_cached_result_again_when_another_session_learns_a_secret()
    {
        var runId = Guid.CreateVersion7().ToString();
        var results = Path.Combine(_output, "results");
        var learned = false;
        string Redact(string text) =>
            learned ? text.Replace("hunter2", "<secret:password>", StringComparison.Ordinal) : text;

        await ReportFile.WriteAsync(
            DocumentOf(runId, new string('a', 64), "wrong password hunter2"),
            _output,
            results,
            Redact
        );

        learned = true;
        var withdraw = Secrecy.ShareStaticSecrets([Secret.Create("password", "hunter2")]);
        try
        {
            await ReportFile.WriteAsync(DocumentOf(runId, new string('b', 64), "other"), _output, results, Redact);
        }
        finally
        {
            withdraw();
        }

        var json = File.ReadAllText(Path.Combine(_output, "report.json"));
        Assert.DoesNotContain("hunter2", json, StringComparison.Ordinal);
        Assert.Contains("wrong password <secret:password>", json, StringComparison.Ordinal);
    }
}
