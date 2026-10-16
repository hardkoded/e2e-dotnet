// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Kernel;

/// <summary>Reads the run's environment (upstream <c>env.ts</c>).</summary>
internal static class KernelEnvironment
{
    /// <summary>A non-empty variable from the run's environment, trimmed, or null.</summary>
    public static string? EnvValue(IReadOnlyDictionary<string, string?> env, string name) =>
        env.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
