// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>filterIncludedContext</c> (<c>telemetry/filter-included-context.ts</c>).</summary>
internal static class FilterIncludedContext
{
    private static readonly IReadOnlyDictionary<string, object?> NoContext = new Dictionary<string, object?>();

    /// <summary>Returns a shallow copy of the runtime context with only the top-level properties marked for telemetry inclusion.</summary>
    internal static IReadOnlyDictionary<string, object?> Filter(
        IReadOnlyDictionary<string, object?>? context,
        IReadOnlyDictionary<string, bool>? includeContext
    )
    {
        if (context is null)
        {
            return NoContext;
        }

        return context
            .Where(pair =>
                includeContext is not null && includeContext.TryGetValue(pair.Key, out var included) && included
            )
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
