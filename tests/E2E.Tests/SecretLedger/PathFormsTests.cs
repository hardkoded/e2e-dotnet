// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

public sealed class PathFormsTests
{
    private readonly E2E.Internal.Redactor _ledger = Ledger.Of(("member", "p@ss word"));

    [Fact]
    public void Redacts_the_value_as_encodeURI_spells_it_in_a_path()
    {
        Assert.Equal("/reset/<secret:member>/done", _ledger.Redact("/reset/p@ss%20word/done"));
    }
}
