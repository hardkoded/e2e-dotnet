// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Internal;

/// <summary>Upstream's <c>internal/keys.ts</c>: the harness-side check of a <c>press</c> key against the contract's key grammar.</summary>
internal static class Keys
{
    /// <summary>Throws <c>INVALID_ARGUMENT</c> for anything but a key in the grammar <see cref="KeyGrammar.Parse"/> accepts. Upstream's <c>requireKey</c>.</summary>
    public static void RequireKey(string? key)
    {
        if (key is not null && KeyGrammar.Parse(key) is not null)
        {
            return;
        }

        throw new TestException(
            "INVALID_ARGUMENT",
            $"press key {(key is null ? "null" : JsonQuote.Of(key))} is not a key: use zero or more modifiers ({string.Join(", ", KeyGrammar.Modifiers)}) "
            + $"and one key joined by \"+\", where the key is a single character or one of {string.Join(", ", KeyGrammar.Names)} "
            + "(for example \"Enter\", \"Control+a\", \"Shift+Tab\")");
    }
}
