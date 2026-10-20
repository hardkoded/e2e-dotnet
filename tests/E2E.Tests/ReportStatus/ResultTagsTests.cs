// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>Upstream's <c>describe('result tags')</c> (<c>tests/unit/report-status.test.ts</c>).</summary>
public sealed class ResultTagsTests
{
    // Partial: upstream also asserts that the document is valid report-1; the port has no wire form yet.
    [Fact]
    public void Carries_the_tags_a_test_declares_an_empty_list_when_it_declares_none()
    {
        var plain = FailedResult(ProductFailure);
        var tagged = FailedResult(ProductFailure, "tagged") with { DeclarationIndex = 1, Tags = ["smoke", "billing"] };

        var document = Build([plain, tagged]);

        Assert.Equal(new string[][] { [], ["smoke", "billing"] }, document.Run.Results.Select(result => result.Tags.ToArray()));
    }
}
