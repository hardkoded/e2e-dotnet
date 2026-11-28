// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.PressCharacter;

/// <summary>
/// <c>describe('the input of a character press')</c> of upstream's <c>packages/web/tests/unit/press-character.test.ts</c>: the order of the
/// engine's input around the checkpoint, on a page that records its calls.
/// </summary>
public sealed class TheInputOfACharacterPressTests
{
    private static readonly int Control = OperatingSystem.IsMacOS() ? 4 : 2;

    [Fact]
    public async Task Checks_after_the_session_opens_and_before_the_first_key()
    {
        var (calls, page, checkpoint) = FakePage();
        await CharacterPressing.PressCharacterAsync(page, new CharacterPress("é", []), checkpoint);
        Assert.Equal(["attach", "checkpoint", "keyDown é 0 text", "keyUp é 0", "detach"], calls);
    }

    [Fact]
    public async Task Holds_the_modifiers_around_the_key_which_then_types_nothing()
    {
        var (calls, page, checkpoint) = FakePage();
        await CharacterPressing.PressCharacterAsync(
            page,
            new CharacterPress("é", [KeyModifier.ControlOrMeta, KeyModifier.Shift]),
            checkpoint
        );
        Assert.Equal(
            [
                "attach",
                "checkpoint",
                "down ControlOrMeta",
                "down Shift",
                $"keyDown é {Control | 8}",
                $"keyUp é {Control | 8}",
                "up Shift",
                "up ControlOrMeta",
                "detach",
            ],
            calls
        );
    }

    [Fact]
    public async Task Releases_the_modifiers_it_held_when_the_key_fails()
    {
        var (calls, page, checkpoint) = FakePage("keyDown é 3");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterPressing.PressCharacterAsync(
                page,
                new CharacterPress("é", [KeyModifier.Control, KeyModifier.Alt]),
                checkpoint
            )
        );
        Assert.Equal("keyDown é 3 failed", error.Message);
        Assert.Equal(["up Alt", "up Control", "detach"], calls.TakeLast(3));
    }

    [Fact]
    public async Task Fails_when_a_modifier_cannot_be_released_after_releasing_the_rest()
    {
        var (calls, page, checkpoint) = FakePage("up Alt");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterPressing.PressCharacterAsync(
                page,
                new CharacterPress("é", [KeyModifier.Control, KeyModifier.Alt]),
                checkpoint
            )
        );
        Assert.Equal("up Alt failed", error.Message);
        Assert.Equal(["up Alt", "up Control", "detach"], calls.TakeLast(3));
    }

    [Fact]
    public async Task Sends_nothing_to_the_focused_element_when_the_checkpoint_throws()
    {
        var stale = new InvalidOperationException("stale");
        var (calls, page, _) = FakePage();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterPressing.PressCharacterAsync(page, new CharacterPress("é", [KeyModifier.Control]), () =>
                throw stale
            )
        );
        Assert.Same(stale, error);
        Assert.Equal(["attach", "detach"], calls);
    }

    [Fact]
    public async Task Sends_nothing_to_a_node_when_the_checkpoint_throws()
    {
        var stale = new InvalidOperationException("stale");
        var (calls, page, _) = FakePage();
        var locator = Fake<ILocator>(
            (method, _) =>
                method == nameof(ILocator.FocusAsync) ? Task.CompletedTask : throw new NotSupportedException(method)
        );
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterPressing.PressCharacterOnAsync(
                new LocatorTarget(locator),
                page,
                new CharacterPress("é", [KeyModifier.Control]),
                new OperationBudget(TimeSpan.FromSeconds(7), "press", CancellationToken.None),
                () => throw stale
            )
        );
        Assert.Same(stale, error);
        Assert.Equal(["attach", "detach"], calls);
    }

    private static (List<string> Calls, IPage Page, Action Checkpoint) FakePage(string? failing = null)
    {
        var calls = new List<string>();
        async Task Step(string call)
        {
            calls.Add(call);
            await Task.Yield();
            if (call == failing)
            {
                throw new InvalidOperationException($"{call} failed");
            }
        }

        var cdp = Fake<ICDPSession>(
            (method, args) =>
                method switch
                {
                    nameof(ICDPSession.SendAsync) => SendAsync(args),
                    nameof(ICDPSession.DetachAsync) => Step("detach"),
                    _ => throw new NotSupportedException(method),
                }
        );
        async Task<JsonElement?> SendAsync(object?[] args)
        {
            var parameters = (Dictionary<string, object>)args[1]!;
            await Step(
                $"{parameters["type"]} {parameters["key"]} {parameters["modifiers"]}{(parameters.ContainsKey("text") ? " text" : "")}"
            );
            return null;
        }

        var keyboard = Fake<IKeyboard>(
            (method, args) =>
                method switch
                {
                    nameof(IKeyboard.DownAsync) => Step($"down {args[0]}"),
                    nameof(IKeyboard.UpAsync) => Step($"up {args[0]}"),
                    _ => throw new NotSupportedException(method),
                }
        );
        var context = Fake<IBrowserContext>(
            (method, _) =>
            {
                if (method != nameof(IBrowserContext.NewCDPSessionAsync))
                {
                    throw new NotSupportedException(method);
                }

                calls.Add("attach");
                return Task.FromResult(cdp);
            }
        );
        var page = Fake<IPage>(
            (method, _) =>
                method switch
                {
                    "get_" + nameof(IPage.Context) => context,
                    "get_" + nameof(IPage.Keyboard) => keyboard,
                    _ => throw new NotSupportedException(method),
                }
        );
        return (calls, page, () => calls.Add("checkpoint"));
    }

    private static T Fake<T>(Func<string, object?[], object?> answer)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Answer = answer;
        return proxy;
    }

    /// <summary>A Playwright interface that answers each call by name.</summary>
    public class Proxy : DispatchProxy
    {
        public Func<string, object?[], object?> Answer { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Answer(targetMethod!.Name, args ?? []);
    }
}
