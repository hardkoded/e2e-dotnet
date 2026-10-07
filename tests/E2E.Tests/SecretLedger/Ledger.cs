// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SecretLedger;

/// <summary>A redactor over named secret values, as upstream's <c>new SecretLedger([[name, value]])</c> builds one.</summary>
internal static class Ledger
{
    public static Redactor Of(params (string Name, string Value)[] entries) =>
        Redactor.For(entries.Select(entry => Secret.Create(entry.Name, entry.Value)));
}
