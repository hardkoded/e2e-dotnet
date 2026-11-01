// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The engine of a resolved target as selection reads it: its name and the capabilities it declares (the slice of upstream's <c>Engine</c> that <c>select</c> uses). .NET-only name.</summary>
internal sealed record ResolvedEngine(string Name, IReadOnlySet<string> Capabilities);

/// <summary>A target as selection reads it (upstream's <c>ResolvedTarget</c>, <c>config/resolve.ts</c>); null <see cref="Engine"/> grades no capability.</summary>
internal sealed record ResolvedTarget(string Name, string Platform, AppConfig App, ResolvedEngine? Engine = null);

/// <summary>
/// The resolved config as selection and the test filters read it (upstream's <c>ResolvedConfig</c>, <c>config/resolve.ts</c>). The port has
/// no resolver yet, so only these members exist.
/// </summary>
internal sealed record ResolvedConfig
{
    public required string ProjectRoot { get; init; }

    /// <summary>The loaded config file; null for a config object.</summary>
    public string? ConfigPath { get; init; }

    public bool Ci { get; init; }

    public required IReadOnlyList<ResolvedTarget> Targets { get; init; }

    /// <summary>The test filters, in order; empty when unset. A run with <c>--changed-since</c> applies them.</summary>
    public IReadOnlyList<ITestFilter> Filters { get; init; } = [];

    /// <summary>Every secret the run holds, by name: the <c>config.secrets</c> entries and every credential's password.</summary>
    public IReadOnlyList<(string Name, string Value)> AllSecrets { get; init; } = [];
}
