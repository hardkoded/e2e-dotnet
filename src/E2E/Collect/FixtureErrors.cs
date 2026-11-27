// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// A fixture rule broken by code declared at a known line (upstream's <c>FixtureUseError</c>, <c>collect/fixture-errors.ts</c>). <see cref="DeclaredAt"/> is absolute;
/// whoever knows the project root puts it in front of the message (<see cref="FixtureErrors.DeclarationSite"/>). Differences: upstream's is a
/// <c>CollectionError</c>, a <c>ConfigurationError</c> with code <c>COLLECTION_ERROR</c>; the port's <see cref="ConfigurationException"/> is sealed, so this
/// is an <see cref="E2EException"/> with the same code that <c>ErrorSerializer</c> classifies as a configuration error.
/// </summary>
internal sealed class FixtureUseException(string message, SourceLocation? declaredAt)
    : E2EException("COLLECTION_ERROR", message)
{
    /// <summary>Where the offending code was declared, as an absolute location.</summary>
    public SourceLocation? DeclaredAt { get; } = declaredAt;
}

/// <summary>Upstream's <c>collect/fixture-errors.ts</c> function.</summary>
internal static class FixtureErrors
{
    /// <summary>
    /// Where the code a collection error is about was declared, as the message names it: <c>tests/support.ts:12</c>, relative to the project root.
    /// <paramref name="file"/> when the error carries no line, or one outside the project (upstream's <c>declarationSite</c>).
    /// </summary>
    public static string DeclarationSite(Exception? cause, string projectRoot, string file)
    {
        if (cause is not FixtureUseException { DeclaredAt: { } site })
        {
            return file;
        }

        var relative = Source.ProjectRelativePath(projectRoot, site.File);
        return relative is null ? file : $"{relative}:{site.Line}";
    }
}
