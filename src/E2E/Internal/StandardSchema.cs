// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Internal;

/// <summary>Upstream's <c>internal/standard-schema.ts</c>: how an issue of a caller's schema reads in a message.</summary>
internal static class StandardSchema
{
    /// <summary>One issue as <c>users.0.email: Invalid email</c>, or the bare message for an issue at the root.</summary>
    public static string DescribeIssue(StandardSchemaV1Issue issue)
    {
        var fieldPath = string.Join('.', (issue.Path ?? []).Select(segment => Convert.ToString(segment, CultureInfo.InvariantCulture) ?? string.Empty));
        return fieldPath.Length == 0 ? issue.Message : $"{fieldPath}: {issue.Message}";
    }
}
