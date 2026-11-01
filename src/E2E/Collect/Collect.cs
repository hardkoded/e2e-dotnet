// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>Whether a collected test is an ordinary test or a setup test that saves a session (upstream's <c>kind: 'test' | 'setup'</c>, <c>collect/collect.ts</c>).</summary>
internal enum TestKind
{
    Test,
    Setup,
}

/// <summary>How a test was declared (upstream's <c>TestMode</c>, <c>collect/registry.ts</c>): plainly, skipped, or <c>.only</c>.</summary>
internal enum TestMode
{
    Normal,
    Skip,
    Only,
}

/// <summary>A <c>describe</c> group; the selection reads its title to name a serial group (upstream's <c>GroupNode</c>, <c>collect/registry.ts</c>, which also holds the group's options and parent).</summary>
internal sealed record GroupNode(string Title);

/// <summary>
/// A test as collection names it (upstream's <c>CollectedTest</c>, <c>collect/collect.ts</c>, with its <c>TestIdentity</c> and
/// <c>RegisteredTest</c>): the identity a report carries, and what selection reads. Not ported: the test body, hooks and fixtures,
/// <c>source</c>, and the raw <c>options</c> with the <c>group</c> chain they resolve through.
/// </summary>
internal sealed record CollectedTest
{
    public required TestKind Kind { get; init; }

    public required string Title { get; init; }

    /// <summary>The describe titles, then the test title.</summary>
    public required IReadOnlyList<string> TitlePath { get; init; }

    public required int DeclarationIndex { get; init; }

    /// <summary>The sessions a setup test saves.</summary>
    public IReadOnlyList<string> Sessions { get; init; } = [];

    /// <summary>The tags the test declares, outermost describe first, each once; what <c>--tag</c> selects on.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Normalized project-root-relative file path with <c>/</c> separators.</summary>
    public required string File { get; init; }

    public required string Id { get; init; }

    public string? SerialId { get; init; }

    public TestMode Mode { get; init; } = TestMode.Normal;

    /// <summary>Outermost serial group, when the test is a serial-group member.</summary>
    public GroupNode? SerialRoot { get; init; }

    /// <summary>Whether the positionals selected this test (always, with none); an unselected setup test still runs when a selected test consumes its session.</summary>
    public bool Selected { get; init; } = true;

    /// <summary>
    /// The test's options once resolved (upstream's <c>resolveOptions(test, config)</c>, which the port does not have yet: no group chain and
    /// no resolved config). .NET-only: a collected test carries its result.
    /// </summary>
    public required ResolvedTestOptions Options { get; init; }
}

/// <summary>A collected file, with what the selection reads of it (upstream's <c>CollectedFile</c>, <c>collect/collect.ts</c>).</summary>
internal sealed record CollectedFile
{
    public required string File { get; init; }

    /// <summary>Whether the positionals selected this file; a test in it is selected too unless <c>Lines</c> leaves it out.</summary>
    public bool Selected { get; init; } = true;

    /// <summary>The declaration lines <c>file:line</c> positionals named; only the tests declared at those lines are selected. Null when the file was selected whole.</summary>
    public IReadOnlyList<int>? Lines { get; init; }

    /// <summary>With <see cref="Lines"/>, the lines at which this file itself declares tests, for the <c>NO_TESTS</c> message.</summary>
    public IReadOnlyList<int>? DeclaredLines { get; init; }
}

/// <summary>A discovered file a narrowed run could not collect, and why (upstream's <c>UncollectedFile</c>).</summary>
internal sealed record UncollectedFile(string File, string Reason);

/// <summary>
/// What collection found (upstream's <c>Collection</c>). Not ported: <c>nearMisses</c> and <c>unmatchedPositionals</c>, which only the
/// glob and positional diagnostics of <c>NO_TESTS</c> read, and each file's <c>registration</c> and <c>tests</c>.
/// </summary>
internal sealed record Collection
{
    public IReadOnlyList<CollectedFile> Files { get; init; } = [];

    public IReadOnlyList<CollectedTest> Tests { get; init; } = [];

    /// <summary>Files positionals left unselected that failed to collect.</summary>
    public IReadOnlyList<UncollectedFile> Uncollected { get; init; } = [];
}
