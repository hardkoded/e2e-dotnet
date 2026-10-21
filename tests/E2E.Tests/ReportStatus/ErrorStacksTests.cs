// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>Upstream's <c>describe('error stacks')</c> (<c>tests/unit/report-status.test.ts</c>).</summary>
public sealed class ErrorStacksTests
{
    // Partial: the port has no serial groups, so the stack of a serial member's error is not checked. Its SerializedError keeps no
    // stack at all, so the failure here is a thrown exception whose stack text names this file, and the test reads the document for it.
    private static string ThisFile([CallerFilePath] string path = "") => path;

    [Fact]
    public void Never_carries_a_stack_into_the_report_from_a_step_an_attempt_or_the_run()
    {
        var thisFile = ThisFile();
        Exception thrown;
        try
        {
            throw new TestException("ASSERTION_FAILED", "expected the dashboard");
        }
        catch (TestException ex)
        {
            thrown = ex;
        }

        var error = ErrorSerializer.Serialize(thrown, Path.GetDirectoryName(thisFile));
        var withStack = ReportError.Of(error);
        var plain = FailedResult(withStack, "plain");
        var step = new StepRecord
        {
            Id = Guid.CreateVersion7().ToString() + ":0",
            Index = 0,
            Kind = StepKind.Assertion,
            Api = "expect.toBe",
            Label = "toBe",
            Status = StepStatus.Failed,
            StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DurationMs = 1,
            Error = error,
        };
        var result = plain with { Attempts = [plain.Attempts[0] with { SecondaryErrors = [withStack], Steps = [step] }] };

        var document = Build([result], ReportRunStatus.Error, 3, [ReportError.Of(error, "report")]);
        var json = JsonSerializer.Serialize(ReportWire.Document(document, "results"));

        Assert.Contains("expected the dashboard", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"stack\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetDirectoryName(thisFile)!.Replace("\\", "\\\\", StringComparison.Ordinal), json, StringComparison.Ordinal);
    }
}
