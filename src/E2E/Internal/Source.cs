// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Internal;

/// <summary>A position the report keeps: the file relative to the project root, with POSIX separators, and a one-based line and column.</summary>
internal sealed record SourceLocation(string File, int Line, int Column);

/// <summary>
/// Where in the project a stack unwound through (upstream's <c>internal/source.ts</c>). The step recorder names the test line each
/// step was called from, and the error serializer names the line a failure unwound through, so the two agree on what counts as the
/// user's line.
/// </summary>
internal static class Source
{
    /// <summary>
    /// <paramref name="file"/> relative to <paramref name="projectRoot"/> with POSIX separators, or null when the file is not below the
    /// root: beside it, above it, on another drive, or the root itself.
    /// </summary>
    public static string? ProjectRelativePath(string projectRoot, string file)
    {
        var relative = Path.GetRelativePath(projectRoot, file);
        if (relative.Length == 0 || relative == "." || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return null;
        }

        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <summary>
    /// The first frame of <paramref name="stack"/> inside the project and outside the runner: the line in the user's test file that
    /// the call or the failure went through. The runner's own frames never match, whatever path its symbols carry. Null without
    /// a project root, or when no frame names a project file.
    /// </summary>
    public static SourceLocation? Locate(StackTrace? stack, string? projectRoot)
    {
        if (stack is null || projectRoot is null)
        {
            return null;
        }

        var runner = typeof(Source).Assembly;
        foreach (var frame in stack.GetFrames())
        {
            if (frame.GetFileName() is not { Length: > 0 } file || frame.GetMethod()?.DeclaringType?.Assembly == runner)
            {
                continue;
            }

            if (ProjectRelativePath(projectRoot, file) is { } relative)
            {
                return new SourceLocation(relative, Math.Max(1, frame.GetFileLineNumber()), Math.Max(1, frame.GetFileColumnNumber()));
            }
        }

        return null;
    }
}
