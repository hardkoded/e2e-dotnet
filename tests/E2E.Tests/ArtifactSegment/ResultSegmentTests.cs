// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.ArtifactSegment;

/// <summary>Upstream's <c>describe('resultSegment')</c> (<c>tests/unit/artifact-segment.test.ts</c>, tester-army/e2e <c>f185ca8</c>).</summary>
public sealed partial class ResultSegmentTests
{
    private const string Id = "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d";

    [GeneratedRegex(@"^[a-z0-9-]+\z")]
    private static partial Regex SafeSegment();

    [Fact]
    public void Names_a_result_by_its_file_the_first_words_of_its_title_and_the_head_of_its_id()
    {
        Assert.Equal("checkout-applies-the-coupon-1a2b3c4d5e6f7a8b", ArtifactPaths.ResultSegment(Id, "tests/checkout.e2e.ts", ["applies the coupon"]));
        Assert.Equal("cart-spec-cart-keeps-items-1a2b3c4d5e6f7a8b", ArtifactPaths.ResultSegment(Id, "apps/shop/tests/cart.spec.mts", ["cart", "keeps items"]));
    }

    [Fact]
    public void Leaves_out_a_first_title_word_the_file_name_already_says()
    {
        Assert.Equal(
            "todos-adds-todos-with-the-button-and-1a2b3c4d5e6f7a8b",
            ArtifactPaths.ResultSegment(Id, "tests/todos.e2e.ts", ["todos", "adds todos with the button and the keyboard"]));
        Assert.Equal("explore-the-app-and-find-bugs-1a2b3c4d5e6f7a8b", ArtifactPaths.ResultSegment(Id, "explore", ["Explore the app and find bugs"]));
    }

    [Fact]
    public void Keeps_two_results_with_the_same_words_apart_by_their_ids()
    {
        string[] titlePath = ["checkout", "applies the coupon"];
        var web = ArtifactPaths.ResultSegment("aaaaaaaaaaaaaaaa1111", "tests/checkout.e2e.ts", titlePath);
        var ios = ArtifactPaths.ResultSegment("aaaaaaaabbbbbbbb1111", "tests/checkout.e2e.ts", titlePath);
        Assert.NotEqual(web, ios);
    }

    [Fact]
    public void Drops_accents_falls_back_to_the_id_alone_and_cuts_one_long_word_always_a_safe_segment()
    {
        string[] segments =
        [
            ArtifactPaths.ResultSegment(Id, "tests/koszyk.e2e.ts", ["Sprawdź koszyk: żółć, Łódź"]),
            ArtifactPaths.ResultSegment(Id, "購入.e2e.ts", ["購入フローを確認する"]),
            ArtifactPaths.ResultSegment(Id, "tests/x.e2e.ts", [$"{new string('y', 300)} and more"]),
            ArtifactPaths.ResultSegment(Id, "../../etc/passwd", ["../../etc/passwd"]),
        ];
        Assert.Equal("koszyk-sprawdz-koszyk-zolc-lodz-1a2b3c4d5e6f7a8b", segments[0]);
        Assert.Equal("1a2b3c4d5e6f7a8b", segments[1]);
        Assert.Equal($"x-{new string('y', 32)}-1a2b3c4d5e6f7a8b", segments[2]);
        Assert.Equal("passwd-etc-passwd-1a2b3c4d5e6f7a8b", segments[3]);
        foreach (var segment in segments)
        {
            Assert.Matches(SafeSegment(), segment);
        }
    }
}
