// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The names an agent step's engine events carry for what the dispatcher did (upstream <c>agent/action-names.ts</c>).</summary>
internal static class ActionNames
{
    /// <summary>The prefix of the engine event a project's own tool records; its name is the project's.</summary>
    public const string ProjectToolEventPrefix = "tool:";
}
