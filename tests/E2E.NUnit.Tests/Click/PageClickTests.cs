// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Click;

/// <summary>
/// <c>test.describe('page click')</c> of upstream's <c>apps/testbed/tests/click.e2e.ts</c> (Playwright's <c>page-click.spec.ts</c>).
/// <c>TapAsync</c> stands for <c>page.click</c>. Not ported, as upstream: should click button inside frameset; should issue clicks in
/// parallel in page and popup; should select the text by triple clicking; should waitFor visible when already visible; should not wait with
/// force; should click the button with fixed position inside an iframe; should click the button with px border with offset; should click the
/// button with em border with offset; should click a very large button with offset; should click a button in scrolling container with
/// offset; should wait for becoming hit target with trial run; trial run should work with short timeout; trial run should not click; trial
/// run should not double click; should fail when obscured and not waiting for hit target; should wait for LABEL to be clickable when it has
/// pointer-events:none; should report nice error when element is detached and force-clicked; should fail when element detaches after
/// animation; should retry when element detaches after animation; should retry when element is animating from outside the viewport; should
/// fail when element is animating from outside the viewport with force; should dispatch microtasks in order; should not throw protocol error
/// when navigating during the click; should retry when navigating during the click; should not hang when frame is detached; should click in
/// an iframe with border 2; should not retain removed iframe after clicking inside it; should click a button that is overlaid by a
/// permission popup; should click in a transformed iframe with force; ensure events are dispatched in the individual tasks; should set
/// PointerEvent.pressure on pointerdown; should set PointerEvent.pressure on pointermove; should click with tweened mouse movement; should
/// abort via signal; should throw an Error when aborted in-flight with a string reason; should abort via already-aborted signal; should
/// throw an Error when aborted via an already-aborted signal with a string reason (no <c>force</c>, <c>trial</c>, <c>position</c> scrolling,
/// <c>clickCount</c>, <c>steps</c>, element handles, Frame objects, events, or abort signals; some upstream left undone).
/// </summary>
public sealed class PageClickTests : PlaywrightTest
{
    /// <summary>Two animation frames and a task, Playwright's <c>rafraf</c>, so a pending tap has had its chance.</summary>
    private const string GiveItAChanceToClick =
        "() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(resolve, 0))))";

    private static readonly ActionOptions Ten = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly ActionOptions NoTimeout = new() { Timeout = TimeSpan.Zero };

    private async Task<string> ResultAsync() => (await Browser.EvaluateAsync<string>("result"))!;

    [Test]
    public async Task Should_click_the_button()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_svg()
    {
        await Browser.GotoAsync(Content("""
            <svg height="100" width="100">
              <circle onclick="window.__CLICKED=42" cx="50" cy="50" r="40" stroke="black" stroke-width="3" fill="red" />
            </svg>
            """));
        await Browser.Locator("circle").TapAsync();
        Assert.That(await Browser.EvaluateAsync<int>("__CLICKED"), Is.EqualTo(42));
    }

    [Test]
    public async Task Should_click_the_button_if_window_Node_is_removed()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("() => { delete window.Node; }");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_on_a_span_with_an_inline_element_inside()
    {
        await Browser.GotoAsync(Content("""
            <style>span::before { content: 'q'; }</style>
            <span onclick='window.CLICKED=42'></span>
            """));
        await Browser.Locator("span").TapAsync();
        Assert.That(await Browser.EvaluateAsync<int>("CLICKED"), Is.EqualTo(42));
    }

    [TestCase("aligned", "")]
    [TestCase("half-aligned", "margin-left: 20.5px; margin-top: 11.5px;")]
    [TestCase("unaligned v1", "margin-left: 20.23px; margin-top: 11.65px;")]
    [TestCase("unaligned v2", "margin-left: 20.68px; margin-top: 11.13px;")]
    [TestCase("unaligned v3", "margin-left: 20.68px; margin-top: 11.52px;")]
    [TestCase("unaligned v4", "margin-left: 20.15px; margin-top: 11.24px;")]
    public async Task Should_click_the_1x1_div(string name, string style)
    {
        await Browser.GotoAsync(Content($"""<div style="{style} width: 1px; height: 1px;" onclick="window.__clicked = true"></div>"""));
        await Browser.Locator("div").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.__clicked"), Is.True, $"should click the {name} 1x1 div");
    }

