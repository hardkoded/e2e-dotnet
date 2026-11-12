// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace E2E.Internal;

/// <summary>Upstream's <c>JsonValueRules</c> (<c>internal/json-value.ts</c>): what the JSON-safety walk refuses beyond the defaults.</summary>
public sealed record JsonValueRules
{
    /// <summary>
    /// Raised on a <see cref="Secret"/> value, with the label of the value. The default throws <c>POLICY_DENIED</c>.
    /// </summary>
    public Action<string>? OnSecret { get; init; }

    /// <summary>Maximum object or array nesting depth; unlimited when unset.</summary>
    public int? MaxDepth { get; init; }
}

/// <summary>
/// The one JSON-safety walker (upstream's <c>validateJsonValue</c> in <c>internal/json-value.ts</c>). Anything crossing a boundary that demands
/// plain JSON validates here, so cycle detection, plain-container checks, and secret rejection cannot drift between call sites. .NET-only
/// holder name: C# has no free functions.
/// </summary>
public static class JsonValueValidation
{
    /// <summary>
    /// Validates that <paramref name="value"/> is JSON-safe, throwing a labeled error otherwise: a non-finite number, a cycle, a container
    /// that is not a plain dictionary or list, a value of any other type, or a <see cref="Secret"/>.
    /// </summary>
    public static void ValidateJsonValue(object? value, string label, JsonValueRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (value is null)
        {
            return;
        }

        var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(value, label, rules ?? new JsonValueRules(), ancestors, depth: 0);
    }

    private static void Visit(object item, string label, JsonValueRules rules, HashSet<object> ancestors, int depth)
    {
        if (rules.MaxDepth is { } maxDepth && depth > maxDepth)
        {
            throw new TestException("INVALID_ARGUMENT", label + " exceeds " + maxDepth + " levels of nesting");
        }

        switch (item)
        {
            case string or bool:
                return;
            case double number when !double.IsFinite(number):
            case float single when !float.IsFinite(single):
                throw new TestException("INVALID_ARGUMENT", label + " contains a non-finite number");
            case double or float or decimal or int or long or short or byte or sbyte or ushort or uint or ulong:
                return;
            case Secret:
                if (rules.OnSecret is { } onSecret)
                {
                    onSecret(label);
                }

                throw new ConfigurationException("POLICY_DENIED", label + " must not contain a Secret");
        }

        IEnumerable<object?>? entries = item switch
        {
            System.Text.Json.Nodes.JsonObject json => json.Select(pair => (object?)pair.Value),
            System.Text.Json.Nodes.JsonArray array => array.Select(node => (object?)node),
            IDictionary dictionary => dictionary.Values.Cast<object?>(),
            IList list => list.Cast<object?>(),
            System.Text.Json.Nodes.JsonNode => [],
            _ => null,
        };
        if (entries is null)
        {
            throw new TestException("INVALID_ARGUMENT", label + " must be JSON-safe, found " + item.GetType().Name);
        }

        if (item is System.Text.Json.Nodes.JsonNode and not System.Text.Json.Nodes.JsonObject and not System.Text.Json.Nodes.JsonArray)
        {
            return;
        }

        if (!ancestors.Add(item))
        {
            throw new TestException("INVALID_ARGUMENT", label + " contains a cycle");
        }

        foreach (var entry in entries)
        {
            if (entry is not null)
            {
                Visit(entry, label, rules, ancestors, depth + 1);
            }
        }

        ancestors.Remove(item);
    }
}
