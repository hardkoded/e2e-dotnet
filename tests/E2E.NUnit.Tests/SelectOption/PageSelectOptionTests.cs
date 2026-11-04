// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;
using Option = E2E.SelectOption;

namespace E2E.NUnit.Tests.SelectOption;

/// <summary>
/// <c>test.describe('page select option')</c> of upstream's <c>apps/testbed/tests/select-option.e2e.ts</c> (Playwright's
/// <c>page-select-option.spec.ts</c>). Not ported, as upstream: should select single option by handle (no element handles); should select
/// single option by multiple attributes, should not select single option when some attributes do not match (<c>SelectOptionAsync</c> takes
/// one of label, value, or index); should select only first option, should select multiple options, should select multiple options with
/// attributes, should return [] on no matched values, should return an array of matched values, should return an array of one element when
/// multiple is not set, should return [] on no values, should not allow null items, should unselect with null, should deselect all options
/// when passed no values for a multiple select, should deselect all options when passed no values for a select without multiple, should wait
/// for multiple options to be present (<c>SelectOptionAsync</c> takes one option, no arrays, and returns nothing); should throw if passed wrong
/// types (asserts Playwright's protocol validator messages; the option is typed).
/// </summary>
public sealed partial class PageSelectOptionTests : PlaywrightTest
{
    /// <summary>Five pairs of animation frames, Playwright's <c>rafraf(page, 5)</c>, so a pending select has had its chance.</summary>
    private const string GiveItAChanceToResolve =
        "() => new Promise((resolve) => { let frames = 0; const step = () => (++frames === 10 ? setTimeout(resolve, 0) : requestAnimationFrame(step)); requestAnimationFrame(step); })";

    /// <summary>Appends a <c>scarlet</c> option to the page's select.</summary>
    private const string AddScarlet =
        "() => { const option = document.createElement('option'); option.value = 'scarlet'; option.textContent = 'Scarlet'; document.querySelector('select').appendChild(option); }";

    private async Task ExpectResultAsync(string name, string expected) =>
        Assert.That(await Browser.EvaluateAsync<string[]>($"() => window.result.{name}"), Is.EqualTo(new[] { expected }));

    [Test]
    public async Task Should_select_single_option()
    {
        // Upstream marks this { fail: 'a bare string selects by label only, where Playwright matches the value or the label' }; this port's
        // bare string matches the value too, so the test passes without the marker.
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync("blue");
        await ExpectResultAsync("onInput", "blue");
        await ExpectResultAsync("onChange", "blue");
    }

