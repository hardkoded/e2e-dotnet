// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>
/// What the report keeps for an attempt marked expected to fail (upstream's <c>ExpectedFailureInfo</c>, <c>internal/expected-failure.ts</c>).
/// </summary>
/// <param name="Reason">Why the test is expected to fail.</param>
/// <param name="Error">The failure the mark covered; null when the attempt passed, or failed with an error the mark does not cover.</param>
internal sealed record ExpectedFailureInfo(string Reason, ReportError? Error = null);

/// <summary>
/// Expecting a test to fail: <c>test.fail(...)</c> and the <c>fail</c> option (upstream's <c>internal/expected-failure.ts</c>). The verdict
/// it leads to is <see cref="E2E.ExpectedFailure.SettleExpectedFailure"/>.
/// </summary>
internal static class ExpectedFailureMarks
{
    /// <summary>The reason recorded for a mark that gave none.</summary>
    internal const string DefaultReason = "expected to fail";

    /// <summary>Why a test cannot be marked from its teardown.</summary>
    private const string Late = "a test from an afterEach hook or a fixture teardown: its verdict is already reached";

    /// <summary>A blank or missing reason becomes the default one (upstream's <c>expectedFailureReason</c>).</summary>
    internal static string ExpectedFailureReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason;

    /// <summary>The reason a <c>fail</c> layer gives, or null when it marks nothing (upstream's <c>expectedFailureOption</c>); a blank reason marks with the default one.</summary>
    internal static string? ExpectedFailureOption(string? fail) => fail is null ? null : ExpectedFailureReason(fail);

    /// <summary>
    /// The reason a test is expected to fail from its layers, outermost group first and the test last (upstream's <c>fail</c> merge in <c>resolveOptions</c>,
    /// <c>collect/select.ts</c>): any layer that marks marks the test, the innermost reason wins, and a mark with no reason (a blank string) keeps an outer layer's.
    /// A layer that gives no <c>fail</c> does not unmark.
    /// </summary>
    internal static string? ResolveExpectedFailure(IEnumerable<string?> layers)
    {
        string? fail = null;
        foreach (var layer in layers)
        {
            if (layer is not null && !string.IsNullOrWhiteSpace(layer))
            {
                fail = layer;
            }
            else if (ExpectedFailureOption(layer) is not null && fail is null)
            {
                fail = DefaultReason;
            }
        }

        return ExpectedFailureOption(fail);
    }

    /// <summary>
    /// Marks the running test expected to fail when <paramref name="condition"/> holds (upstream's <c>markRunningTestExpectedToFail</c>).
    /// Unlike a skip it does not throw: the body runs on. Outside a body there is no running test. A setup test, a serial group member, and
    /// a test whose verdict is already reached (its teardown) publish no way to mark, and the call is a test error there.
    /// </summary>
    internal static void MarkRunningTestExpectedToFail(
        E2ESession? session,
        bool condition,
        string? reason,
        bool serialMember,
        bool late
    )
    {
        if (session is null)
        {
            throw new InvalidOperationException(
                "Fail(condition, reason) marks the running test and must be called inside a test body; to mark at collection, put [Property(\"E2E.Fail\", reason)] on the test or its fixture"
            );
        }

        if (!condition)
        {
            return;
        }

        if (late)
        {
            throw LateExpectedFailureError();
        }

        if (session.IsSetup)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "Fail() cannot mark a setup test: the sessions it declares are owed to their consumers"
            );
        }

        if (serialMember)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "Fail() cannot mark a serial group member: the group passes or fails as one unit"
            );
        }

        session.ExpectedFailure = ExpectedFailureReason(reason);
    }

    /// <summary>The error for a mark made once the verdict is reached, from a teardown (upstream's <c>lateExpectedFailureError</c>).</summary>
    internal static TestException LateExpectedFailureError() => new("INVALID_ARGUMENT", $"Fail() cannot mark {Late}");
}
