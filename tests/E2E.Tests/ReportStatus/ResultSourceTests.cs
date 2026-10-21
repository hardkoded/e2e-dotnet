// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>Upstream's <c>describe('result source')</c> (<c>tests/unit/report-status.test.ts</c>). Partial: upstream also asserts that the document is valid report-1.</summary>
public sealed class ResultSourceTests
{
    private static ReportResult DeclaredAt(string file) => FailedResult(ProductFailure) with { Source = new SourceLocation(file, 7, 3) };

    [Fact]
    public void Keeps_a_file_under_the_project_root_relative_to_it()
    {
        var document = Build([DeclaredAt("/repo/app/tests/case.e2e.ts")], projectRoot: "/repo/app");

        Assert.Equal(new SourceLocation("tests/case.e2e.ts", 7, 3), document.Run.Results[0].Source);
    }

    [Fact]
    public void Does_not_treat_a_sibling_directory_sharing_the_root_prefix_as_inside_the_project()
    {
        var document = Build([DeclaredAt("/repo/app-shared/helpers.ts")], projectRoot: "/repo/app");

        Assert.Equal(new SourceLocation("tests/case.e2e.ts", 7, 3), document.Run.Results[0].Source);
    }

    [Fact]
    public void Keeps_a_file_under_a_root_of_slash_or_one_ending_in_a_separator()
    {
        var atRoot = Build([DeclaredAt("/repo/app/tests/case.e2e.ts")], projectRoot: "/");
        Assert.Equal(new SourceLocation("repo/app/tests/case.e2e.ts", 7, 3), atRoot.Run.Results[0].Source);

        var trailing = Build([DeclaredAt("/repo/app/tests/case.e2e.ts")], projectRoot: "/repo/app/");
        Assert.Equal(new SourceLocation("tests/case.e2e.ts", 7, 3), trailing.Run.Results[0].Source);
    }
}
