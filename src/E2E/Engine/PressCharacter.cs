// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>Upstream's <c>CharacterPress</c> (<c>press-character.ts</c>): the character a <c>press</c> sends itself, and the modifiers held over it.</summary>
internal sealed record CharacterPress(string Character, IReadOnlyList<KeyModifier> Modifiers);

/// <summary>
/// .NET-only holder name (C# has no free functions): upstream's <c>press-character.ts</c>. <c>press</c> with a character that has no key
/// on the US keyboard layout Playwright presses from (<c>é</c>, <c>€</c>, <c>中</c>). Chromium takes a key event for any character over
/// CDP, so the page gets the same <c>keydown</c>, <c>input</c>, and <c>keyup</c> as for a US key, with an empty <c>code</c> and a
/// <c>keyCode</c> of 0: no physical key stands behind it.
/// </summary>
internal static class CharacterPressing
{
    // The `modifiers` bits of CDP's `Input.dispatchKeyEvent`.
    private const int Alt = 1;
    private const int Control = 2;
    private const int Meta = 4;
    private const int Shift = 8;

    /// <summary>
    /// A <c>press</c> key the engine sends itself, because Playwright has no key for its character; null for a key Playwright presses.
    /// Throws <c>UNSUPPORTED_CAPABILITY</c> outside Chromium, where no channel takes the key. Upstream's <c>requireCharacterPress</c>.
    /// </summary>
    public static CharacterPress? RequireCharacterPress(string key, BrowserName browser)
    {
        var parsed = KeyGrammar.ParseKey(key);
        if (parsed?.Character is not { } character || IsUsLayoutCharacter(character))
        {
            return null;
        }

        if (browser != BrowserName.Chromium)
        {
            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                $"press cannot send \"{character}\" on {BrowserConnection.NameOf(browser)}: only Chromium presses a character that is not on a US keyboard. Enter it with fill or pressSequentially.",
                retryable: false
            );
        }

        return new CharacterPress(
            character,
            [
                .. parsed.Modifiers.Select(modifier =>
                    KeyModifiers.TryParse(modifier, out var parsedModifier) ? parsedModifier : default
                ),
            ]
        );
    }

    /// <summary>
    /// Focuses the node, as Playwright's <c>press</c> does, and presses the character on it; <paramref name="checkpoint"/> is
    /// <see cref="PressCharacterAsync"/>'s. Upstream's <c>pressCharacterOn</c>.
    /// </summary>
    public static async Task PressCharacterOnAsync(
        ActionTarget target,
        IPage page,
        CharacterPress press,
        OperationBudget operation,
        Action checkpoint
    )
    {
        // An ElementHandle call takes no timeout: the element is already resolved.
        if (target is LocatorTarget located)
        {
            await located
                .Locator.FocusAsync(new LocatorFocusOptions { Timeout = operation.PlaywrightTimeout })
                .ConfigureAwait(false);
        }
        else
        {
            await ((ElementTarget)target).Element.FocusAsync().ConfigureAwait(false);
        }

        await PressCharacterAsync(page, press, checkpoint).ConfigureAwait(false);
    }

    /// <summary>
    /// Presses the character on whatever holds focus. Opening the CDP session can outlast the operation or the connection, so
    /// <paramref name="checkpoint"/> runs between it and the first key. Upstream's <c>pressCharacter</c>.
    /// </summary>
    public static async Task PressCharacterAsync(IPage page, CharacterPress press, Action checkpoint)
    {
        var character = press.Character;
        var cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        var held = new List<KeyModifier>();
        try
        {
            checkpoint();
            try
            {
                // Playwright holds the modifiers, so its own record of what is down stays true.
                foreach (var modifier in press.Modifiers)
                {
                    await page.Keyboard.DownAsync(modifier.ToString()).ConfigureAwait(false);
                    held.Add(modifier);
                }

                var modifiers = press.Modifiers.Aggregate(0, (bits, modifier) => bits | Bit(modifier));

                // As for a US key, a modifier other than Shift makes the key a shortcut that types nothing.
                var down = new Dictionary<string, object>
                {
                    ["type"] = "keyDown",
                    ["key"] = character,
                    ["modifiers"] = modifiers,
                };
                if (press.Modifiers.All(modifier => modifier == KeyModifier.Shift))
                {
                    down["text"] = character;
                    down["unmodifiedText"] = character;
                }

                await cdp.SendAsync("Input.dispatchKeyEvent", down).ConfigureAwait(false);
                await cdp.SendAsync(
                        "Input.dispatchKeyEvent",
                        new Dictionary<string, object>
                        {
                            ["type"] = "keyUp",
                            ["key"] = character,
                            ["modifiers"] = modifiers,
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch
            {
                // The key's own failure is the one to report: the release only has to be tried.
                await ReleaseAsync(page, held, throwOnFailure: false).ConfigureAwait(false);
                throw;
            }

            await ReleaseAsync(page, held, throwOnFailure: true).ConfigureAwait(false);
        }
        finally
        {
            _ = await DetachAsync(cdp).ConfigureAwait(false);
        }
    }

    // Upstream's US_LAYOUT_CHARACTER, /^[ -~]$/.
    private static bool IsUsLayoutCharacter(string character) =>
        character.Length == 1 && character[0] is >= ' ' and <= '~';

    // Releases every held modifier, last first, then, when asked, throws the first failure: a modifier that may still be down is not a finished press.
    private static async Task ReleaseAsync(IPage page, IReadOnlyList<KeyModifier> held, bool throwOnFailure)
    {
        var failures = new List<Exception>();
        for (var index = held.Count - 1; index >= 0; index--)
        {
            try
            {
                await page.Keyboard.UpAsync(held[index].ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        if (throwOnFailure && failures.Count > 0)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
    }

    // `ControlOrMeta` as Playwright resolves it: by the platform of the process driving the browser.
    private static int Bit(KeyModifier modifier) =>
        modifier switch
        {
            KeyModifier.Alt => Alt,
            KeyModifier.Control => Control,
            KeyModifier.Meta => Meta,
            KeyModifier.Shift => Shift,
            _ => OperatingSystem.IsMacOS() ? Meta : Control,
        };

    // A session that cannot detach has nothing left to release: whether it did is for the caller to read.
    private static async Task<bool> DetachAsync(ICDPSession cdp)
    {
        try
        {
            await cdp.DetachAsync().ConfigureAwait(false);
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }
}