    [Test]
    public async Task Should_select_single_option_by_value()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync(new Option { Value = "blue" });
        await ExpectResultAsync("onInput", "blue");
        await ExpectResultAsync("onChange", "blue");
    }

    [Test]
    public async Task Should_fall_back_to_selecting_by_label()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync("Blue");
        await ExpectResultAsync("onInput", "blue");
        await ExpectResultAsync("onChange", "blue");
    }

    [Test]
    public async Task Should_select_single_option_by_label()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync(new Option { Label = "Indigo" });
        await ExpectResultAsync("onInput", "indigo");
        await ExpectResultAsync("onChange", "indigo");
    }

    [Test]
    public async Task Should_select_single_option_by_label_with_html_whitespace()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync(new Option { Label = "HTML" });
        await ExpectResultAsync("onInput", "html");
        await ExpectResultAsync("onChange", "html");
    }

    [Test]
    public async Task Should_select_single_option_by_index()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync(new Option { Index = 2 });
        await ExpectResultAsync("onInput", "brown");
        await ExpectResultAsync("onChange", "brown");
    }

    [Test]
    public async Task Should_not_throw_when_select_causes_navigation()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.EvaluateAsync<object>("() => document.querySelector('select').addEventListener('input', () => window.location.href = '/empty.html')");
        await Task.WhenAll(
            Browser.Locator("select").SelectOptionAsync(new Option { Value = "blue" }),
            Browser.WaitForURLAsync(EmptyHtml()));
        Assert.That(await Browser.UrlAsync(), Does.Contain("empty.html"));
    }

    [Test]
    public async Task Should_respect_event_bubbling()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.Locator("select").SelectOptionAsync(new Option { Value = "blue" });
        await ExpectResultAsync("onBubblingInput", "blue");
        await ExpectResultAsync("onBubblingChange", "blue");
    }

    [Test]
    public async Task Should_throw_when_element_is_not_a_select()
    {
        await Browser.GotoAsync("/input/select.html");
        var error = await Assert.CatchAsync(() => Browser.Locator("body").SelectOptionAsync(""));
        Assert.That(error!.Message, Does.Contain("Element is not a <select> element"));
    }

    [Test]
    public async Task Should_work_when_re_defining_top_level_Event_class()
    {
        await Browser.GotoAsync("/input/select.html");
        await Browser.EvaluateAsync<object>("() => { window.Event = null; }");
        await Browser.Locator("select").SelectOptionAsync(new Option { Value = "blue" });
        await ExpectResultAsync("onInput", "blue");
        await ExpectResultAsync("onChange", "blue");
    }

    [Test]
    public async Task Should_wait_for_option_to_be_present()
    {
        await Browser.GotoAsync("/input/select.html");
        var didSelect = false;
        var selectPromise = Task.Run(async () =>
        {
            await Browser.Locator("select").SelectOptionAsync(new Option { Value = "scarlet" }, new ActionOptions { Timeout = TimeSpan.FromSeconds(10) });
            didSelect = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToResolve);
        Assert.That(didSelect, Is.False);
        await Browser.EvaluateAsync<object>(AddScarlet);
        await selectPromise;
        await ExpectResultAsync("onChange", "scarlet");
    }

    [Test]
    public async Task Should_wait_for_option_index_to_be_present()
    {
        await Browser.GotoAsync("/input/select.html");
        var len = await Browser.EvaluateAsync<int>("() => document.querySelector('select').options.length");
        var didSelect = false;
        var selectPromise = Task.Run(async () =>
        {
            await Browser.Locator("select").SelectOptionAsync(new Option { Index = len }, new ActionOptions { Timeout = TimeSpan.FromSeconds(10) });
            didSelect = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToResolve);
        Assert.That(didSelect, Is.False);
        await Browser.EvaluateAsync<object>(AddScarlet);
        await selectPromise;
        await ExpectResultAsync("onChange", "scarlet");
    }

    [Test]
    public async Task Input_event_composed_should_be_true_and_cross_shadow_dom_boundary()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content("""
            <body><script>
              const div = document.createElement('div');
              const shadowRoot = div.attachShadow({mode: 'open'});
              shadowRoot.innerHTML = `<select>
                <option value="black">Black</option>
                <option value="blue">Blue</option>
              </select>`;
              document.body.appendChild(div);
            </script></body>
            """));
        await Browser.EvaluateAsync<object>("""
            () => {
              window.firedBodyEvents = [];
              for (const event of ['input', 'change'])
                document.body.addEventListener(event, (e) => window.firedBodyEvents.push(e.type + ':' + e.composed), false);
            }
            """);
        await Browser.EvaluateAsync<object>("""
            () => {
              window.firedEvents = [];
              const select = document.querySelector('div').shadowRoot.querySelector('select');
              for (const event of ['input', 'change'])
                select.addEventListener(event, (e) => window.firedEvents.push(e.type + ':' + e.composed), false);
            }
            """);
        await Browser.Locator("select").SelectOptionAsync(new Option { Value = "blue" });
        Assert.That(await Browser.EvaluateAsync<string[]>("firedEvents"), Is.EqualTo(new[] { "input:true", "change:false" }));
        Assert.That(await Browser.EvaluateAsync<string[]>("firedBodyEvents"), Is.EqualTo(new[] { "input:true" }));
    }

    [Test]
    public async Task Should_wait_for_select_to_be_enabled()
    {
        await Browser.GotoAsync(Content("""
            <select disabled>
              <option>one</option>
              <option>two</option>
            </select>

            <script>
            function hydrate() {
              const select = document.querySelector('select');
              select.removeAttribute('disabled');
              select.addEventListener('change', () => {
                window['result'] = select.value;
              });
            }
            </script>
            """));
        var selectPromise = Browser.Locator("select").SelectOptionAsync("two");
        await Task.Delay(1000);
        await Browser.EvaluateAsync<object>("() => window.hydrate()");
        await selectPromise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("two"));
        await Expect.That(Browser.Locator("select")).ToHaveValueAsync("two");
    }

    [Test]
    public async Task Should_wait_for_option_to_be_enabled()
    {
        await Browser.GotoAsync(Content("""
            <select>
              <option>one</option>
              <option disabled id=myoption>two</option>
            </select>

            <script>
            function hydrate() {
              const option = document.querySelector('#myoption');
              option.removeAttribute('disabled');
              const select = document.querySelector('select');
              select.addEventListener('change', () => {
                window['result'] = select.value;
              });
            }
            </script>
            """));
        var error = await Assert.CatchAsync(() => Browser.Locator("select").SelectOptionAsync("two", new ActionOptions { Timeout = TimeSpan.FromSeconds(1) }));
        Assert.That(error!.Message, Does.Contain("option being selected is not enabled"));

        var selectPromise = Browser.Locator("select").SelectOptionAsync("two");
        await Task.Delay(1000);
        await Browser.EvaluateAsync<object>("() => window.hydrate()");
        await selectPromise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("two"));
        await Expect.That(Browser.Locator("select")).ToHaveValueAsync("two");
    }

    [Test]
    public async Task Should_wait_for_optgroup_to_be_enabled()
    {
        await Browser.GotoAsync(Content("""
            <select>
              <option>one</option>
              <optgroup label="Group" disabled id=mygroup>
                <option>two</option>
              </optgroup>
            </select>

            <script>
            function hydrate() {
              const group = document.querySelector('#mygroup');
              group.removeAttribute('disabled');
              const select = document.querySelector('select');
              select.addEventListener('change', () => {
                window['result'] = select.value;
              });
            }
            </script>
            """));
        var error = await Assert.CatchAsync(() => Browser.Locator("select").SelectOptionAsync("two", new ActionOptions { Timeout = TimeSpan.FromSeconds(1) }));
        Assert.That(error!.Message, Does.Contain("option being selected is not enabled"));

        var selectPromise = Browser.Locator("select").SelectOptionAsync("two");
        await Task.Delay(1000);
        await Browser.EvaluateAsync<object>("() => window.hydrate()");
        await selectPromise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("two"));
        await Expect.That(Browser.Locator("select")).ToHaveValueAsync("two");
    }

    [Test]
    public async Task Should_wait_for_select_to_be_swapped()
    {
        await Browser.GotoAsync(Content("""
            <select disabled>
              <option>one</option>
              <option>two</option>
            </select>

            <script>
            function hydrate() {
              const select = document.querySelector('select');
              select.remove();

              const newSelect = document.createElement('select');
              const option1 = document.createElement('option');
              option1.textContent = 'one';
              newSelect.appendChild(option1);
              const option2 = document.createElement('option');
              option2.textContent = 'two';
              newSelect.appendChild(option2);

              document.body.appendChild(newSelect);

              newSelect.addEventListener('change', () => {
                window['result'] = newSelect.value;
              });
            }
            </script>
            """));
        var selectPromise = Browser.Locator("select").SelectOptionAsync("two");
        await Task.Delay(1000);
        await Browser.EvaluateAsync<object>("() => window.hydrate()");
        await selectPromise;
        await Expect.That(Browser.Locator("select")).ToHaveValueAsync("two");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("two"));
    }

    [GeneratedRegex(@"empty\.html")]
    private static partial Regex EmptyHtml();
}
