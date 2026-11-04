// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.SelectorsRole;

/// <summary>
/// <c>test.describe('selectors role')</c> of upstream's <c>apps/testbed/tests/selectors-role.e2e.ts</c> (Playwright's
/// <c>selectors-role.spec.ts</c>). A <c>role=</c> selector is <c>GetByRole</c> with the same options, a string name is exact by
/// default so Playwright's default substring match is <c>exact: false</c>, and an <c>evaluateAll(e =&gt; e.outerHTML)</c> read is
/// the matches' texts or attributes. The <c>include-hidden</c>, <c>checked="mixed"</c>, and <c>pressed="mixed"</c> cases are left
/// out, as upstream: <c>GetByRole</c> has no includeHidden or mixed state. Not ported, as upstream: errors (Playwright
/// <c>role=</c> selector parse errors).
/// </summary>
public sealed class SelectorsRoleTests : PlaywrightTest
{
    /// <summary>The texts as one string, for <see cref="Equal{T}"/>, which a body run by <c>ExpectedToFailAsync</c> needs.</summary>
    private static string Joined(IEnumerable<string> texts) => string.Join('|', texts);

    /// <summary>The <c>aria-label</c> of every current match, in document order.</summary>
    private static async Task<List<string?>> LabelsAsync(Locator locator)
    {
        var labels = new List<string?>();
        foreach (var match in await locator.AllAsync())
        {
            labels.Add(await match.GetAttributeAsync("aria-label"));
        }

        return labels;
    }

    [Test]
    public async Task Should_detect_roles()
    {
        await Browser.GotoAsync(Content("""
            <button>Hello</button>
            <select multiple="" size="2"></select>
            <select></select>
            <h3>Heading</h3>
            <details><summary>Hello</summary></details>
            <div role="dialog">I am a dialog</div>
            """));
        Assert.That(await Screen.GetByRole("button").AllTextContentsAsync(), Is.EqualTo(new[] { "Hello" }));
        await Expect.That(Screen.GetByRole("listbox")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("listbox")).ToHaveAttributeAsync("multiple", "");
        await Expect.That(Screen.GetByRole("combobox")).ToHaveCountAsync(1);
        Assert.That(await Screen.GetByRole("combobox").GetAttributeAsync("multiple"), Is.Null);
        Assert.That(await Screen.GetByRole("heading").AllTextContentsAsync(), Is.EqualTo(new[] { "Heading" }));
        Assert.That(await Screen.GetByRole("group").AllTextContentsAsync(), Is.EqualTo(new[] { "Hello" }));
        Assert.That(await Screen.GetByRole("dialog").AllTextContentsAsync(), Is.EqualTo(new[] { "I am a dialog" }));
        Assert.That(await Screen.GetByRole("menuitem").AllTextContentsAsync(), Is.Empty);
    }

    [Test]
    public async Task Should_support_selected()
    {
        await Browser.GotoAsync(Content("""
            <select>
              <option>Hi</option>
              <option selected>Hello</option>
            </select>
            <div>
              <div role="option" aria-selected="true">Hi</div>
              <div role="option" aria-selected="false">Hello</div>
            </div>
            """));
        Assert.That(await Screen.GetByRole("option", new RoleOptions { Selected = true }).AllTextContentsAsync(), Is.EqualTo(new[] { "Hello", "Hi" }));
        Assert.That(await Screen.GetByRole("option", new RoleOptions { Selected = false }).AllTextContentsAsync(), Is.EqualTo(new[] { "Hi", "Hello" }));
    }

