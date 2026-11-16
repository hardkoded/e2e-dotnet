// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The names an agent step's engine events carry for what the dispatcher did (upstream <c>agent/action-names.ts</c>).</summary>
internal static class ActionNames
{
    /// <summary>Assertion tool names and the dispatcher actions each may perform (upstream's <c>READ_ONLY_GRAMMAR</c>).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ReadOnlyGrammar = new Dictionary<string, string[]>(
        StringComparer.Ordinal
    )
    {
        ["observe"] = [],
        ["screenshot"] = [],
        ["scroll"] = ["scroll", "scrollUntil"],
        ["scroll_to"] = ["scrollIntoView"],
    };

    /// <summary>The prefix of the engine event a project's own tool records; its name is the project's.</summary>
    public const string ProjectToolEventPrefix = "tool:";

    /// <summary>
    /// Every name the grammar may hand out (upstream's <c>GRAMMAR_TOOL_NAMES</c>). A project tool cannot take one.
    /// </summary>
    public static readonly HashSet<string> GrammarToolNames = new(
        [
            "observe",
            "tap",
            "double_tap",
            "long_press",
            "right_click",
            "hover",
            "type",
            "type_secret",
            "press",
            "select",
            "check",
            "scroll",
            "scroll_to",
            "drag",
            "upload",
            "navigate",
            "back",
            "resize_viewport",
            "switch_tab",
            "close_tab",
            "screenshot",
            "tap_at",
            "hover_at",
            "drag_at",
            "type_at",
            "press_at",
            "select_at",
            "dismiss_keyboard",
        ],
        StringComparer.Ordinal
    );
}
