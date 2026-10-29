// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E;

/// <summary>
/// Upstream's <c>ModelUsage</c> (<c>agent/usage.ts</c>): the shared token and cost accounting for executor loops and judgment
/// calls. A provider total is authoritative only when every call reports both token counts. Token counts are the report's safe
/// integers (<see cref="MaxSafeInteger"/>), as in JavaScript.
/// </summary>
internal sealed class ModelUsage
{
    /// <summary>JavaScript's <c>Number.MAX_SAFE_INTEGER</c>, the largest count the report schema can represent.</summary>
    public const long MaxSafeInteger = 9_007_199_254_740_991;

    private int _records;
    private long _inputTokens;
    private long _outputTokens;
    private long _peakTokensPerCall;

    // Null until a provider reports the cache split; a run that never does omits the fields.
    private long? _cacheReadTokens;
    private long? _cacheWriteTokens;
    private TokenAccounting _accounting = TokenAccounting.Provider;

    // The cost total. Upstream's null (overflowed) and undefined (none reported) are the two flags below.
    private double _estimatedCostUsd;
    private bool _costSeen;
    private bool _costOverflowed;

    /// <summary>Accumulates one call, preserving incomplete or estimated provenance across later calls. Returns the call's tokens.</summary>
    public long Record(Usage usage = default)
    {
        _records++;
        var input = TokenCount(usage.InputTokens);
        var output = TokenCount(usage.OutputTokens);
        _inputTokens = AddTokens(_inputTokens, input ?? 0);
        _outputTokens = AddTokens(_outputTokens, output ?? 0);
        var tokens = AddTokens(input ?? 0, output ?? 0);
        _peakTokensPerCall = Math.Max(_peakTokensPerCall, tokens);
        if (TokenCount(usage.CacheReadTokens) is { } cacheRead)
        {
            _cacheReadTokens = AddTokens(_cacheReadTokens ?? 0, cacheRead);
        }

        if (TokenCount(usage.CacheWriteTokens) is { } cacheWrite)
        {
            _cacheWriteTokens = AddTokens(_cacheWriteTokens ?? 0, cacheWrite);
        }

        if (input is null || output is null || usage.Accounting == TokenAccounting.AdapterUpperBound)
        {
            _accounting = TokenAccounting.AdapterUpperBound;
        }

        if (!_costOverflowed && usage.EstimatedCostUsd is { } cost && double.IsFinite(cost) && cost >= 0)
        {
            var total = (_costSeen ? _estimatedCostUsd : 0) + cost;
            _costSeen = true;
            if (double.IsFinite(total))
            {
                _estimatedCostUsd = total;
            }
            else
            {
                _costOverflowed = true;
            }
        }

        return tokens;
    }

    // Saturates unrepresentable sums and marks the step's token counts non-authoritative.
    private long AddTokens(long left, long right)
    {
        var sum = left + right;
        if (sum <= MaxSafeInteger)
        {
            return sum;
        }

        _accounting = TokenAccounting.AdapterUpperBound;
        return MaxSafeInteger;
    }

    /// <summary>Projects the accumulated usage into the report, including calls that returned no usage.</summary>
    public StepModelInfo Report(StepModelInfo provenance, int calls) =>
        provenance with
        {
            Calls = calls,
            TokenAccounting = calls > _records ? TokenAccounting.AdapterUpperBound : _accounting,
            InputTokens = _inputTokens,
            OutputTokens = _outputTokens,
            PeakTokensPerCall = _peakTokensPerCall,
            CacheReadTokens = _cacheReadTokens,
            CacheWriteTokens = _cacheWriteTokens,
            EstimatedCostUsd = _costSeen && !_costOverflowed ? _estimatedCostUsd : null,
        };

    /// <summary>The per-call token split for a model event, each side present only when the provider reported it as a representable count (upstream's <c>tokenFields</c>).</summary>
    public static (long? InputTokens, long? OutputTokens) TokenFields(Usage usage = default) =>
        (TokenCount(usage.InputTokens), TokenCount(usage.OutputTokens));

    // Rejects counters that cannot be represented in the report schema.
    private static long? TokenCount(long? value) => value is >= 0 and <= MaxSafeInteger ? value : null;

    /// <summary>What one call reported (upstream's non-exported <c>Usage</c> interface); every field is optional.</summary>
    public readonly record struct Usage(
        long? InputTokens = null,
        long? OutputTokens = null,
        long? CacheReadTokens = null,
        long? CacheWriteTokens = null,
        TokenAccounting? Accounting = null,
        double? EstimatedCostUsd = null);
}
