// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// One option of a select-like control, named by exactly one of its <see cref="Label"/>, its <see cref="Value"/>, or its
/// <see cref="Index"/>. Upstream's <c>SelectOption</c> object forms <c>{ label }</c>, <c>{ value }</c>, and <c>{ index }</c>; the plain
/// string form is <see cref="Locator.SelectOptionAsync(string, ActionOptions, CancellationToken)"/>. A select takes one option per call.
/// </summary>
public sealed record SelectOption
{
    /// <summary>The option's visible label.</summary>
    public string? Label { get; init; }

    /// <summary>The option's <c>value</c> attribute.</summary>
    public string? Value { get; init; }

    /// <summary>The option's position in its list, from 0.</summary>
    public int? Index { get; init; }

    internal const string Shape = "selectOption takes a label string or exactly one of { label }, { value }, { index } with a nonnegative integer index";

    /// <summary>Refuses an option that names none or more than one of the three, or an index below 0 (upstream's <c>requireSelectOption</c>).</summary>
    internal void Require()
    {
        var named = (Label is null ? 0 : 1) + (Value is null ? 0 : 1) + (Index is null ? 0 : 1);
        if (named != 1 || Index < 0)
        {
            throw new TestException("INVALID_ARGUMENT", Shape);
        }
    }
}
