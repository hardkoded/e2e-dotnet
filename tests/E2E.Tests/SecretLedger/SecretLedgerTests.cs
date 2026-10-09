// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

/// <summary>"redacts values registered after the redact function was handed out" and "keeps redacting earlier values after a name rotates" run in <c>E2E.Cli.Tests</c>, against the MCP server's ledger: this redactor takes a fixed set of secrets.</summary>
public sealed class SecretLedgerTests
{
    [Fact]
    public void Substitutes_longer_values_first_so_nested_secrets_are_not_half_rewritten()
    {
        var ledger = Ledger.Of(("short", "abc"), ("long", "abcdef"));
        Assert.Equal("<secret:long> <secret:short>", ledger.Redact("abcdef abc"));
    }
}
