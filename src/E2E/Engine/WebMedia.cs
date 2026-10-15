// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>The <c>prefers-color-scheme</c> a page reports.</summary>
public enum WebColorScheme
{
    /// <summary><c>light</c>.</summary>
    Light,

    /// <summary><c>dark</c>.</summary>
    Dark,

    /// <summary><c>no-preference</c>.</summary>
    NoPreference,
}

/// <summary>The <c>prefers-reduced-motion</c> a page reports.</summary>
public enum WebReducedMotion
{
    /// <summary><c>reduce</c>.</summary>
    Reduce,

    /// <summary><c>no-preference</c>.</summary>
    NoPreference,
}

/// <summary>
/// User preferences a page reads through <c>@media</c> rules and <c>matchMedia</c>, as Playwright's
/// <c>emulateMedia</c> sets them. A feature left <see langword="null"/> keeps the browser's own value,
/// or the one an earlier call set.
/// </summary>
public sealed record WebMedia
{
    /// <summary><c>prefers-color-scheme</c>.</summary>
    public WebColorScheme? ColorScheme { get; init; }

    /// <summary><c>prefers-reduced-motion</c>.</summary>
    public WebReducedMotion? ReducedMotion { get; init; }

    internal bool IsEmpty => ColorScheme is null && ReducedMotion is null;

    /// <summary>The features <paramref name="other"/> sets, over the ones this sets.</summary>
    internal WebMedia Merge(WebMedia other) => new()
    {
        ColorScheme = other.ColorScheme ?? ColorScheme,
        ReducedMotion = other.ReducedMotion ?? ReducedMotion,
    };

    /// <summary>
    /// Why a feature's value is refused, naming it the way <paramref name="where"/> does; null for a value the
    /// browser emulates and for an unset one. A cast can put any number in an enum, so the type alone is no guard.
    /// </summary>
    internal static string? Problem(Func<string, string> where, WebMedia media)
    {
        if (media.ColorScheme is { } scheme && !Enum.IsDefined(scheme))
        {
            return where(nameof(ColorScheme)) + " must be Light, Dark, or NoPreference, got " + ((int)scheme).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (media.ReducedMotion is { } motion && !Enum.IsDefined(motion))
        {
            return where(nameof(ReducedMotion)) + " must be Reduce or NoPreference, got " + ((int)motion).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>The Playwright context options carrying these features.</summary>
    internal void ApplyTo(BrowserNewContextOptions context)
    {
        context.ColorScheme = ColorScheme switch
        {
            null => null,
            WebColorScheme.Light => Microsoft.Playwright.ColorScheme.Light,
            WebColorScheme.Dark => Microsoft.Playwright.ColorScheme.Dark,
            _ => Microsoft.Playwright.ColorScheme.NoPreference,
        };
        context.ReducedMotion = ReducedMotion switch
        {
            null => null,
            WebReducedMotion.Reduce => Microsoft.Playwright.ReducedMotion.Reduce,
            _ => Microsoft.Playwright.ReducedMotion.NoPreference,
        };
    }

    /// <summary>The Playwright page options carrying these features; a feature that is not set stays as the page has it.</summary>
    internal PageEmulateMediaOptions ToPageOptions()
    {
        var context = new BrowserNewContextOptions();
        ApplyTo(context);
        return new PageEmulateMediaOptions { ColorScheme = context.ColorScheme, ReducedMotion = context.ReducedMotion };
    }
}
