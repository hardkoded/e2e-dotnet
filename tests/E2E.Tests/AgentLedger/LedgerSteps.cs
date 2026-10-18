// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentLedger;

/// <summary>Upstream's <c>step()</c> helper of <c>agent-ledger.test.ts</c>: a passed agent step with the fields a ledger reads.</summary>
internal static class LedgerSteps
{
    public static StepRecord Step(string api, string label, StepKind kind = StepKind.Agent, StepStatus status = StepStatus.Passed, string? explanation = null, CacheInfo? cache = null) =>
        new()
        {
            Id = "a:0",
            Index = 0,
            Kind = kind,
            Api = api,
            Label = label,
            Status = status,
            StartedAt = DateTimeOffset.Parse("2026-01-01T00:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture),
            DurationMs = 1,
            Explanation = explanation,
            Cache = cache,
        };
}
