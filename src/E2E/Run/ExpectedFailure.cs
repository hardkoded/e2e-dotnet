// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>The verdict-bearing slice of an attempt record (upstream's <c>MarkableAttempt</c>, <c>run/expected-failure.ts</c>).</summary>
internal sealed class MarkableAttempt
{
    public AttemptStatus Status { get; set; }

    public ReportError? Error { get; set; }

    public ReportSkip? Skip { get; set; }

    public IReadOnlyList<ReportError> SecondaryErrors { get; init; } = [];

    public ExpectedFailureInfo? ExpectedFailure { get; set; }
}

/// <summary>How an attempt's errors and reason are made safe for the report (upstream's <c>ExpectedFailureSerializer</c>).</summary>
/// <param name="Serialize">Turns the error into the report's shape.</param>
/// <param name="Redact">Hides secrets in a text.</param>
internal sealed record ExpectedFailureSerializer(
    Func<TestException, ReportError> Serialize,
    Func<string, string> Redact
);

/// <summary>
/// The verdict of an attempt marked <c>test.fail</c> (upstream's <c>run/expected-failure.ts</c>). A marked attempt that fails with a
/// product failure passes, keeping the error beside the reason; one that passes fails with <c>UNEXPECTED_PASS</c>. Nothing else is
/// covered: an infrastructure, configuration, or internal error, an interrupt, a blocked agent verdict, a suite hook failure, or a
/// test that misuses the API says nothing about the app, and hiding it would hide a crashed browser, a broken run, or a broken test.
/// </summary>
internal static class ExpectedFailure
{
    /// <summary>Quotes like <c>JSON.stringify</c>: non-ASCII text stays literal.</summary>
    private static readonly JsonSerializerOptions Quoting = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The report schema's bound on a reason.</summary>
    private const int MaxReasonBytes = 8192;

    /// <summary>Test-category codes that say the test is broken, not the app.</summary>
    private static readonly HashSet<string> AuthoringCodes =
    [
        "INVALID_ARGUMENT",
        "INVALID_LOCATOR",
        "UNSUPPORTED_CAPABILITY",
        "STEP_NOT_AWAITED",
        "UNEXPECTED_PASS",
    ];

    /// <summary>
    /// Applies a mark to a finished attempt once its status is classified, before its recordings are kept or dropped, so they follow
    /// the verdict. A skip wins and drops the mark, also beside a failure. A pass becomes <c>UNEXPECTED_PASS</c>. A product failure or
    /// timeout becomes a pass, its error moved into <see cref="MarkableAttempt.ExpectedFailure"/>, unless an error that is not one
    /// landed beside it outside cleanup. Anything else keeps its verdict and records the mark.
    /// </summary>
    public static void SettleExpectedFailure(MarkableAttempt record, string? mark, ExpectedFailureSerializer serializer)
    {
        if (mark is null || record.Status == AttemptStatus.Skipped || record.Skip is not null)
        {
            return;
        }

        var reason = TextRules.TruncateUtf8(TextRules.SanitizeRecordedText(serializer.Redact(mark)), MaxReasonBytes);
        if (record.Status == AttemptStatus.Passed)
        {
            record.Status = AttemptStatus.Failed;
            record.Error = serializer.Serialize(
                new TestException(
                    "UNEXPECTED_PASS",
                    $"test is marked test.fail ({JsonSerializer.Serialize(reason, Quoting)}) but passed; remove the mark if it is fixed"
                )
            );
            record.ExpectedFailure = new ExpectedFailureInfo(reason);
            return;
        }

        var error = record.Error;
        var covered =
            record.Status is AttemptStatus.Failed or AttemptStatus.TimedOut
            && error is not null
            && IsProductFailure(error)
            && record.SecondaryErrors.All(secondary => secondary.Phase == "cleanup" || IsProductFailure(secondary));
        if (covered)
        {
            record.Status = AttemptStatus.Passed;
            record.Error = null;
            record.ExpectedFailure = new ExpectedFailureInfo(reason, error);
            return;
        }

        record.ExpectedFailure = new ExpectedFailureInfo(reason);
    }

    /// <summary>Whether an error is a product failure of the test itself: test-category, not blocked, not an authoring error, not a suite hook.</summary>
    private static bool IsProductFailure(ReportError error) =>
        error.Category == "test"
        && !StepExecutors.BlockableCodes.Contains(error.Code)
        && !AuthoringCodes.Contains(error.Code)
        && error.Phase is not ("beforeAll" or "afterAll");
}
