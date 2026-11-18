// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Decision;

/// <summary>Upstream's <c>api-key.ts</c>.</summary>
internal static class ApiKey
{
    private static readonly Regex VariableName = new(
        @"\bthe ([A-Z][A-Z0-9_]*) environment variable\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2)
    );

    /// <summary>
    /// Upstream's <c>missingKey</c>: <c>MODEL_UNAVAILABLE</c> for a missing API key, naming the environment variable from the SDK's own
    /// message when it names one. Only that identifier is kept, never the rest of the text. <paramref name="role"/> is
    /// <c>decision</c> or <c>field-text</c>.
    /// </summary>
    public static AgentException MissingKey(string role, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var variable = VariableName.Match(error.Message) is { Success: true } match ? match.Groups[1].Value : null;
        var message = variable is null
            ? "Set the " + role + " model API key."
            : "Set " + variable + " to the " + role + " model API key.";
        return new AgentException("MODEL_UNAVAILABLE", message);
    }
}
