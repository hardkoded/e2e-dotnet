// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepScreens;

internal static class ScreenKit
{
    /// <summary>A screen from listing-like lines, <c>#id description</c> each; the description is what the trace compares.</summary>
    public static ScreenText Screen(params string[] lines) =>
        new(
            "http://app.test/todos",
            [.. lines.SelectMany(line => line.Split('\n')).Select(line =>
            {
                var parts = line.Trim().Split(' ', 2);
                return new ScreenLine(parts[0][1..], parts.Length > 1 ? parts[1] : "");
            })]);
}
