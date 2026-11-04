// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Fill;

/// <summary>
/// <c>test.describe('page fill')</c> of upstream's <c>apps/testbed/tests/fill.e2e.ts</c> (Playwright's <c>page-fill.spec.ts</c>).
/// Not ported, as upstream: should throw if passed a non-string value (<c>Fill</c> is typed to strings).
/// </summary>
public sealed partial class PageFillTests : PlaywrightTest
{
    /// <summary>Five pairs of animation frames, Playwright's <c>rafraf(page, 5)</c>, so a pending fill has had its chance.</summary>
    private const string GiveItAChanceToFill =
        "() => new Promise((resolve) => { let frames = 0; const step = () => (++frames === 10 ? setTimeout(resolve, 0) : requestAnimationFrame(step)); requestAnimationFrame(step); })";

    private async Task<string> ValueOfInputAsync() =>
        (await Browser.EvaluateAsync<string>("() => document.querySelector('input').value"))!;

    [Test]
    public async Task Should_fill_textarea()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_fill_input()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("input").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_throw_on_unsupported_inputs()
    {
        await Browser.GotoAsync("/input/textarea.html");
        foreach (var type in new[] { "button", "checkbox", "file", "image", "radio", "reset", "submit" })
        {
            await Browser.EvaluateAsync<object>("(type) => document.querySelector('input').setAttribute('type', type)", type);
            var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync(""));
            Assert.That(error!.Message, Does.Contain($"Input of type \"{type}\" cannot be filled"));
        }
    }

    [Test]
    public async Task Should_fill_different_input_types()
    {
        await Browser.GotoAsync("/input/textarea.html");
        foreach (var type in new[] { "password", "search", "tel", "text", "url", "invalid-type" })
        {
            await Browser.EvaluateAsync<object>("(type) => document.querySelector('input').setAttribute('type', type)", type);
            await Browser.Locator("input").FillAsync("text " + type);
            Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("text " + type));
        }
    }

    [Test]
    public async Task Should_fill_range_input()
    {
        await Browser.GotoAsync(Content("<input type=range min=0 max=100 value=50>"));
        await Browser.Locator("input").FillAsync("42");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("42"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_range_value()
    {
        await Browser.GotoAsync(Content("<input type=range min=0 max=100 value=50>"));
        foreach (var value in new[] { "foo", "200", "15.43" })
        {
            var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync(value));
            Assert.That(error!.Message, Does.Contain("Malformed value"));
        }
    }

    [Test]
    public async Task Should_fill_color_input()
    {
        await Browser.GotoAsync(Content("<input type=color value=\"#e66465\">"));
        await Browser.Locator("input").FillAsync("#aaaaaa");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("#aaaaaa"));
    }

    [Test]
    public async Task Should_fill_color_input_case_insensitive()
    {
        await Browser.GotoAsync(Content("<input type=color value=\"#e66465\">"));
        await Browser.Locator("input").FillAsync("#AbCd00");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("#abcd00"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_color_value()
    {
        await Browser.GotoAsync(Content("<input type=color value=\"#e66465\">"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("badvalue"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_fill_date_input_after_clicking()
    {
        await Browser.GotoAsync(Content("<input type=date>"));
        await Browser.Locator("input").TapAsync();
        await Browser.Locator("input").FillAsync("2020-03-02");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("2020-03-02"));
    }

    [TestCase("color", "#aaaaaa")]
    [TestCase("date", "2020-03-02")]
    [TestCase("time", "13:15")]
    [TestCase("datetime-local", "2020-03-02T13:15:30")]
    [TestCase("month", "2020-03")]
    [TestCase("range", "42")]
    [TestCase("week", "2020-W50")]
    public async Task Input_event_composed_should_be_true_and_cross_shadow_dom_boundary(string type, string value)
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($$"""
            <body><script>
              const div = document.createElement('div');
              const shadowRoot = div.attachShadow({mode: 'open'});
              shadowRoot.innerHTML = '<input type={{type}}></input>';
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
              const input = document.querySelector('div').shadowRoot.querySelector('input');
              for (const event of ['input', 'change'])
                input.addEventListener(event, (e) => window.firedEvents.push(e.type + ':' + e.composed), false);
            }
            """);
        await Browser.Locator("input").FillAsync(value);
        Assert.That(await Browser.EvaluateAsync<string[]>("firedEvents"), Is.EqualTo(new[] { "input:true", "change:false" }));
        Assert.That(await Browser.EvaluateAsync<string[]>("firedBodyEvents"), Is.EqualTo(new[] { "input:true" }));
    }

    [Test]
    public async Task Should_throw_on_incorrect_date()
    {
        await Browser.GotoAsync(Content("<input type=date>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("2020-13-05"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_fill_time_input()
    {
        await Browser.GotoAsync(Content("<input type=time>"));
        await Browser.Locator("input").FillAsync("13:15");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("13:15"));
    }

    [Test]
    public async Task Should_fill_month_input()
    {
        await Browser.GotoAsync(Content("<input type=month>"));
        await Browser.Locator("input").FillAsync("2020-07");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("2020-07"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_month()
    {
        await Browser.GotoAsync(Content("<input type=month>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("2020-13"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_fill_week_input()
    {
        await Browser.GotoAsync(Content("<input type=week>"));
        await Browser.Locator("input").FillAsync("2020-W50");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("2020-W50"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_week()
    {
        await Browser.GotoAsync(Content("<input type=week>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("2020-123"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_time()
    {
        await Browser.GotoAsync(Content("<input type=time>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("25:05"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_fill_datetime_local_input()
    {
        await Browser.GotoAsync(Content("<input type=datetime-local>"));
        await Browser.Locator("input").FillAsync("2020-03-02T05:15");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("2020-03-02T05:15"));
    }

    [Test]
    public async Task Should_throw_on_incorrect_datetime_local()
    {
        await Browser.GotoAsync(Content("<input type=datetime-local>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("abc"));
        Assert.That(error!.Message, Does.Contain("Malformed value"));
    }

    [Test]
    public async Task Should_fill_contenteditable()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("div[contenteditable]").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div[contenteditable]').textContent"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_fill_contenteditable_with_new_lines()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content("<div contenteditable=\"true\"></div>"));
        await Browser.Locator("div[contenteditable]").FillAsync("John\nDoe");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div[contenteditable]').innerText"), Is.EqualTo("John\nDoe"));
    }

    [Test]
    public async Task Should_not_double_fill_in_contenteditable_with_beforeinput_handler_in_Firefox()
    {
        await Browser.GotoAsync(Content("""
            <div id="editor" contenteditable="true"></div>
            <script>
              const editor = document.getElementById('editor');
              editor.addEventListener('beforeinput', (event) => {
                event.preventDefault();
                editor.textContent = event.data;
              });
            </script>
            """));
        var locator = Browser.Locator("#editor");
        const string testValue = "Playwright";
        await locator.FillAsync(testValue);
        await Expect.That(locator).ToHaveTextAsync(testValue);
    }

    [Test]
    public async Task Should_fill_elements_with_existing_value_and_selection()
    {
        await Browser.GotoAsync("/input/textarea.html");

        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').value = 'value one'; }");
        await Browser.Locator("input").FillAsync("another value");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("another value"));

        await Browser.EvaluateAsync<object>("() => { const input = document.querySelector('input'); input.selectionStart = 1; input.selectionEnd = 2; }");
        await Browser.Locator("input").FillAsync("maybe this one");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("maybe this one"));

        await Browser.EvaluateAsync<object>("""
            () => {
              const div = document.querySelector('div[contenteditable]');
              div.innerHTML = 'some text <span>some more text<span> and even more text';
              const range = document.createRange();
              range.selectNodeContents(div.querySelector('span'));
              const selection = window.getSelection();
              selection.removeAllRanges();
              selection.addRange(range);
            }
            """);
        await Browser.Locator("div[contenteditable]").FillAsync("replace with this");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div[contenteditable]').textContent"), Is.EqualTo("replace with this"));
    }

    [Test]
    public async Task Should_throw_nice_error_without_injected_script_stack_when_element_is_not_an_input()
    {
        await Browser.GotoAsync(Content("<select><option>value1</option></select>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("select").FillAsync(""));
        Assert.That(error!.Message, Does.Contain("Element is not an <input>, <textarea> or [contenteditable] element"));
    }

    [Test]
    public async Task Should_retry_on_disabled_element()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').disabled = true; }");
        var done = false;
        var promise = Task.Run(async () =>
        {
            await Browser.Locator("input").FillAsync("some value", new ActionOptions { Timeout = TimeSpan.FromSeconds(10) });
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToFill);
        Assert.That(done, Is.False);
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo(""));

        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').disabled = false; }");
        await promise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_retry_on_readonly_element()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.EvaluateAsync<object>("() => { document.querySelector('textarea').readOnly = true; }");
        var done = false;
        var promise = Task.Run(async () =>
        {
            await Browser.Locator("textarea").FillAsync("some value", new ActionOptions { Timeout = TimeSpan.FromSeconds(10) });
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToFill);
        Assert.That(done, Is.False);
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo(""));

        await Browser.EvaluateAsync<object>("() => { document.querySelector('textarea').readOnly = false; }");
        await promise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_retry_on_invisible_element()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').style.display = 'none'; }");
        var done = false;
        var promise = Task.Run(async () =>
        {
            await Browser.Locator("input").FillAsync("some value", new ActionOptions { Timeout = TimeSpan.FromSeconds(10) });
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToFill);
        Assert.That(done, Is.False);
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo(""));

        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').style.display = 'inline'; }");
        await promise;
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_be_able_to_fill_the_body()
    {
        await Browser.GotoAsync(Content("<body contentEditable=\"true\"></body>"));
        await Browser.Locator("body").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.body.textContent"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_fill_fixed_position_input()
    {
        await Browser.GotoAsync(Content("<input style='position: fixed;' />"));
        await Browser.Locator("input").FillAsync("some value");
        Assert.That(await ValueOfInputAsync(), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_be_able_to_fill_when_focus_is_in_the_wrong_frame()
    {
        await Browser.GotoAsync(Content("""
            <div contentEditable="true"></div>
            <iframe></iframe>
            """));
        await Browser.Locator("iframe").FocusAsync();
        await Browser.Locator("div").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div').textContent"), Is.EqualTo("some value"));
    }

    [Test]
    public async Task Should_be_able_to_fill_the_input_type_number()
    {
        await Browser.GotoAsync(Content("<input id=\"input\" type=\"number\"></input>"));
        await Browser.Locator("input").FillAsync("42");
        Assert.That(await Browser.EvaluateAsync<string>("() => window.input.value"), Is.EqualTo("42"));
    }

    [Test]
    public async Task Should_be_able_to_fill_exponent_into_the_input_type_number()
    {
        await Browser.GotoAsync(Content("<input id=\"input\" type=\"number\"></input>"));
        await Browser.Locator("input").FillAsync("-10e5");
        Assert.That(await Browser.EvaluateAsync<string>("() => window.input.value"), Is.EqualTo("-10e5"));
    }

    [Test]
    public async Task Should_be_able_to_fill_input_type_number_with_empty_string()
    {
        await Browser.GotoAsync(Content("<input id=\"input\" type=\"number\" value=\"123\"></input>"));
        await Browser.Locator("input").FillAsync("");
        Assert.That(await Browser.EvaluateAsync<string>("() => window.input.value"), Is.EqualTo(""));
    }

    [Test]
    public async Task Should_not_be_able_to_fill_text_into_the_input_type_number()
    {
        await Browser.GotoAsync(Content("<input id=\"input\" type=\"number\"></input>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("input").FillAsync("abc"));
        Assert.That(error!.Message, Does.Contain("Cannot type text into input[type=number]"));
    }

    [Test]
    public async Task Should_be_able_to_clear_using_fill()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("input").FillAsync("some value");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo("some value"));
        await Browser.Locator("input").FillAsync("");
        Assert.That(await Browser.EvaluateAsync<string>("result"), Is.EqualTo(""));
    }

    [Test]
    public async Task Should_not_throw_when_fill_causes_navigation()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.GotoAsync(Content("<input type=date>"));
        await Browser.EvaluateAsync<object>("() => document.querySelector('input').addEventListener('input', () => window.location.href = '/empty.html')");
        await Task.WhenAll(
            Browser.Locator("input").FillAsync("2020-03-02"),
            Browser.WaitForURLAsync(EmptyHtml()));
        Assert.That(await Browser.UrlAsync(), Does.Contain("empty.html"));
    }

    [Test]
    public async Task Fill_back_to_back()
    {
        await Browser.GotoAsync(Content("<input id=\"one\"></input><input id=\"two\"></input>"));
        await Browser.Locator("#one").FillAsync("first value");
        await Browser.Locator("#two").FillAsync("second value");
        await Expect.That(Browser.Locator("#one")).ToHaveValueAsync("first value");
        await Expect.That(Browser.Locator("#two")).ToHaveValueAsync("second value");
    }

    [Test]
    public async Task Should_fill_contenteditable_with_focus_handler_that_collapses_selection()
    {
        await Browser.GotoAsync(Content("""
            <div contenteditable="true">initial text</div>
            <script>
              const editor = document.querySelector('[contenteditable]');
              editor.addEventListener('focus', () => {
                const selection = window.getSelection();
                if (selection.rangeCount > 0)
                  selection.collapseToEnd();
              });
            </script>
            """));
        await Browser.Locator("div[contenteditable]").FillAsync("some value");
        Assert.That(await Browser.Locator("div[contenteditable]").TextContentAsync(), Is.EqualTo("some value"));
    }

    [GeneratedRegex(@"empty\.html")]
    private static partial Regex EmptyHtml();
}
