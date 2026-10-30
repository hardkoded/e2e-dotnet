// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Tests.ModelUsage;

/// <summary>
/// Upstream's <c>tests/unit/model-usage.test.ts</c> as of commit 2b3342df8936, <c>describe('model usage accounting')</c>, one method per <c>it</c>. The third <c>it</c> is partial: a
/// C# token count is a <c>long?</c>, so it cannot hold Infinity, and that case of the test is left out.
/// </summary>
public sealed class ModelUsageAccountingTests
{
    private static readonly StepModelInfo Provenance = new() { Provider = "test", Model = "test" };

    private static E2E.ModelUsage.Usage Usage(long? input = null, long? output = null, double? cost = null, TokenAccounting? accounting = null) =>
        new(input, output, EstimatedCostUsd: cost, Accounting: accounting);

    [Fact]
    public void Totals_complete_provider_reports_and_preserves_the_peak_per_call()
    {
        var usage = new E2E.ModelUsage();
        usage.Record(Usage(10, 3, 0.1));
        usage.Record(Usage(20, 7, 0.2));

        var report = usage.Report(Provenance, 2);

        Assert.Equal(TokenAccounting.Provider, report.TokenAccounting);
        Assert.Equal(30, report.InputTokens);
        Assert.Equal(10, report.OutputTokens);
        Assert.Equal(27, report.PeakTokensPerCall);
        Assert.Equal(0.3, report.EstimatedCostUsd!.Value, 9);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(1L, null, null)]
    [InlineData(1L, 2L, TokenAccounting.AdapterUpperBound)]
    public void Keeps_mixed_usage_non_authoritative_after_later_complete_reports(long? input, long? output, TokenAccounting? accounting)
    {
        var usage = new E2E.ModelUsage();
        usage.Record(Usage(input, output, accounting: accounting));
        usage.Record(Usage(10, 3));

        Assert.Equal(TokenAccounting.AdapterUpperBound, usage.Report(Provenance, 2).TokenAccounting);
    }

    [Fact]
    public void Partial_Marks_calls_without_returned_usage_as_incomplete_and_rejects_malformed_counters_and_cost()
    {
        var usage = new E2E.ModelUsage();
        usage.Record(Usage(10, 3));
        Assert.Equal(TokenAccounting.AdapterUpperBound, usage.Report(Provenance, 2).TokenAccounting);

        usage.Record(Usage(-1, null, double.NaN));

        var report = usage.Report(Provenance, 2);
        Assert.Equal(10, report.InputTokens);
        Assert.Equal(3, report.OutputTokens);
        Assert.Null(report.EstimatedCostUsd);
    }

    [Fact]
    public void Caps_overflowing_token_sums_and_preserves_non_authoritative_accounting()
    {
        var usage = new E2E.ModelUsage();
        const long maximum = E2E.ModelUsage.MaxSafeInteger;
        Assert.Equal(maximum, usage.Record(Usage(maximum, maximum)));
        usage.Record(Usage(2, 2));
        usage.Record(Usage(0, 0));

        var report = usage.Report(Provenance, 3);

        Assert.Equal(TokenAccounting.AdapterUpperBound, report.TokenAccounting);
        Assert.Equal(maximum, report.InputTokens);
        Assert.Equal(maximum, report.OutputTokens);
        Assert.Equal(maximum, report.PeakTokensPerCall);
    }

    [Fact]
    public void Omits_an_overflowing_cost_total_even_after_later_finite_costs_arrive()
    {
        var costs = new E2E.ModelUsage();
        costs.Record(Usage(1, 1, double.MaxValue));
        Assert.Equal(double.MaxValue, costs.Report(Provenance, 1).EstimatedCostUsd);
        costs.Record(Usage(1, 1, double.MaxValue));
        costs.Record(Usage(1, 1, 0.25));

        var capped = costs.Report(Provenance, 3);
        Assert.Null(capped.EstimatedCostUsd);
        Assert.Equal(3, capped.InputTokens);
        Assert.Equal(3, capped.OutputTokens);
    }
}
