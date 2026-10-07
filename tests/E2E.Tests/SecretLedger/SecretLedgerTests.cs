// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SecretLedger;

public sealed class SecretLedgerTests
{
    [Fact]
    public void Substitutes_longer_values_first_so_nested_secrets_are_not_half_rewritten()
    {
        var secrets = new[] { Secret.Create("short", "abcdef"), Secret.Create("long", "abcdef-123456") };
        Assert.Equal("x <secret:long> y <secret:short>", SnapshotText.Redact("x ABCDEF-123456 y abcdef", secrets));
    }
}