    [Test]
    public async Task Should_click_the_button_after_navigation()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.Locator("button").TapAsync();
        await Browser.GotoAsync("/input/button.html");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_the_button_after_a_cross_origin_navigation()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.Locator("button").TapAsync();
        await Browser.GotoAsync($"{CROSS}/input/button.html");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_when_one_of_inline_box_children_is_outside_of_viewport()
    {
        await Browser.GotoAsync(Content("""
            <style>i { position: absolute; top: -1000px; }</style>
            <span onclick='window.CLICKED = 42;'><i>woof</i><b>doggo</b></span>
            """));
        await Browser.Locator("span").TapAsync();
        Assert.That(await Browser.EvaluateAsync<int>("CLICKED"), Is.EqualTo(42));
    }

    [Test]
    public async Task Should_click_offscreen_buttons()
    {
        await Browser.GotoAsync("/offscreenbuttons.html");
        await Browser.EvaluateAsync<object>("() => { window.messages = []; console.log = (text) => window.messages.push(text); }");
        for (var index = 0; index < 11; ++index)
        {
            await Browser.EvaluateAsync<object>("() => window.scrollTo(0, 0)");
            await Browser.Locator($"#btn{index}").TapAsync();
        }

        Assert.That(
            await Browser.EvaluateAsync<string[]>("messages"),
            Is.EqualTo(Enumerable.Range(0, 11).Select(index => $"button #{index} clicked").ToArray()));
    }

    [Test]
    public async Task Should_waitFor_display_none_to_be_gone()
    {
        await WaitForPropertyToBeGoneAsync("display", "none", "block");
    }

    [Test]
    public async Task Should_waitFor_visibility_hidden_to_be_gone()
    {
        await WaitForPropertyToBeGoneAsync("visibility", "hidden", "visible");
    }

