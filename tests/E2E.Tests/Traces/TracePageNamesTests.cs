// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>Upstream's <c>describe('trace page names')</c> (<c>tests/unit/traces.test.ts</c>).</summary>
public sealed class TracePageNamesTests : TracePageKit
{
    [Fact]
    public async Task Read_as_the_file_and_the_title_keep_two_results_with_the_same_words_apart_by_their_id_and_bound_a_long_file_name()
    {
        static ReportResult Named(string id, string file) =>
            ReportBuilders.Result(id: id, file: file, titlePath: ["signs up", "with Google"], status: ResultStatus.Failed, attempts: [ReportBuilders.Attempt(AttemptStatus.Failed)]);

        var pages = await PagesOfAsync(
            Named("abcdef0123456789ff", "tests/Sign Up.e2e.ts"),
            Named("0123456789abcdefff", "tests/sign-up.e2e.ts"),
            Named("99999999aaaaaaaaff", $"tests/{string.Concat(Enumerable.Repeat("very-long-name-", 20))}.e2e.ts"));

        Assert.Equal(
            [
                "sign-up-signs-up-with-google-0123456789abcdef",
                "sign-up-signs-up-with-google-abcdef0123456789",
                $"{string.Concat(Enumerable.Repeat("very-long-name-", 3))[..40]}-signs-up-with-google-99999999aaaaaaaa",
            ],
            pages.Select(page => page.Name).Order(StringComparer.Ordinal));
    }
}
