// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The text the agent's prompts share. Upstream's <c>agent/prompts.ts</c>.</summary>
internal static class Prompts
{
    /// <summary>
    /// The line naming the test a step runs in, <c>Test: billing &gt; upgrades the tier</c>, or null outside one. It goes in the user
    /// message, never the system message, so the prefix a provider's prompt cache serves stays the same across tests. Upstream's
    /// <c>testTitleLine</c>. Upstream puts it after the fixed request text of the user message; the port keeps that text in the judgment's
    /// system message, so the title opens the user message.
    /// </summary>
    internal static string? TestTitleLine(IReadOnlyList<string> titlePath)
    {
        return titlePath.Count == 0 ? null : "Test: " + string.Join(" > ", titlePath);
    }
}