    private async Task WaitForPropertyToBeGoneAsync(string property, string hidden, string shown)
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>($"() => {{ document.querySelector('button').style.{property} = '{hidden}'; }}");
        var done = false;
        var clicked = Task.Run(async () =>
        {
            await Browser.Locator("button").TapAsync(NoTimeout);
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(await ResultAsync(), Is.EqualTo("Was not clicked"));
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>($"() => {{ document.querySelector('button').style.{property} = '{shown}'; }}");
        await clicked;
        Assert.That(done, Is.True);
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_waitFor_visible_when_parent_is_hidden()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("() => { document.querySelector('button').parentElement.style.display = 'none'; }");
        var done = false;
        var clicked = Task.Run(async () =>
        {
            await Browser.Locator("button").TapAsync(NoTimeout);
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('button').parentElement.style.display = 'block'; }");
        await clicked;
        Assert.That(done, Is.True);
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_wrapped_links()
    {
        await Browser.GotoAsync("/wrappedlink.html");
        await Browser.Locator("a").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_on_checkbox_input_and_toggle()
    {
        await Browser.GotoAsync("/input/checkbox.html");
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.Null);
        await Browser.Locator("input#agree").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.True);
        Assert.That(
            await Browser.EvaluateAsync<string[]>("() => window.result.events"),
            Is.EqualTo(new[] { "mouseover", "mouseenter", "mousemove", "mousedown", "mouseup", "click", "input", "change" }));
        await Browser.Locator("input#agree").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.False);
    }

    [Test]
    public async Task Should_click_on_checkbox_label_and_toggle()
    {
        await Browser.GotoAsync("/input/checkbox.html");
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.Null);
        await Browser.Locator("label[for=\"agree\"]").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.True);
        Assert.That(await Browser.EvaluateAsync<string[]>("() => window.result.events"), Is.EqualTo(new[] { "click", "input", "change" }));
        await Browser.Locator("label[for=\"agree\"]").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool?>("() => window.result.check"), Is.False);
    }

    [Test]
    public async Task Should_scroll_and_click_the_button()
    {
        await Browser.GotoAsync("/input/scrollable.html");
        await Browser.Locator("#button-5").TapAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('#button-5').textContent"), Is.EqualTo("clicked"));
        await Browser.Locator("#button-80").TapAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('#button-80').textContent"), Is.EqualTo("clicked"));
    }

    [Test]
    public async Task Should_scroll_and_click_the_button_with_smooth_scroll_behavior()
    {
        await Browser.GotoAsync("/input/scrollable.html");
        await Browser.EvaluateAsync<object>("() => { const style = document.createElement('style'); style.textContent = 'html { scroll-behavior: smooth; }'; document.head.append(style); }");
        for (var index = 0; index < 5; index++)
        {
            await Browser.Locator("#button-80").TapAsync();
            Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('#button-80').textContent"), Is.EqualTo("clicked"));
            await Browser.Locator("#button-20").TapAsync();
            Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('#button-20').textContent"), Is.EqualTo("clicked"));
        }
    }

    [Test]
    public async Task Should_double_click_the_button()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("() => { window.double = false; document.querySelector('button').addEventListener('dblclick', () => { window.double = true; }); }");
        await Browser.Locator("button").DoubleTapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("double"), Is.True);
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_a_partially_obscured_button()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("() => { const button = document.querySelector('button'); button.textContent = 'Some really long text that will go offscreen'; button.style.position = 'absolute'; button.style.left = '368px'; }");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_a_rotated_button()
    {
        await Browser.GotoAsync("/input/rotatedButton.html");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_fire_contextmenu_event_on_right_click()
    {
        await Browser.GotoAsync("/input/scrollable.html");
        await Browser.Locator("#button-8").SecondaryTapAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('#button-8').textContent"), Is.EqualTo("context menu"));
    }

    [Test]
    public async Task Should_click_links_which_cause_navigation()
    {
        await Browser.GotoAsync(Content("<a href=\"/empty.html\">empty.html</a>"));
        await Browser.Locator("a").TapAsync();
        await Browser.WaitForURLAsync("/empty.html");
    }

    [Test]
    public async Task Should_click_the_button_inside_an_iframe()
    {
        await Browser.GotoAsync(Content("<div style=\"width:100px;height:100px\">spacer</div><iframe name=\"button-test\" src=\"/input/button.html\"></iframe>"));
        var frame = Browser.FrameLocator("iframe");
        await frame.Locator("button").TapAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('iframe').contentWindow.result"), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_the_button_behind_sticky_header()
    {
        await Browser.SetViewportAsync(500, 240);
        await Browser.GotoAsync(Content("""
            <style>
              * { padding: 0; margin: 0; }
              li { height: 80px; border: 1px solid black; }
              ol { padding-top: 160px; }
              div.fixed { position: fixed; z-index: 1001; width: 100%; background: red; height: 160px; }
            </style>
            <div class=fixed></div>
            <ol>
              <li>hi1</li><li>hi2</li><li>hi3</li><li>hi4</li><li>hi5</li><li>hi6</li><li>hi7</li><li>hi8</li>
              <li id=target onclick="window.__clicked = true">hi9</li>
              <li>hi10</li><li>hi11</li><li>hi12</li><li>hi13</li><li id=li14>hi14</li>
            </ol>
            """));
        await Browser.EvaluateAsync<object>("() => document.querySelector('#li14').scrollIntoView()");
        await Browser.Locator("#target").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.__clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_the_button_behind_position_absolute_header()
    {
        await Browser.SetViewportAsync(500, 240);
        await Browser.GotoAsync(Content("""
            <style>
              * { padding: 0; margin: 0; }
              li { height: 80px; border: 1px solid black; }
              ol { height: 100vh; overflow: scroll; padding-top: 160px; }
              body { position: relative; }
              div.fixed { position: absolute; top: 0; z-index: 1001; width: 100%; background: red; height: 160px; }
            </style>
            <ol>
              <li>hi1</li><li>hi2</li><li>hi3</li><li>hi4</li><li>hi5</li><li>hi6</li><li>hi7</li><li>hi8</li>
              <li id=target onclick="window.__clicked = true">hi9</li>
              <li>hi10</li><li>hi11</li><li>hi12</li><li>hi13</li><li id=li14>hi14</li>
            </ol>
            <div class=fixed>Overlay</div>
            """));
        await Browser.EvaluateAsync<object>("() => { const target = document.querySelector('#target'); document.querySelector('ol').scrollTo({ top: target.offsetTop, behavior: 'instant' }); }");
        await Browser.Locator("#target").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.__clicked"), Is.True);
    }

    [Test]
    public async Task Should_wait_for_stable_position()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("""
            () => {
              const button = document.querySelector('button');
              button.style.transition = 'margin 500ms linear 0s';
              button.style.marginLeft = '200px';
              button.style.borderWidth = '0';
              button.style.width = '200px';
              button.style.height = '20px';
              button.style.display = 'block';
              document.body.style.margin = '0';
            }
            """);
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
        Assert.That(await Browser.EvaluateAsync<double>("pageX"), Is.EqualTo(300));
        Assert.That(await Browser.EvaluateAsync<double>("pageY"), Is.EqualTo(10));
    }

    [Test]
    public async Task Should_wait_for_becoming_hit_target()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("""
            () => {
              const button = document.querySelector('button');
              button.style.borderWidth = '0';
              button.style.width = '200px';
              button.style.height = '20px';
              document.body.style.margin = '0';
              document.body.style.position = 'relative';
              const flyOver = document.createElement('div');
              flyOver.className = 'flyover';
              flyOver.style.position = 'absolute';
              flyOver.style.width = '400px';
              flyOver.style.height = '20px';
              flyOver.style.left = '-200px';
              flyOver.style.top = '0';
              flyOver.style.background = 'red';
              document.body.appendChild(flyOver);
            }
            """);
        var clicked = false;
        var tap = Task.Run(async () =>
        {
            await Browser.Locator("button").TapAsync(Ten);
            clicked = true;
        });
        Assert.That(clicked, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('.flyover').style.left = '0'; }");
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(clicked, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('.flyover').style.left = '200px'; }");
        await tap;
        Assert.That(clicked, Is.True);
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [TestCase("button", "<button onclick=\"window.__CLICKED=true;\" disabled><span>Click target</span></button>", "document.querySelector('button').removeAttribute('disabled')")]
    [TestCase("input", "<input onclick=\"window.__CLICKED=true;\" disabled>", "document.querySelector('input').removeAttribute('disabled')")]
    public async Task Should_wait_for_element_to_be_enabled(string name, string html, string enable)
    {
        await Browser.GotoAsync(Content(html));
        var done = false;
        var target = name == "button" ? Screen.GetByText("Click target") : Browser.Locator(name);
        var tap = Task.Run(async () =>
        {
            await target.TapAsync(Ten);
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(await Browser.EvaluateAsync<object>("() => window.__CLICKED"), Is.Null);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>($"() => {{ {enable}; }}");
        await tap;
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True, $"should wait for {name} to be enabled");
    }

    [Test]
    public async Task Should_wait_for_select_to_be_enabled()
    {
        await Browser.GotoAsync(Content("""
            <select disabled><option selected>Hello</option></select>
            <script>
              document.querySelector('select').addEventListener('mousedown', event => { window.__CLICKED = true; event.preventDefault(); });
            </script>
            """));
        var done = false;
        var tap = Task.Run(async () =>
        {
            await Browser.Locator("select").TapAsync(Ten);
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(await Browser.EvaluateAsync<object>("() => window.__CLICKED"), Is.Null);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('select').removeAttribute('disabled'); }");
        await tap;
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_click_disabled_div()
    {
        await Browser.GotoAsync(Content("<div onclick=\"window.__CLICKED=true\" disabled>Click target</div>"));
        await Screen.GetByText("Click target").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_wait_for_BUTTON_to_be_clickable_when_it_has_pointer_events_none()
    {
        await Browser.GotoAsync(Content("<button onclick=\"window.__CLICKED=true\" style=\"pointer-events:none\"><span>Click target</span></button>"));
        var done = false;
        var tap = Task.Run(async () =>
        {
            await Screen.GetByText("Click target").TapAsync(Ten);
            done = true;
        });
        await Browser.EvaluateAsync<object>(GiveItAChanceToClick);
        Assert.That(await Browser.EvaluateAsync<object>("() => window.__CLICKED"), Is.Null);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('button').style.removeProperty('pointer-events'); }");
        await tap;
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_update_modifiers_correctly()
    {
        await Browser.GotoAsync("/input/button.html");
        var button = Browser.Locator("button");
        await button.TapAsync(new ClickOptions { Modifiers = [KeyModifier.Shift] });
        Assert.That(await Browser.EvaluateAsync<bool>("shiftKey"), Is.True);
        await button.TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("shiftKey"), Is.False);
        await Browser.Keyboard.DownAsync("Shift");
        await button.TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("shiftKey"), Is.True);
        await Browser.Keyboard.UpAsync("Shift");
        await button.TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("shiftKey"), Is.False);
    }

    [Test]
    public async Task Should_click_an_offscreen_element_when_scroll_behavior_is_smooth()
    {
        await Browser.GotoAsync(Content("""
            <div style="border: 1px solid black; height: 500px; overflow: auto; width: 500px; scroll-behavior: smooth">
              <button style="margin-top: 2000px" onClick="window.clicked = true">hi</button>
            </div>
            """));
        await Browser.Locator("button").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_the_button_when_window_innerWidth_is_corrupted()
    {
        await Browser.GotoAsync("/input/button.html");
        await Browser.EvaluateAsync<object>("() => { Object.defineProperty(window, 'innerWidth', { value: 0 }); }");
        await Browser.Locator("button").TapAsync();
        Assert.That(await ResultAsync(), Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Should_click_zero_sized_input_by_label()
    {
        await Browser.GotoAsync(Content("""
            <label>
              Click me
              <input onclick="window.__clicked=true" style="width:0;height:0;padding:0;margin:0;border:0;">
            </label>
            """));
        await Screen.GetByText("Click me").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.__clicked"), Is.True);
    }

    [Test]
    public async Task Should_climb_dom_for_inner_label_with_pointer_events_none()
    {
        await Browser.GotoAsync(Content("<button onclick=\"window.__CLICKED=true;\"><label style=\"pointer-events:none\">Click target</label></button>"));
        await Screen.GetByText("Click target").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_climb_up_to_role_button()
    {
        await Browser.GotoAsync(Content("<div role=button onclick=\"window.__CLICKED=true;\"><div style=\"pointer-events:none\"><span><div>Click target</div></span></div>"));
        await Screen.GetByText("Click target").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_climb_up_to_a_anchor()
    {
        await Browser.GotoAsync(Content("<a href=\"#\" onclick=\"window.__CLICKED=true\" id=\"outer\"><div id=\"intermediate\"><div id=\"inner\" style=\"pointer-events: none\">Inner</div></div></a>"));
        await Browser.Locator("#inner").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_climb_up_to_a_role_link()
    {
        await Browser.GotoAsync(Content("<div role=link onclick=\"window.__CLICKED=true\" id=\"outer\"><div id=\"inner\" style=\"pointer-events: none\">Inner</div></div>"));
        await Browser.Locator("#inner").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("__CLICKED"), Is.True);
    }

    [Test]
    public async Task Should_click_in_an_iframe_with_border()
    {
        await Browser.GotoAsync(Content("""
            <style>
              body, html, iframe { margin: 0; padding: 0; border: none; box-sizing: border-box; }
              iframe { border: 4px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; }
            </style>
            <iframe srcdoc="
              <style>
                body, html { margin: 0; padding: 0; }
                div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
              </style>
              <div>Target</div>
              <script>
                document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
              </script>
            "></iframe>
            """));
        await Browser.FrameLocator("iframe").Locator("div").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_in_a_transformed_iframe()
    {
        await Browser.GotoAsync(Content("""
            <style>
              body, html, iframe { margin: 0; padding: 0; border: none; }
              iframe {
                border: 4px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px;
                transform: translate(100px, 100px) scale(1.2) rotate3d(1, 1, 1, 25deg);
              }
            </style>
            <iframe srcdoc="
              <style>
                body, html { margin: 0; padding: 0; }
                div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
              </style>
              <div>Target</div>
              <script>
                document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
              </script>
            "></iframe>
            """));
        await Browser.FrameLocator("iframe").Locator("div").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_in_a_nested_transformed_iframe()
    {
        await Browser.GotoAsync(Content("""
            <style>
              body, html, iframe { margin: 0; padding: 0; box-sizing: border-box; }
              iframe { border: 1px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; transform: scale(0.8); }
            </style>
            <iframe srcdoc="
              <style>
                body, html, iframe { margin: 0; padding: 0; box-sizing: border-box; }
                iframe { border: 3px solid black; background: gray; margin-left: 18px; margin-top: 14px; width: 200px; height: 200px; transform: scale(0.7); }
              </style>
              <iframe srcdoc='
                <style>
                  div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
                </style>
                <div onclick=&quot;window.top._clicked = true&quot;>Target</div>
              '></iframe>
            "></iframe>
            """));
        await Browser.FrameLocator("iframe").FrameLocator("iframe").Locator("div").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_if_opened_select_covers_the_button()
    {
        await Browser.GotoAsync(Content("""
            <div>
              <select>
                <option>very long text #1</option><option>very long text #2</option><option>very long text #3</option>
                <option>very long text #4</option><option>very long text #5</option><option>very long text #6</option>
              </select>
            </div>
            <div><button onclick="window.__CLICKED=42">clickme</button></div>
            """));
        await Browser.Locator("select").TapAsync();
        await Browser.Locator("button").TapAsync();
        Assert.That(await Browser.EvaluateAsync<int>("window.__CLICKED"), Is.EqualTo(42));
    }

    [Test]
    public async Task Should_fire_contextmenu_event_on_right_click_in_correct_order()
    {
        await Browser.GotoAsync(Content("<button id=\"target\">Click me</button>"));
        await Browser.EvaluateAsync<object>("() => { window.entries = []; const log = (event) => window.entries.push(event.type); for (const type of ['mousedown', 'mouseup', 'contextmenu']) document.addEventListener(type, log); }");
        await Screen.GetByRole("button", "Click me").SecondaryTapAsync();
        await Expect.Poll(() => Browser.EvaluateAsync<string[]>("entries")).ToMatchAsync(Is.EqualTo(new[] { "mousedown", "contextmenu", "mouseup" }));
    }

    [Test]
    public async Task Should_click_after_a_right_click()
    {
        await Browser.GotoAsync(Content("""
            <button>Click me</button>
            <script>
              const button = document.querySelector('button');
              button.addEventListener('click', () => button.textContent = 'Clicked!');
            </script>
            """));
        await Screen.GetByRole("button").SecondaryTapAsync();
        await Screen.GetByRole("button").TapAsync();
        await Expect.That(Screen.GetByRole("button")).ToHaveTextAsync("Clicked!");
    }

    [Test]
    public async Task Should_click_into_shadow_root_with_slotted_div()
    {
        await Browser.GotoAsync(Content("""
            <my-button>
              <template shadowrootmode="open"><button onclick="window.__clicked = true"><slot></slot></button></template>
              <div>Foo</div>
            </my-button>
            """));
        await Screen.GetByRole("button", "Foo").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.__clicked"), Is.True);
    }

    [Test]
    public async Task Should_click_shadow_root_button()
    {
        await Browser.GotoAsync(Content("""
            <my-button>
              <template shadowrootmode="open"><button onclick="window.__clicked = true"><slot></slot></button></template>
              <div>Foo</div>
            </my-button>
            """));
        await Browser.Locator("my-button").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.__clicked"), Is.True);
    }
}
