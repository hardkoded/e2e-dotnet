// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

public sealed class WhitespaceNormalizedFormsTests
{
    private const string Note = "first line 4417\nsecond  line\tQx";

    [Fact]
    public void Redacts_a_multi_line_value_with_each_whitespace_run_collapsed_to_one_space_as_a_text_reader_shows_it()
    {
        var ledger = Ledger.Of(("note", Note));
        Assert.Equal("<pre><secret:note></pre>", ledger.Redact("<pre>" + Note + "</pre>"));
        Assert.Equal("text \"<secret:note>\"", ledger.Redact("text \"first line 4417 second line Qx\""));
        Assert.Equal("""{"actual":"<secret:note>"}""", ledger.Redact("""{"actual":"first line 4417 second line Qx"}"""));
        Assert.Equal("""{"actual":"<secret:note>"}""", ledger.Redact("""{"actual":"first line 4417\nsecond  line\tQx"}"""));
    }
}
