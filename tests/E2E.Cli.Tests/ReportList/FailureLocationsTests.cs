// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using E2E.Internal;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('failure locations'), which tests <c>userFrame</c> on stack text.
/// A .NET failure carries a <see cref="StackTrace"/>, not text, and the report keeps the user's frame already located (<c>ReportError.Source</c>),
/// so these tests run <c>Source.Locate</c>, the port's <c>userFrame</c> (and <c>sourceLocation</c>, which upstream's <c>internal/source.ts</c>
/// keeps beside it), on stack traces that real calls produce. The second <c>it</c>, <c>resolves file URLs and strips module-cache query strings</c>,
/// is not ported: a .NET frame names a plain path, with no URL form and no query string to strip.
/// </summary>
public sealed class FailureLocationsTests
{
    /// <summary>The repository root, which holds the tests and the runner's sources, so the runner's own frames are inside the root too.</summary>
    private static string Root([CallerFilePath] string caller = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(caller)!, "..", "..", ".."));

    /// <summary>
    /// The stack of a failure thrown in the runner's assembly (below this test's call) and the line this call is on. One line, so the lambda's
    /// frame names the line of the call.
    /// </summary>
    private static (StackTrace Stack, int Line) RunnerFailure(Action thrower, [CallerLineNumber] int line = 0)
    {
        try
        {
            thrower();
        }
        catch (ArgumentNullException failure)
        {
            return (new StackTrace(failure, fNeedFileInfo: true), line);
        }

        throw new InvalidOperationException("The call did not fail.");
    }

    // Partial: the first `it` also skips frames in `node_modules`; a .NET project has no such directory, and a package's frames are outside the
    // project root.
    [Fact]
    public void Partial_Extracts_the_first_project_frame_skipping_runner_frames()
    {
        var (stack, line) = RunnerFailure(() => Source.ProjectRelativePath(null!, "x"));

        // The failure unwound through the runner first: its frame is in the stack, and inside the root when its symbols name its source file.
        Assert.Contains(stack.GetFrames(), frame => frame.GetMethod()?.DeclaringType?.Assembly == typeof(Source).Assembly);
        var located = Source.Locate(stack, Root());
        Assert.NotNull(located);
        Assert.Equal("tests/E2E.Cli.Tests/ReportList/FailureLocationsTests.cs", located.File);
        Assert.Equal(line, located.Line);
        Assert.True(located.Column >= 1);
    }

    [Fact]
    public void Returns_undefined_without_a_stack_root_or_matching_frame()
    {
        var (stack, _) = RunnerFailure(() => Source.ProjectRelativePath(null!, "x"));
        Assert.Null(Source.Locate(null, Root()));
        Assert.Null(Source.Locate(stack, Path.Combine(Path.GetTempPath(), "e2e-elsewhere")));
        Assert.Null(Source.Locate(stack, null));
    }
}
