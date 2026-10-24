// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The text the agent's prompts share. Upstream's <c>agent/prompts.ts</c>.</summary>
internal static class Prompts
{
    /// <summary>
    /// The line naming the test a step runs in, <c>Test: billing &gt; upgrades the tier</c>, or null outside one. It opens the user
    /// message, never the system message: the system message stays the same across tests, so a provider's prompt cache serves
    /// every test of a run. Upstream's <c>testTitleLine</c>.
    /// </summary>
    internal static string? TestTitleLine(IReadOnlyList<string> titlePath)
    {
        return titlePath.Count == 0 ? null : "Test: " + string.Join(" > ", titlePath);
    }
}
