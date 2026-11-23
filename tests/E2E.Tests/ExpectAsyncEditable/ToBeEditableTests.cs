// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ExpectAsyncEditable;

public sealed class ToBeEditableTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(100);

    private static int s_nextId;

    private static readonly Screen Screen = ScreenFixture.Create(
        Node("field", "textbox", "Name", new NodeStates { Readonly = false }),
        Node("read-only", "textbox", "Id", new NodeStates { Readonly = true }),
        // A readonly attribute is the engine's to read: the matcher reads the state alone.
        new SemanticNode
        {
            Ref = "node-attribute-only",
            TestId = "attribute-only",
            Role = "textbox",
            Name = "Total",
            Attributes = new Dictionary<string, string> { ["readonly"] = "", ["aria-readonly"] = "true" },
        },
        Node("disabled", "combobox", "Country", new NodeStates { Disabled = true, Readonly = false }),
        Node("disabled-read-only", "textbox", "Code", new NodeStates { Disabled = true, Readonly = true }),
        Node("checkbox", "checkbox", "Agree", new NodeStates { Readonly = false }),
        Node("grid-cell", "gridcell", "Qty", new NodeStates { Readonly = true }),
        Node("secret", "textbox", "Password", new NodeStates { Secure = true, Readonly = false }),
        // A disabled button: still no read-only state, so not judged.
        Node("button", "button", "Save", new NodeStates { Disabled = true }),
        new SemanticNode
        {
            Ref = "node-unroled",
            TestId = "unroled",
            Text = "Plain",
        },
        Node("twin", "textbox", "One", new NodeStates { Readonly = false }),
        Node("twin", "textbox", "Two", new NodeStates { Readonly = false })
    );

    private static SemanticNode Node(string testId, string role, string name, NodeStates states) =>
        new()
        {
            Ref = "node-" + Interlocked.Increment(ref s_nextId),
            TestId = testId,
            Role = role,
            Name = name,
            States = states,
        };

    private static LocatorExpect At(string testId) => Expect.That(Screen.GetByTestId(testId));

    [Fact]
    public async Task Passes_on_an_enabled_control_that_is_not_read_only()
    {
        await At("field").ToBeEditableAsync();
        await At("checkbox").ToBeEditableAsync();
        await At("field").Not.ToBeEditableAsync(editable: false);
    }

    [Fact]
    public async Task Reads_the_engine_read_only_state()
    {
        await At("read-only").Not.ToBeEditableAsync(timeout: Soon);
        await At("grid-cell").ToBeEditableAsync(editable: false);
        var error = await Fails("ASSERTION_FAILED", () => At("read-only").ToBeEditableAsync(timeout: Soon));
        Assert.Equal(
            string.Join(
                "\n",
                "expect.toBeEditable failed",
                "locator: getByTestId(\"read-only\")",
                "expected: editable",
                "observed: read-only (match count 1)"
            ),
            error.Message
        );
    }

    [Fact]
    public async Task Never_reads_attributes_itself()
    {
        foreach (
            var run in new[]
            {
                () => At("attribute-only").ToBeEditableAsync(timeout: Soon),
                () => At("attribute-only").Not.ToBeEditableAsync(timeout: Soon),
            }
        )
        {
            var error = await Fails("ASSERTION_FAILED", run);
            Assert.Contains("observed: role \"textbox\", no read-only state", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Counts_a_disabled_control_as_not_editable_and_says_disabled_first()
    {
        await At("disabled").ToBeEditableAsync(editable: false);
        var both = await Fails("ASSERTION_FAILED", () => At("disabled-read-only").ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: disabled (match count 1)", both.Message, StringComparison.Ordinal);
        var error = await Fails("ASSERTION_FAILED", () => At("disabled").ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: disabled (match count 1)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flips_with_editable_false_and_names_the_wanted_state()
    {
        var wanted = await Fails("ASSERTION_FAILED", () =>
            At("field").ToBeEditableAsync(editable: false, timeout: Soon)
        );
        Assert.Contains(
            "expected: read-only or disabled\nobserved: editable",
            wanted.Message,
            StringComparison.Ordinal
        );
        var negated = await Fails("ASSERTION_FAILED", () => At("field").Not.ToBeEditableAsync(timeout: Soon));
        Assert.Contains("expected: not editable\nobserved: editable", negated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_either_way_on_a_node_with_no_read_only_state()
    {
        foreach (var testId in new[] { "button", "unroled" })
        {
            await Fails("ASSERTION_FAILED", () => At(testId).ToBeEditableAsync(timeout: Soon));
            await Fails("ASSERTION_FAILED", () => At(testId).Not.ToBeEditableAsync(timeout: Soon));
            await Fails("ASSERTION_FAILED", () => At(testId).ToBeEditableAsync(editable: false, timeout: Soon));
        }

        var button = await Fails("ASSERTION_FAILED", () => At("button").Not.ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: role \"button\", no read-only state", button.Message, StringComparison.Ordinal);
        var unroled = await Fails("ASSERTION_FAILED", () => At("unroled").ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: no role, no read-only state", unroled.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answers_on_a_secure_field_whose_editability_is_not_a_secret()
    {
        await At("secret").ToBeEditableAsync();
    }

    [Fact]
    public async Task Keeps_polling_while_nothing_matches_negated_or_not()
    {
        var plain = await Fails("ASSERTION_FAILED", () => At("nowhere").ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: no node", plain.Message, StringComparison.Ordinal);
        var negated = await Fails("ASSERTION_FAILED", () => At("nowhere").Not.ToBeEditableAsync(timeout: Soon));
        Assert.Contains("observed: no node", negated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_an_ambiguous_locator_at_once()
    {
        await Fails("LOCATOR_AMBIGUOUS", () => At("twin").ToBeEditableAsync(timeout: Soon));
    }

    private static async Task<TestException> Fails(string code, Func<Task> run)
    {
        var error = await Assert.ThrowsAsync<TestException>(run);
        Assert.Equal(code, error.Code);
        return error;
    }
}