    // Known difference, hardkoded/e2e-dotnet#1076: an indeterminate checkbox matches checked: false.
    [Test]
    public async Task Should_support_checked()
    {
        await ExpectedToFailAsync("expected count 3; observed count 4", async () =>
        {
            await Browser.GotoAsync(Content("""
                <input type=checkbox>
                <input type=checkbox checked>
                <input type=checkbox indeterminate>
                <div role=checkbox aria-checked="true">Hi</div>
                <div role=checkbox aria-checked="false">Hello</div>
                <div role=checkbox>Unknown</div>
                """));
            await Browser.EvaluateAsync<object>("() => { document.querySelector('[indeterminate]').indeterminate = true; }");

            var checkedBoxes = Screen.GetByRole("checkbox", new RoleOptions { Checked = true });
            await Expect.That(checkedBoxes).ToHaveCountAsync(2);
            await Expect.That(checkedBoxes.First()).ToHaveAttributeAsync("checked", "");
            await Expect.That(checkedBoxes.Nth(1)).ToHaveTextAsync("Hi");

            var unchecked_ = Screen.GetByRole("checkbox", new RoleOptions { Checked = false });
            await Expect.That(unchecked_).ToHaveCountAsync(3);
            Equal<string?>(null, await unchecked_.First().GetAttributeAsync("checked"));
            Equal<string?>(null, await unchecked_.First().GetAttributeAsync("indeterminate"));
            Equal("|Hello|Unknown", Joined(await unchecked_.AllTextContentsAsync()));

            await Expect.That(Screen.GetByRole("checkbox")).ToHaveCountAsync(6);
        });
    }

    // Known difference, hardkoded/e2e-dotnet#1076: a button with aria-pressed="mixed" matches pressed: false.
    [Test]
    public async Task Should_support_pressed()
    {
        await ExpectedToFailAsync("Expected Hi|Bye", async () =>
        {
            await Browser.GotoAsync(Content("""
                <button>Hi</button>
                <button aria-pressed="true">Hello</button>
                <button aria-pressed="false">Bye</button>
                <button aria-pressed="mixed">Mixed</button>
                """));
            Equal("Hello", Joined(await Screen.GetByRole("button", new RoleOptions { Pressed = true }).AllTextContentsAsync()));
            Equal("Hi|Bye", Joined(await Screen.GetByRole("button", new RoleOptions { Pressed = false }).AllTextContentsAsync()));
            Equal("Hi|Hello|Bye|Mixed", Joined(await Screen.GetByRole("button").AllTextContentsAsync()));
        });
    }

    // Known difference, hardkoded/e2e-dotnet#1076: a treeitem with no aria-expanded matches expanded: false.
    [Test]
    public async Task Should_support_expanded()
    {
        await ExpectedToFailAsync("Expected Bye", async () =>
        {
            await Browser.GotoAsync(Content("""
                <div role="treeitem">Hi</div>
                <div role="treeitem" aria-expanded="true">Hello</div>
                <div role="treeitem" aria-expanded="false">Bye</div>
                """));

            Equal("Hi|Hello|Bye", Joined(await Screen.GetByRole("treeitem").AllTextContentsAsync()));
            Equal("Hello", Joined(await Screen.GetByRole("treeitem", new RoleOptions { Expanded = true }).AllTextContentsAsync()));
            Equal("Bye", Joined(await Screen.GetByRole("treeitem", new RoleOptions { Expanded = false }).AllTextContentsAsync()));
            Equal("Hi", Joined(await Browser.Locator("[role=treeitem]:not([aria-expanded])").AllTextContentsAsync()));
        });
    }

