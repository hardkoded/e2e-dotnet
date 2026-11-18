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
