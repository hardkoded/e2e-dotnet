// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli;

/// <summary>Upstream's <c>cli/docs-url.ts</c>. Only <c>REPOSITORY_URL</c> is ported: the port prints no link to the documentation site.</summary>
internal static class DocsUrl
{
    /// <summary>The GitHub repository, where a star helps others find e2e (upstream's <c>REPOSITORY_URL</c>; the port's own repository).</summary>
    public const string RepositoryUrl = "https://github.com/hardkoded/e2e-dotnet";
}