    [Test]
    public async Task Should_support_disabled()
    {
        await Browser.GotoAsync(Content("""
            <button>Hi</button>
            <button disabled>Bye</button>
            <button aria-disabled="true">Hello</button>
            <button aria-disabled="false">Oh</button>
            <fieldset disabled>
              <button>Yay</button>
            </fieldset>
            <select>
              <optgroup disabled>
                <option>one</option>
              </optgroup>
              <optgroup>
                <option>two</option>
              </optgroup>
              <option disabled>three</option>
            </select>
            """));
        Assert.That(await Screen.GetByRole("button", new RoleOptions { Disabled = true }).AllTextContentsAsync(), Is.EqualTo(new[] { "Bye", "Hello", "Yay" }));
        Assert.That(await Screen.GetByRole("button", new RoleOptions { Disabled = false }).AllTextContentsAsync(), Is.EqualTo(new[] { "Hi", "Oh" }));
        Assert.That(await Screen.GetByRole("option", new RoleOptions { Disabled = true }).AllTextContentsAsync(), Is.EqualTo(new[] { "one", "three" }));
        Assert.That(await Screen.GetByRole("option", new RoleOptions { Disabled = false }).AllTextContentsAsync(), Is.EqualTo(new[] { "two" }));
        Assert.That(await Screen.GetByRole("option").AllTextContentsAsync(), Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public async Task Should_inherit_disabled_from_the_ancestor()
    {
        await Browser.GotoAsync(Content("""
            <span aria-disabled="true">
              <button>Click me!</button>
            </span>
            """));
        await Expect.That(Browser.Locator("button")).ToBeDisabledAsync();

        await Browser.GotoAsync(Content("""
            <span aria-disabled="true">
              <h1>Heading</h1>
            </span>
            """));
        await Expect.That(Browser.Locator("h1")).Not.ToBeDisabledAsync();
    }

    [Test]
    public async Task Should_support_disabled_fieldset()
    {
        await Browser.GotoAsync(Content("""
            <fieldset disabled>
              <input></input>
              <button data-testid="inside-fieldset-element">x</button>
              <legend>
                <button data-testid="inside-legend-element">legend</button>
              </legend>
            </fieldset>

            <fieldset disabled>
              <legend>
                <div>
                  <button data-testid="nested-inside-legend-element">x</button>
                </div>
              </legend>
            </fieldset>

            <fieldset disabled>
              <div></div>
              <legend>
                <button data-testid="first-legend-element">x</button>
              </legend>
              <legend>
                <button data-testid="second-legend-element">x</button>
              </legend>
            </fieldset>

            <fieldset disabled>
              <fieldset>
                <button data-testid="deep-button">x</button>
              </fieldset>
            </fieldset>
            """));

        await Expect.Soft(Screen.GetByTestId("inside-legend-element")).ToBeEnabledAsync();
        await Expect.Soft(Screen.GetByTestId("nested-inside-legend-element")).ToBeEnabledAsync();
        await Expect.Soft(Screen.GetByTestId("first-legend-element")).ToBeEnabledAsync();
        await Expect.Soft(Screen.GetByTestId("second-legend-element")).ToBeDisabledAsync();
        await Expect.Soft(Screen.GetByTestId("deep-button")).ToBeDisabledAsync();
    }

    // Known difference, hardkoded/e2e-dotnet#1076: a div with role="heading" and aria-level="5" does not match level: 5.
    [Test]
    public async Task Should_support_level()
    {
        await ExpectedToFailAsync("Expected Bye", async () =>
        {
            await Browser.GotoAsync(Content("""
                <h1>Hello</h1>
                <h3>Hi</h3>
                <div role="heading" aria-level="5">Bye</div>
                """));
            Equal("Hello", Joined(await Screen.GetByRole("heading", new RoleOptions { Level = 1 }).AllTextContentsAsync()));
            Equal("Hi", Joined(await Screen.GetByRole("heading", new RoleOptions { Level = 3 }).AllTextContentsAsync()));
            Equal("Bye", Joined(await Screen.GetByRole("heading", new RoleOptions { Level = 5 }).AllTextContentsAsync()));
        });
    }

    [Test]
    public async Task Should_filter_hidden_unless_explicitly_asked_for()
    {
        await Browser.GotoAsync(Content("""
            <button>Hi</button>
            <button hidden>Hello</button>
            <button aria-hidden="true">Yay</button>
            <button aria-hidden="false">Nay</button>
            <button style="visibility:hidden">Bye</button>
            <div style="visibility:hidden">
              <button>Oh</button>
            </div>
            <div style="visibility:hidden">
              <button style="visibility:visible">Still here</button>
            </div>
            <button style="display:none">Never</button>
            <div id=host1></div>
            <div id=host2 style="display:none"></div>

            <input name="one">
            <details>
              <summary>Open form</summary>
              <label>
                 Label
                 <input name="two">
              </label>
            </details>

            <select>
              <option style="visibility:hidden">One</option>
              <option style="display:none">Two</option>
              <option>Three</option>
            </select>

            <script>
              function addButton(host, text) {
                const root = host.attachShadow({ mode: 'open' });
                const button = document.createElement('button');
                button.textContent = text;
                root.appendChild(button);
              }
              addButton(document.getElementById('host1'), 'Shadow1');
              addButton(document.getElementById('host2'), 'Shadow2');
            </script>
            """));
        Assert.That(await Screen.GetByRole("button").AllTextContentsAsync(), Is.EqualTo(new[] { "Hi", "Nay", "Still here", "Shadow1" }));
        await Expect.That(Screen.GetByRole("textbox")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("textbox")).ToHaveAttributeAsync("name", "one");
        Assert.That(await Screen.GetByRole("option").AllTextContentsAsync(), Is.EqualTo(new[] { "One", "Three" }));
    }

    [Test]
    public async Task Should_support_name()
    {
        await Browser.GotoAsync(Content("""
            <div role="button" aria-label=" Hello "></div>
            <div role="button" aria-label="Hallo"></div>
            <div role="button" aria-label="Hello" aria-hidden="true"></div>
            <div role="button" aria-label="123" aria-hidden="true"></div>
            <div role="button" aria-label='foo"bar' aria-hidden="true"></div>
            """));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", "Hello")), Is.EqualTo(new[] { " Hello " }));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", " \n Hello ")), Is.EqualTo(new[] { " Hello " }));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", "Hello", exact: false)), Is.EqualTo(new[] { " Hello " }));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", "all", exact: false)), Is.EqualTo(new[] { "Hallo" }));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", new Regex("^H[ae]llo$"))), Is.EqualTo(new[] { " Hello ", "Hallo" }));
        Assert.That(await LabelsAsync(Screen.GetByRole("button", new Regex("h.*o", RegexOptions.IgnoreCase))), Is.EqualTo(new[] { " Hello ", "Hallo" }));
    }

    [Test]
    public async Task Should_support_option_name_with_html_whitespace()
    {
        await Browser.GotoAsync(Content("""
            <select>
              <option value="html">&nbsp;HTML</option>
            </select>
            """));
        await Expect.That(Screen.GetByRole("option", "HTML", exact: false)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Hidden_with_shadow_dom_slots()
    {
        await Browser.GotoAsync(Content("""
            <div make-hidden>
              <button>hidden1</button>
            </div>
            <div make-hidden>
              <span><button>hidden2</button></v>
            </div>
            <div>
              <button>visible1</button>
            </div>
            <div>
              <span><button>visible2</button></span>
            </div>
            <script>
              for (const div of document.querySelectorAll('div')) {
                const hidden = div.hasAttribute('make-hidden');
                div.attachShadow({ mode: 'open' }).innerHTML = hidden ? 'nothing to see here' : '<slot></slot>';
              }
            </script>
            """));
        Assert.That(await Screen.GetByRole("button").AllTextContentsAsync(), Is.EqualTo(new[] { "visible1", "visible2" }));
    }

    [Test]
    public async Task Should_support_output_accessible_name()
    {
        await Browser.GotoAsync(Content("<label>Output1<output>output</output></label>"));
        await Expect.That(Screen.GetByRole("status", "Output1", exact: false)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Should_not_match_scope_by_default()
    {
        await Browser.GotoAsync(Content("""
            <ul>
              <li aria-label="Parent list">
                Parent list
                <ul>
                  <li>child 1</li>
                  <li>child 2</li>
                </ul>
              </li>
            </ul>
            """));
        var children = Screen.GetByRole("listitem", "Parent list", exact: false).GetByRole("listitem");
        await Expect.That(children).ToHaveCountAsync(2);
        await Expect.That(children).ToHaveTextAsync(new TextMatch[] { "child 1", "child 2" });
    }
}
