// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Keyboard;

/// <summary>
/// <c>test.describe('page keyboard')</c> of upstream's <c>apps/testbed/tests/keyboard.e2e.ts</c> (Playwright's <c>page-keyboard.spec.ts</c>).
/// <c>PressSequentiallyAsync</c> stands for <c>page.type(selector, text)</c>, and an element press of a key <c>Locator.PressAsync</c> does
/// not name (a code like <c>Digit5</c>) is a focus and a keyboard press. Not ported, as upstream: should send a character with insertText,
/// insertText should only emit input event (no <c>keyboard.insertText</c>); should expose keyIdentifier in webkit (WebKit only); should
/// dispatch insertText after context menu was opened (no <c>keyboard.insertText</c>, and <c>mouse.down</c> presses only the primary button);
/// should type after context menu was opened (<c>mouse.down</c> presses only the primary button).
/// </summary>
public sealed class PageKeyboardTests : PlaywrightTest
{
    /// <summary>Records the last keydown in <c>window.lastEvent</c>, Playwright's <c>captureLastKeydown</c>.</summary>
    private const string CaptureLastKeydown = """
        () => {
          const lastEvent = { repeat: false, location: -1, code: '', key: '', metaKey: false, keyIdentifier: 'unsupported' };
          window.lastEvent = lastEvent;
          document.addEventListener('keydown', (e) => {
            lastEvent.repeat = e.repeat;
            lastEvent.location = e.location;
            lastEvent.key = e.key;
            lastEvent.code = e.code;
            lastEvent.metaKey = e.metaKey;
            lastEvent.keyIdentifier = 'keyIdentifier' in e && typeof e.keyIdentifier === 'string' && e.keyIdentifier;
          }, true);
        }
        """;

    private static readonly bool IsMac = OperatingSystem.IsMacOS();

    private async Task<string> TextareaValueAsync() =>
        (await Browser.EvaluateAsync<string>("() => document.querySelector('textarea').value"))!;

    private async Task<string> ResultAsync() => (await Browser.EvaluateAsync<string>("getResult()"))!;

    private static string Lines(params string[] lines) => string.Join('\n', lines);

