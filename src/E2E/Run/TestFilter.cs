// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Leaves collected tests out of a run before anything starts (upstream's <c>TestFilter</c>, <c>types.ts</c>). A host that selects
/// tests calls it through <c>TestFilters.ApplyTestFilters</c>, at most once. A call that throws, returns an invalid result, or
/// runs past 5 minutes leaves nothing out. A failed call scores nothing either, unless the exception it throws is a
/// <see cref="TestFilterException"/> with a <c>Relevance</c>: the run checks it as it checks a result's, keeps the scores, and
/// still warns of the failure. A call past 5 minutes is abandoned, and nothing it throws after that is read. .NET-only: the interface prefix and
/// the <c>Async</c> suffix.
/// </summary>
public interface ITestFilter
{
    /// <summary>Names the filter in the report and in errors.</summary>
    string Name { get; }

    /// <summary>Decides which of <paramref name="tests"/> to leave out; every test the result does not name runs.</summary>
    Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context);
}

/// <summary>One test a filter may leave out: what names it, never its body (upstream's <c>TestFilterCandidate</c>).</summary>
/// <param name="Id">Stable test id (<c>&lt;file&gt;::&lt;titles&gt;</c>), the report's <c>testId</c>, and the key <c>Exclude</c> takes.</param>
/// <param name="File">Project-relative POSIX path of the test file.</param>
/// <param name="TitlePath">The describe titles, then the test title.</param>
/// <param name="Tags">The describe chain's tags, outermost first, then the test's own.</param>
/// <param name="SetupFile">Project-relative POSIX path of the setup test file that saves the test's session; null when the test uses none.</param>
public sealed record TestFilterCandidate(
    string Id,
    string File,
    IReadOnlyList<string> TitlePath,
    IReadOnlyList<string> Tags,
    string? SetupFile = null
);

/// <summary>What a run's test filters judge: every file that differs from the base commit (upstream's <c>TestFilterChanges</c>).</summary>
/// <param name="Base">The commit the change is measured from: the merge base of the base ref and HEAD.</param>
/// <param name="Root">Absolute path of the git repository root, spelled as on disk like <c>ProjectRoot</c>; <paramref name="Files"/> are relative to it.</param>
/// <param name="Files">
/// Repository-relative POSIX paths of every file added, modified, or deleted since <paramref name="Base"/>, both paths of a rename,
/// uncommitted and untracked files included; sorted by code point. Spelled as git prints them, which on macOS is NFC while a test's
/// <c>File</c> keeps the bytes on disk, so compare a non-ASCII path in NFC on both sides.
/// </param>
public sealed record TestFilterChanges(string Base, string Root, IReadOnlyList<string> Files);

/// <summary>What the host hands a filter besides its tests (upstream's <c>TestFilterContext</c>).</summary>
public sealed class TestFilterContext
{
    /// <summary>Absolute project root, spelled as on disk like <see cref="Changes"/>.<c>Root</c>: symlinks resolved, names in on-disk case; a test's <c>File</c> is relative to it.</summary>
    public required string ProjectRoot { get; init; }

    /// <summary>The change the filter judges; the same for every filter of the run.</summary>
    public required TestFilterChanges Changes { get; init; }

    /// <summary>
    /// The config file this run loaded, whatever its name, as a POSIX path relative to <c>Changes.Root</c> like <c>Changes.Files</c>, in its
    /// on-disk bytes (compare in NFC, as with a test's <c>File</c>); null when the run was given a config object or loaded one outside the
    /// repository.
    /// </summary>
    public string? ConfigFile { get; init; }

    /// <summary>Cancelled when the run is interrupted, or after 5 minutes; hand it to every model and network call.</summary>
    public required CancellationToken Signal { get; init; }

    /// <summary>Prints one progress line as a run notice.</summary>
    public required Action<string> Log { get; init; }

    /// <summary>Replaces every fixed-value secret and credential password in the text, and any run of 8 or more consecutive characters of one.</summary>
    public required Func<string, string> Redact { get; init; }
}

/// <summary>A filter's answer: every test it does not name runs (upstream's <c>TestFilterResult</c>).</summary>
public sealed class TestFilterResult
{
    /// <summary>The tests to leave out, by <see cref="TestFilterCandidate.Id"/>, each with the reason the report shows.</summary>
    public required IReadOnlyDictionary<string, string> Exclude { get; init; }

    /// <summary>
    /// How likely each test is to fail on this change, from 0 to 1, by <see cref="TestFilterCandidate.Id"/>. Upstream's runner starts the files of
    /// the likeliest tests first, in bands at 75%, 50%, and 25%, and targets by the band of their best file; here the scores are kept and
    /// <c>Units.OrderWorkPlans</c> orders plans with them, but the test framework owns the order of a <c>dotnet test</c> run, so they do not
    /// reorder it. A test no filter scores counts as 0, and one several filters score keeps its highest. It never leaves a test out.
    /// </summary>
    public IReadOnlyDictionary<string, double>? Relevance { get; init; }

    /// <summary>One line about the whole decision, such as <c>the diff is too large to judge</c>.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// What a filter throws when it cannot finish but still knows how likely some tests are to fail, such as the ones the change touches. .NET-only:
/// upstream reads a <c>relevance</c> property off whatever the filter throws, which C# has no way to name without a type.
/// </summary>
public class TestFilterException : Exception
{
    /// <summary>Creates the exception.</summary>
    public TestFilterException() { }

    /// <summary>Creates the exception with a message.</summary>
    public TestFilterException(string message)
        : base(message) { }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    public TestFilterException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Shaped like <see cref="TestFilterResult.Relevance"/>; the run checks it as it checks a result's. Null scores nothing.</summary>
    public virtual IReadOnlyDictionary<string, double>? Relevance { get; init; }
}
