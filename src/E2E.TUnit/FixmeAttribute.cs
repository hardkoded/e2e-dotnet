// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.TUnit;

/// <summary>
/// Registers the test as fixme (upstream's <c>test.fixme(title, fn)</c>, the registration form). The test is skipped with the cause
/// <c>fixme</c> and the reason <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> for a reason that is not blank, and it is reported as
/// such, not as an explicit skip. It starts its session and skips before its body, as <c>Fixme()</c> does from a set-up. A <c>NO_TESTS</c>
/// message counts it as <c>marked fixme</c>. Method-level only, a .NET-only shape: the port has no collection registry, so the host's own
/// discovery finds the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FixmeAttribute(string? reason = null) : Attribute
{
    /// <summary>The reason the test is marked fixme; a blank or missing reason reports just <c>fixme</c>.</summary>
    public string? Reason { get; } = reason;
}