    [Test]
    public async Task Should_type_into_a_textarea()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("() => { const textarea = document.createElement('textarea'); document.body.appendChild(textarea); textarea.focus(); }");
        const string text = "Hello world. I am the text that was typed!";
        await Browser.Keyboard.TypeAsync(text);
        Assert.That(await TextareaValueAsync(), Is.EqualTo(text));
    }

    [Test]
    public async Task Should_move_with_the_arrow_keys()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").PressSequentiallyAsync("Hello World!");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("Hello World!"));
        for (var i = 0; i < "World!".Length; i++)
        {
            await Browser.Keyboard.PressAsync("ArrowLeft");
        }

        await Browser.Keyboard.TypeAsync("inserted ");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("Hello inserted World!"));
        await Browser.Keyboard.DownAsync("Shift");
        for (var i = 0; i < "inserted ".Length; i++)
        {
            await Browser.Keyboard.PressAsync("ArrowLeft");
        }

        await Browser.Keyboard.UpAsync("Shift");
        await Browser.Keyboard.PressAsync("Backspace");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("Hello World!"));
    }

    [Test]
    public async Task Should_send_a_character_with_ElementHandle_press()
    {
        await Browser.GotoAsync("/input/textarea.html");
        var textarea = Browser.Locator("textarea");
        await textarea.PressAsync("a");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("a"));

        await Browser.EvaluateAsync<object>("() => window.addEventListener('keydown', (e) => e.preventDefault(), true)");

        await textarea.PressAsync("b");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("a"));
    }

    [Test]
    public async Task Should_not_type_text_with_Alt_modifier()
    {
        await Browser.GotoAsync(Content("<input><textarea></textarea><div contenteditable></div>"));
        foreach (var locator in new[] { Browser.Locator("input"), Browser.Locator("textarea"), Browser.Locator("div") })
        {
            await locator.FocusAsync();
            await Browser.Keyboard.PressAsync("Alt+KeyA");
            await Browser.Keyboard.PressAsync("Alt+Digit1");
            await Browser.Keyboard.PressAsync("Alt+Shift+KeyA");
            await Browser.Keyboard.DownAsync("Alt");
            await Browser.Keyboard.PressAsync("b");
            await Browser.Keyboard.UpAsync("Alt");
            await locator.PressAsync("c");
        }

        await Expect.That(Browser.Locator("input")).ToHaveValueAsync("c");
        await Expect.That(Browser.Locator("textarea")).ToHaveValueAsync("c");
        await Expect.That(Browser.Locator("div")).ToHaveTextAsync("c");
    }

    [Test]
    public async Task Should_emit_keydown_keypress_textInput_and_input_when_typing_a_character()
    {
        await Browser.GotoAsync(Content("<input>"));
        await Browser.EvaluateAsync<object>("""
            () => {
              window.events = [];
              for (const type of ['keydown', 'keypress', 'textInput', 'input', 'keyup'])
                document.querySelector('input').addEventListener(type, () => window.events.push(type));
            }
            """);
        await Browser.Locator("input").FocusAsync();
        await Browser.Keyboard.PressAsync("f");
        Assert.That(await Browser.EvaluateAsync<string[]>("events"), Is.EqualTo(new[] { "keydown", "keypress", "textInput", "input", "keyup" }));
    }

    [Test]
    public async Task Should_dispatch_key_events_in_separate_tasks()
    {
        await Browser.GotoAsync(Content("<input>"));
        await Browser.EvaluateAsync<object>("""
            () => {
              window.log = [];
              const input = document.querySelector('input');
              for (const type of ['keydown', 'keypress'])
                input.addEventListener(type, () => { window.log.push(type); queueMicrotask(() => window.log.push('microtask-' + type)); });
              input.focus();
            }
            """);
        await Browser.Keyboard.PressAsync("a");
        Assert.That(await Browser.EvaluateAsync<string[]>("log"), Is.EqualTo(new[] { "keydown", "microtask-keydown", "keypress", "microtask-keypress" }));
    }

    [Test]
    public async Task Should_report_shiftKey()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        var keyboard = Browser.Keyboard;
        foreach (var modifierKey in new[] { "Shift", "Alt", "Control" })
        {
            await keyboard.DownAsync(modifierKey);
            Assert.That(await ResultAsync(), Is.EqualTo("Keydown: " + modifierKey + " " + modifierKey + "Left LEFT [" + modifierKey + "]"));
            await keyboard.DownAsync("!");
            if (modifierKey == "Shift")
            {
                Assert.That(await ResultAsync(), Is.EqualTo("Keydown: ! Digit1 STANDARD [" + modifierKey + "]\nKeypress: ! Digit1 STANDARD 33 [" + modifierKey + "]"));
            }
            else
            {
                Assert.That(await ResultAsync(), Is.EqualTo("Keydown: ! Digit1 STANDARD [" + modifierKey + "]"));
            }

            await keyboard.UpAsync("!");
            Assert.That(await ResultAsync(), Is.EqualTo("Keyup: ! Digit1 STANDARD [" + modifierKey + "]"));
            await keyboard.UpAsync(modifierKey);
            Assert.That(await ResultAsync(), Is.EqualTo("Keyup: " + modifierKey + " " + modifierKey + "Left LEFT []"));
        }
    }

    [Test]
    public async Task Should_report_multiple_modifiers()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        var keyboard = Browser.Keyboard;
        await keyboard.DownAsync("Control");
        Assert.That(await ResultAsync(), Is.EqualTo("Keydown: Control ControlLeft LEFT [Control]"));
        await keyboard.DownAsync("Alt");
        Assert.That(await ResultAsync(), Is.EqualTo("Keydown: Alt AltLeft LEFT [Alt Control]"));
        await keyboard.DownAsync(";");
        Assert.That(await ResultAsync(), Is.EqualTo("Keydown: ; Semicolon STANDARD [Alt Control]"));
        await keyboard.UpAsync(";");
        Assert.That(await ResultAsync(), Is.EqualTo("Keyup: ; Semicolon STANDARD [Alt Control]"));
        await keyboard.UpAsync("Control");
        Assert.That(await ResultAsync(), Is.EqualTo("Keyup: Control ControlLeft LEFT [Alt]"));
        await keyboard.UpAsync("Alt");
        Assert.That(await ResultAsync(), Is.EqualTo("Keyup: Alt AltLeft LEFT []"));
    }

    [Test]
    public async Task Should_send_proper_codes_while_typing()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.TypeAsync("!");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines("Keydown: ! Digit1 STANDARD []", "Keypress: ! Digit1 STANDARD 33 []", "Keyup: ! Digit1 STANDARD []")));
        await Browser.Keyboard.TypeAsync("^");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines("Keydown: ^ Digit6 STANDARD []", "Keypress: ^ Digit6 STANDARD 94 []", "Keyup: ^ Digit6 STANDARD []")));
    }

    [Test]
    public async Task Should_send_proper_codes_while_typing_with_shift()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        var keyboard = Browser.Keyboard;
        await keyboard.DownAsync("Shift");
        await Browser.Keyboard.TypeAsync("~");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines(
                "Keydown: Shift ShiftLeft LEFT [Shift]",
                "Keydown: ~ Backquote STANDARD [Shift]",
                "Keypress: ~ Backquote STANDARD 126 [Shift]",
                "Keyup: ~ Backquote STANDARD [Shift]")));
        await keyboard.UpAsync("Shift");
    }

    [Test]
    public async Task Should_not_type_canceled_events()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").FocusAsync();
        await Browser.EvaluateAsync<object>("""
            () => {
              window.addEventListener('keydown', (event) => {
                event.stopPropagation();
                event.stopImmediatePropagation();
                if (event.key === 'l')
                  event.preventDefault();
                if (event.key === 'o')
                  event.preventDefault();
              }, false);
            }
            """);
        await Browser.Keyboard.TypeAsync("Hello World!");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("He Wrd!"));
    }

    [Test]
    public async Task Should_press_plus()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("+");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines("Keydown: + Equal STANDARD []", "Keypress: + Equal STANDARD 43 []", "Keyup: + Equal STANDARD []")));
    }

    [Test]
    public async Task Should_press_shift_plus()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("Shift++");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines(
                "Keydown: Shift ShiftLeft LEFT [Shift]",
                "Keydown: + Equal STANDARD [Shift]",
                "Keypress: + Equal STANDARD 43 [Shift]",
                "Keyup: + Equal STANDARD [Shift]",
                "Keyup: Shift ShiftLeft LEFT []")));
    }

    [Test]
    public async Task Should_support_plus_separated_modifiers()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("Shift+~");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines(
                "Keydown: Shift ShiftLeft LEFT [Shift]",
                "Keydown: ~ Backquote STANDARD [Shift]",
                "Keypress: ~ Backquote STANDARD 126 [Shift]",
                "Keyup: ~ Backquote STANDARD [Shift]",
                "Keyup: Shift ShiftLeft LEFT []")));
    }

    [Test]
    public async Task Should_support_multiple_plus_separated_modifiers()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("Control+Shift+~");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines(
                "Keydown: Control ControlLeft LEFT [Control]",
                "Keydown: Shift ShiftLeft LEFT [Control Shift]",
                "Keydown: ~ Backquote STANDARD [Control Shift]",
                "Keyup: ~ Backquote STANDARD [Control Shift]",
                "Keyup: Shift ShiftLeft LEFT [Control]",
                "Keyup: Control ControlLeft LEFT []")));
    }

    [Test]
    public async Task Should_shift_raw_codes()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("Shift+Digit3");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines(
                "Keydown: Shift ShiftLeft LEFT [Shift]",
                "Keydown: # Digit3 STANDARD [Shift]",
                "Keypress: # Digit3 STANDARD 35 [Shift]",
                "Keyup: # Digit3 STANDARD [Shift]",
                "Keyup: Shift ShiftLeft LEFT []")));
    }

    [Test]
    public async Task Should_specify_repeat_property()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").FocusAsync();
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        await Browser.Keyboard.DownAsync("a");
        Assert.That(await Browser.EvaluateAsync<bool>("lastEvent.repeat"), Is.False);
        await Browser.Keyboard.PressAsync("a");
        Assert.That(await Browser.EvaluateAsync<bool>("lastEvent.repeat"), Is.True);

        await Browser.Keyboard.DownAsync("b");
        Assert.That(await Browser.EvaluateAsync<bool>("lastEvent.repeat"), Is.False);
        await Browser.Keyboard.DownAsync("b");
        Assert.That(await Browser.EvaluateAsync<bool>("lastEvent.repeat"), Is.True);

        await Browser.Keyboard.UpAsync("a");
        await Browser.Keyboard.DownAsync("a");
        Assert.That(await Browser.EvaluateAsync<bool>("lastEvent.repeat"), Is.False);
    }

    [Test]
    public async Task Should_type_all_kinds_of_characters()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").FocusAsync();
        const string text = "This text goes onto two lines.\nThis character is 嗨.";
        await Browser.Keyboard.TypeAsync(text);
        Assert.That(await TextareaValueAsync(), Is.EqualTo(text));
    }

    /// <summary>Reads <c>lastEvent</c>'s location, key, and code and checks them together, Playwright's three <c>expect.soft</c> calls.</summary>
    private async Task ExpectLastKeydownAsync(int location, string key, string code)
    {
        var actualLocation = await Browser.EvaluateAsync<int>("lastEvent.location");
        var actualKey = await Browser.EvaluateAsync<string>("lastEvent.key");
        var actualCode = await Browser.EvaluateAsync<string>("lastEvent.code");
        Assert.Multiple(() =>
        {
            Assert.That(actualLocation, Is.EqualTo(location));
            Assert.That(actualKey, Is.EqualTo(key));
            Assert.That(actualCode, Is.EqualTo(code));
        });
    }

    [Test]
    public async Task Should_specify_location()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        var textarea = Browser.Locator("textarea");

        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("Digit5");
        await ExpectLastKeydownAsync(0, "5", "Digit5");

        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("ControlLeft");
        await ExpectLastKeydownAsync(1, "Control", "ControlLeft");

        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("ControlRight");
        await ExpectLastKeydownAsync(2, "Control", "ControlRight");

        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("NumpadSubtract");
        await ExpectLastKeydownAsync(3, "-", "NumpadSubtract");
    }

    [Test]
    public async Task Should_specify_numpad_location_on_keyup()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("NumpadSubtract");
        Assert.That(
            await ResultAsync(),
            Is.EqualTo(Lines("Keydown: - NumpadSubtract NUMPAD []", "Keypress: - NumpadSubtract NUMPAD 45 []", "Keyup: - NumpadSubtract NUMPAD []")));
    }

    [Test]
    public async Task Should_press_NumpadDecimal_as_Delete()
    {
        await Browser.GotoAsync(Content("<textarea></textarea>"));
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        var textarea = Browser.Locator("textarea");

        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("NumpadDecimal");
        Assert.That(await Browser.EvaluateAsync<string>("lastEvent.key"), Is.EqualTo("Delete"));
        Assert.That(await Browser.EvaluateAsync<string>("lastEvent.code"), Is.EqualTo("NumpadDecimal"));
        Assert.That(await Browser.EvaluateAsync<int>("lastEvent.location"), Is.EqualTo(3));
        await Expect.That(textarea).ToHaveValueAsync("");
    }

    [Test]
    public async Task Should_type_numpad_digits_with_Shift()
    {
        await Browser.GotoAsync(Content("<textarea></textarea>"));
        var textarea = Browser.Locator("textarea");
        await textarea.FocusAsync();
        await Browser.Keyboard.PressAsync("Shift+Numpad1");
        await Browser.Keyboard.PressAsync("Shift+NumpadDecimal");
        await Browser.Keyboard.PressAsync("Shift+Numpad9");
        await Expect.That(textarea).ToHaveValueAsync("1.9");
    }

    [Test]
    public async Task Should_press_Enter()
    {
        await Browser.GotoAsync(Content("<textarea></textarea>"));
        await Browser.Locator("textarea").FocusAsync();
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        foreach (var (key, expectedKey, expectedCode) in new[]
        {
            ("Enter", "Enter", "Enter"),
            ("NumpadEnter", "Enter", "NumpadEnter"),
            ("\n", "Enter", "Enter"),
            ("\r", "Enter", "Enter"),
        })
        {
            await Browser.Keyboard.PressAsync(key);
            Assert.That(await Browser.EvaluateAsync<string>("lastEvent.key"), Is.EqualTo(expectedKey));
            Assert.That(await Browser.EvaluateAsync<string>("lastEvent.code"), Is.EqualTo(expectedCode));
            Assert.That(await TextareaValueAsync(), Is.EqualTo("\n"));
            await Browser.EvaluateAsync<object>("() => { document.querySelector('textarea').value = ''; }");
        }
    }

    [Test]
    public async Task Should_press_audio_and_media_control_keys()
    {
        await Browser.GotoAsync(Content("<input autofocus>"));
        await Browser.Locator("input").FocusAsync();
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        foreach (var mediaKey in new[] { "AudioVolumeMute", "AudioVolumeDown", "AudioVolumeUp", "MediaTrackNext", "MediaTrackPrevious", "MediaPlayPause" })
        {
            await Browser.Keyboard.PressAsync(mediaKey);
            await ExpectLastKeydownAsync(0, mediaKey, mediaKey);
        }
    }

    [Test]
    public async Task Should_throw_on_unknown_keys()
    {
        await Browser.GotoAsync("/empty.html");
        foreach (var key in new[] { "NotARealKey", "ё", "😊" })
        {
            var error = await Assert.CatchAsync(() => Browser.Keyboard.PressAsync(key));
            Assert.That(error!.Message, Does.Contain($"Unknown key: \"{key}\""));
        }
    }

    [Test]
    public async Task Should_type_emoji()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").PressSequentiallyAsync("👹 Tokyo street Japan 🇯🇵");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("👹 Tokyo street Japan 🇯🇵"));
    }

    [Test]
    public async Task Should_type_emoji_into_an_iframe()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("""
            () => new Promise((resolve) => {
              const frame = document.createElement('iframe');
              frame.id = 'emoji-test';
              frame.src = '/input/textarea.html';
              frame.onload = () => resolve();
              document.body.appendChild(frame);
            })
            """);
        await Browser.FrameLocator("#emoji-test").Locator("textarea").PressSequentiallyAsync("👹 Tokyo street Japan 🇯🇵");
        Assert.That(
            await Browser.EvaluateAsync<string>("() => document.querySelector('#emoji-test').contentDocument.querySelector('textarea').value"),
            Is.EqualTo("👹 Tokyo street Japan 🇯🇵"));
    }

    [Test]
    public async Task Should_handle_selectAll()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").PressSequentiallyAsync("some text");
        await Browser.Keyboard.DownAsync("ControlOrMeta");
        await Browser.Keyboard.PressAsync("a");
        await Browser.Keyboard.UpAsync("ControlOrMeta");
        await Browser.Keyboard.PressAsync("Backspace");
        Assert.That(await TextareaValueAsync(), Is.EqualTo(""));
    }

    [Test]
    public async Task Pressing_Meta_should_not_result_in_any_text_insertion_on_any_platform()
    {
        await Browser.GotoAsync(Content("<input type=\"text\" value=\"hello world\">"));
        var input = Browser.Locator("input");
        await Expect.That(input).ToHaveValueAsync("hello world");
        await input.FocusAsync();
        await Browser.Keyboard.PressAsync("Meta");
        await Expect.That(input).ToHaveValueAsync("hello world");
    }

    [Test]
    public async Task Should_be_able_to_prevent_selectAll()
    {
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").PressSequentiallyAsync("some text");
        await Browser.EvaluateAsync<object>("""
            () => {
              document.querySelector('textarea').addEventListener('keydown', (event) => {
                if (event.key === 'a' && (event.metaKey || event.ctrlKey))
                  event.preventDefault();
              }, false);
            }
            """);
        await Browser.Keyboard.DownAsync("ControlOrMeta");
        await Browser.Keyboard.PressAsync("a");
        await Browser.Keyboard.UpAsync("ControlOrMeta");
        await Browser.Keyboard.PressAsync("Backspace");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("some tex"));
    }

    [Test]
    public async Task Should_support_MacOS_shortcuts()
    {
        Assume.That(IsMac, Is.True, "macOS only");
        await Browser.GotoAsync("/input/textarea.html");
        await Browser.Locator("textarea").PressSequentiallyAsync("some text");
        await Browser.Keyboard.PressAsync("Shift+Control+Alt+KeyB");
        await Browser.Keyboard.PressAsync("Backspace");
        Assert.That(await TextareaValueAsync(), Is.EqualTo("some "));
    }

    [Test]
    public async Task Should_press_the_meta_key()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        await Browser.Keyboard.PressAsync("Meta");
        var last = (await Browser.EvaluateAsync<LastKeydown>("lastEvent"))!;
        Assert.That(last.Key, Is.EqualTo("Meta"));
        Assert.That(last.Code, Is.EqualTo("MetaLeft"));
        Assert.That(last.MetaKey, Is.True);
    }

    [Test]
    public async Task Should_work_with_keyboard_events_with_empty_html()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        await Browser.Keyboard.PressAsync("a");
        Assert.That(await Browser.EvaluateAsync<string>("lastEvent.key"), Is.EqualTo("a"));
    }

    [Test]
    public async Task Should_work_after_a_cross_origin_navigation()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync($"{CROSS}/empty.html");
        await Browser.EvaluateAsync<object>(CaptureLastKeydown);
        await Browser.Keyboard.PressAsync("a");
        Assert.That(await Browser.EvaluateAsync<string>("lastEvent.key"), Is.EqualTo("a"));
    }

    [Test]
    public async Task Should_scroll_with_PageDown()
    {
        await Browser.GotoAsync("/input/scrollable.html");
        await Browser.Locator("body").TapAsync();
        await Browser.Keyboard.PressAsync("PageDown");
        await Expect.Poll(() => Browser.EvaluateAsync<bool>("scrollY > 0")).ToBeAsync(true);
    }

    [Test]
    public async Task Should_move_around_the_selection_in_a_contenteditable()
    {
        await Browser.GotoAsync(Content("<div contenteditable></div>"));
        await Browser.Locator("div").FocusAsync();
        var modifier = IsMac ? "Alt" : "Control";
        await Browser.Keyboard.TypeAsync("Hello World");
        await Browser.Keyboard.DownAsync(modifier);
        await Browser.Keyboard.DownAsync("Shift");
        await Browser.Keyboard.PressAsync("ArrowLeft");
        await Browser.Keyboard.UpAsync("Shift");
        await Browser.Keyboard.UpAsync(modifier);
        Assert.That(await Browser.EvaluateAsync<string>("() => window.getSelection().toString()"), Is.EqualTo("World"));
    }

    [Test]
    public async Task Should_move_to_the_start_of_the_document()
    {
        Assume.That(IsMac, Is.True, "macOS only");
        await Browser.GotoAsync(Content("<div contenteditable></div>"));
        await Browser.Locator("div").FocusAsync();
        await Browser.Keyboard.TypeAsync("1\n2\n3\n");
        await Browser.Keyboard.PressAsync("Shift+Meta+ArrowUp");
        Assert.That(await Browser.EvaluateAsync<string>("() => window.getSelection().toString()"), Is.EqualTo("1\n2\n3\n"));
    }

    [Test]
    public async Task Should_dispatch_a_click_event_on_a_button_when_Space_gets_pressed()
    {
        await Browser.GotoAsync(Content("<button type=\"button\">a11y</button>"));
        await Browser.EvaluateAsync<object>("() => { window.actual = { clicked: false }; document.querySelector('button').addEventListener('click', () => (window.actual.clicked = true)); }");
        await Browser.Locator("button").FocusAsync();
        await Browser.Keyboard.PressAsync("Space");
        Assert.That(await Browser.EvaluateAsync<bool>("actual.clicked"), Is.True);
    }

    [Test]
    public async Task Should_dispatch_a_click_event_on_a_button_when_Enter_gets_pressed()
    {
        await Browser.GotoAsync(Content("<button type=\"button\">a11y</button>"));
        await Browser.EvaluateAsync<object>("() => { window.actual = { clicked: false }; document.querySelector('button').addEventListener('click', () => (window.actual.clicked = true)); }");
        await Browser.Locator("button").FocusAsync();
        await Browser.Keyboard.PressAsync("Enter");
        Assert.That(await Browser.EvaluateAsync<bool>("actual.clicked"), Is.True);
    }

    [Test]
    public async Task Should_support_simple_copy_pasting()
    {
        await Browser.GotoAsync(Content("<div contenteditable>123</div>"));
        await Browser.Locator("div").FocusAsync();
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyA");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyC");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyV");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyV");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div').textContent"), Is.EqualTo("123123"));
    }

    [Test]
    public async Task Should_support_simple_cut_pasting()
    {
        await Browser.GotoAsync(Content("<div contenteditable>123</div>"));
        await Browser.Locator("div").FocusAsync();
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyA");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyX");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyV");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyV");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('div').textContent"), Is.EqualTo("123123"));
    }

    [Test]
    public async Task Should_support_undo_redo()
    {
        await Browser.GotoAsync(Content("<div contenteditable></div>"));
        var div = Browser.Locator("div");
        await Expect.That(div).ToHaveTextAsync("");
        await div.PressSequentiallyAsync("123");
        await Expect.That(div).ToHaveTextAsync("123");
        await Browser.Keyboard.PressAsync("ControlOrMeta+KeyZ");
        await Expect.That(div).ToHaveTextAsync("");
        await Browser.Keyboard.PressAsync("Shift+ControlOrMeta+KeyZ");
        await Expect.That(div).ToHaveTextAsync("123");
    }

    /// <summary>A page with a <c>shadow-element</c> whose shadow root holds <paramref name="editors"/>, for the typing-in-shadow-dom tests.</summary>
    private static string ShadowElementPage(string editors) => $$"""
        <html>
          <body>
            <shadow-element></shadow-element>
            <script>
              customElements.define('shadow-element', class extends HTMLElement {
                constructor() {
                  super();
                  this.attachShadow({ mode: 'open' });
                }

                connectedCallback() {
                  this.shadowRoot.innerHTML = `
                    <style>
                      .editor { padding: 1rem; margin: 1rem; border: 1px solid #ccc; }
                    </style>
                    {{editors}}
                  `;
                }
              });
            </script>
          </body>
        </html>
        """;

    [Test]
    public async Task Should_type_repeatedly_in_contenteditable_in_shadow_dom()
    {
        await Browser.GotoAsync(Content(ShadowElementPage("""
            <div class=editor contenteditable id=foo></div>
            <hr>
            <section>
              <div class=editor contenteditable id=bar></div>
            </section>
            """)));

        var editor = Browser.Locator("shadow-element > .editor").First();
        await editor.PressSequentiallyAsync("This is the first box.");

        var sectionEditor = Browser.Locator("section .editor");
        await sectionEditor.PressSequentiallyAsync("This is the second box.");

        Assert.That(await editor.TextContentAsync(), Is.EqualTo("This is the first box."));
        Assert.That(await sectionEditor.TextContentAsync(), Is.EqualTo("This is the second box."));
    }

    [Test]
    public async Task Should_type_repeatedly_in_contenteditable_in_shadow_dom_with_nested_elements()
    {
        await Browser.GotoAsync(Content(ShadowElementPage("""
            <div class=editor contenteditable id=foo><p>hello</p></div>
            <hr>
            <section>
              <div class=editor contenteditable id=bar><p>world</p></div>
            </section>
            """)));

        var editor = Browser.Locator("shadow-element > .editor").First();
        await editor.PressSequentiallyAsync("This is the first box: ");

        var sectionEditor = Browser.Locator("section .editor");
        await sectionEditor.PressSequentiallyAsync("This is the second box: ");

        Assert.That(await editor.TextContentAsync(), Is.EqualTo("This is the first box: hello"));
        Assert.That(await sectionEditor.TextContentAsync(), Is.EqualTo("This is the second box: world"));
    }

    [Test]
    public async Task Should_type_repeatedly_in_input_in_shadow_dom()
    {
        await Browser.GotoAsync(Content(ShadowElementPage("""
            <input class=editor id=foo>
            <hr>
            <section>
              <input class=editor id=bar>
            </section>
            """)));

        var editor = Browser.Locator("shadow-element > .editor").First();
        await editor.PressSequentiallyAsync("This is the first box.");

        var sectionEditor = Browser.Locator("section .editor");
        await sectionEditor.PressSequentiallyAsync("This is the second box.");

        Assert.That(await editor.InputValueAsync(), Is.EqualTo("This is the first box."));
        Assert.That(await sectionEditor.InputValueAsync(), Is.EqualTo("This is the second box."));
    }

    [Test]
    public async Task Type_to_non_focusable_element_should_maintain_old_focus()
    {
        await Browser.GotoAsync(Content("""
            <div id="focusable" tabindex="0">focusable div</div>
            <div id="non-focusable-and-non-editable">non-editable, non-focusable</div>
            """));

        await Browser.Locator("#focusable").FocusAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => document.activeElement?.id"), Is.EqualTo("focusable"));
        await Browser.Locator("#non-focusable-and-non-editable").PressSequentiallyAsync("foo");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.activeElement?.id"), Is.EqualTo("focusable"));
    }

    [Test]
    public async Task Should_have_correct_Keydown_Keyup_order_when_pressing_Escape_key()
    {
        await Browser.GotoAsync("/input/keyboard.html");
        await Browser.Keyboard.PressAsync("Escape");
        Assert.That(await ResultAsync(), Is.EqualTo(Lines("Keydown: Escape Escape STANDARD []", "Keyup: Escape Escape STANDARD []")));
    }

    [Test]
    public async Task Should_close_dialog_on_Escape_key_press_in_contenteditable()
    {
        await Browser.GotoAsync(Content("""
            <dialog>
              <div contenteditable>Edit Me</div>
            </dialog>
            """));
        var widget = Browser.Locator("dialog [contenteditable]");
        await Browser.EvaluateAsync<object>("() => document.querySelector('dialog').showModal()");
        await Expect.Poll(() => Browser.EvaluateAsync<bool>("() => document.querySelector('dialog').open")).ToBeAsync(true);
        await Expect.That(widget).ToBeVisibleAsync();

        await widget.PressAsync("Escape");
        await Expect.Poll(() => Browser.EvaluateAsync<bool>("() => document.querySelector('dialog').open")).ToBeAsync(false);
        await Expect.That(widget).Not.ToBeVisibleAsync();
    }

    private sealed record LastKeydown(string Key, string Code, bool MetaKey);
}
