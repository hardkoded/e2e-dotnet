// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>normalizeBatchRequestCounts</c> (<c>provider-utils/src/normalize-batch-request-counts.ts</c>).</summary>
public static class NormalizeBatchRequestCounts
{
    /// <summary>
    /// Normalizes complete batch request counts. Returns null when any count is missing, is not a non-negative safe integer, or when the item
    /// counts do not add up to the total.
    /// </summary>
    public static BatchV4RequestCounts? Normalize(double? total, double? pending, double? completed, double? failed)
    {
        if (
            IsNonNegativeSafeInteger(total)
            && IsNonNegativeSafeInteger(pending)
            && IsNonNegativeSafeInteger(completed)
            && IsNonNegativeSafeInteger(failed)
            && pending + completed + failed == total
        )
        {
            return new BatchV4RequestCounts(
                (long)total!.Value,
                (long)pending!.Value,
                (long)completed!.Value,
                (long)failed!.Value
            );
        }

        return null;
    }

    // Number.isSafeInteger(value) && value >= 0
    private static bool IsNonNegativeSafeInteger(double? value) =>
        value is >= 0 and <= 9007199254740991 && Math.Floor(value.Value) == value.Value;
}
