// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

public sealed class CaseTransformedFormsTests
{
    private const string Hex = "Tok-9f3aC0DeB1e7";

    [Fact]
    public void Redacts_the_value_upper_cased_lower_cased_and_capitalized_as_CSS_text_transform_renders_it()
    {
        var ledger = Ledger.Of(("token", Hex));
        Assert.Equal("plain <secret:token>", ledger.Redact("plain " + Hex));
        Assert.Equal("upper <secret:token>", ledger.Redact("upper " + Hex.ToUpperInvariant()));
        Assert.Equal("lower <secret:token>", ledger.Redact("lower " + Hex.ToLowerInvariant()));
        Assert.Equal("capitalized <secret:token>", ledger.Redact("capitalized Tok-9f3ac0deb1e7"));
    }
}
