// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ExpectAsyncRole;

public sealed class ToHaveRoleTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(100);

    private static readonly Screen Screen = ScreenFixture.Create(
        new SemanticNode
        {
            Ref = "node-1",
            TestId = "secret",
            Role = "textbox",
            Name = "Password",
            States = new NodeStates { Secure = true },
        },
        new SemanticNode
        {
            Ref = "node-2",
            TestId = "heading",
            Role = "heading",
            Name = "Title",
            Text = "Title",
            Level = 1,
        },
        new SemanticNode
        {
            Ref = "node-3",
            TestId = "switch",
            Role = "switch",
            Name = "Wi-Fi",
            States = new NodeStates { Checked = true },
        },
        new SemanticNode
        {
            Ref = "node-4",
            TestId = "logo",
            Role = "image",
            Name = "Logo",
        },
        new SemanticNode
        {
            Ref = "node-5",
            TestId = "unroled",
            Text = "Plain",
        },
        // A role outside the getByRole vocabulary, as a device engine reports some verbatim.
        new SemanticNode
        {
            Ref = "node-6",
            TestId = "verbatim",
            Role = "text",
            Text = "Hi",
        },
        new SemanticNode
        {
            Ref = "node-7",
            TestId = "twin",
            Role = "button",
            Name = "One",
        },
        new SemanticNode
        {
            Ref = "node-8",
            TestId = "twin",
            Role = "button",
            Name = "Two",
        },
        new SemanticNode
        {
            Ref = "node-9",
            TestId = "heading-text",
            Role = "heading",
            Text = "Title",
        }
    );

    private static Locator At(string testId) => Screen.GetByTestId(testId);

    [Fact]
    public async Task Compares_the_role_as_a_string()
    {
        await Expect.That(At("heading")).ToHaveRoleAsync("heading");
        await Expect.That(At("switch")).ToHaveRoleAsync("switch");
        // The role hierarchy does not count: a switch is not a checkbox.
        await Expect.That(At("switch")).Not.ToHaveRoleAsync("checkbox", Soon);
        var error = await Fails("ASSERTION_FAILED", () => Expect.That(At("switch")).ToHaveRoleAsync("checkbox", Soon));
        Assert.Equal(
            string.Join(
                "\n",
                "expect.toHaveRole failed",
                "locator: getByTestId(\"switch\")",
                "expected: role \"checkbox\"",
                "observed: role \"switch\" (match count 1)"
            ),
            error.Message
        );
    }

    [Fact]
    public async Task Reads_ARIA_img_as_image_as_getByRole_does()
    {
        await Expect.That(At("logo")).ToHaveRoleAsync("img");
        await Expect.That(At("logo")).ToHaveRoleAsync("image");
        var error = await Fails("ASSERTION_FAILED", () => Expect.That(At("heading")).ToHaveRoleAsync("img", Soon));
        Assert.Contains("expected: role \"image\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_role_it_found_when_a_negation_fails()
    {
        var error = await Fails("ASSERTION_FAILED", () =>
            Expect.That(At("switch")).Not.ToHaveRoleAsync("switch", Soon)
        );
        Assert.StartsWith("expect.not.toHaveRole failed\n", error.Message, StringComparison.Ordinal);
        Assert.Contains("observed: role \"switch\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compares_any_other_string_as_written()
    {
        await Expect.That(At("verbatim")).ToHaveRoleAsync("text");
        await Expect.That(At("unroled")).Not.ToHaveRoleAsync("paragraph", Soon);
    }

    [Fact]
    public async Task Fails_an_ambiguous_locator_at_once()
    {
        await Fails("LOCATOR_AMBIGUOUS", () => Expect.That(At("twin")).ToHaveRoleAsync("button", Soon));
    }

    [Fact]
    public async Task Fails_on_a_node_with_no_role_and_passes_its_negation()
    {
        await Expect.That(At("unroled")).Not.ToHaveRoleAsync("button", Soon);
        var error = await Fails("ASSERTION_FAILED", () => Expect.That(At("unroled")).ToHaveRoleAsync("button", Soon));
        Assert.Contains("observed: no role", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answers_on_a_secure_field_whose_role_is_never_withheld()
    {
        await Expect.That(At("secret")).ToHaveRoleAsync("textbox");
        await Expect.That(At("secret")).Not.ToHaveRoleAsync("searchbox", Soon);
    }

    [Fact]
    public async Task Keeps_polling_while_nothing_matches_negated_or_not()
    {
        var error = await Fails("ASSERTION_FAILED", () =>
            Expect.That(At("nowhere")).Not.ToHaveRoleAsync("button", Soon)
        );
        Assert.Contains("observed: no node", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_Refuses_a_role_that_is_not_a_non_empty_string_before_the_first_read()
    {
        var error = Assert.Throws<TestException>(new Action(() => _ = Expect.That(At("heading")).ToHaveRoleAsync("")));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("expect.toHaveRole role must be a non-empty string", error.Message);
    }

    private static async Task<TestException> Fails(string code, Func<Task> run)
    {
        var error = await Assert.ThrowsAsync<TestException>(run);
        Assert.Equal(code, error.Code);
        return error;
    }
}
