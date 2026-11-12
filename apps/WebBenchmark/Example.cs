// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark;

/// <summary>One scenario of the catalog (upstream's <c>Example</c> type in <c>app/src/examples.ts</c>).</summary>
/// <param name="Component">The Razor component that renders the scenario, or null while the scenario is not ported yet.</param>
/// <param name="Slug">The path segment the scenario is served at: <c>/e/&lt;slug&gt;</c>.</param>
/// <param name="Name">The link text on the home page and the scenario's heading.</param>
/// <param name="Description">The line under the name on the home page.</param>
/// <param name="PlantedBug">
/// The intentional product bug a correct test must catch. Present only on bug-book scenarios; never rendered anywhere a test or an
/// agent can read it. Keep slug, name, and description flow-neutral so they do not leak the bug.
/// </param>
public sealed record Example(Type? Component, string Slug, string Name, string Description, string? PlantedBug = null);
