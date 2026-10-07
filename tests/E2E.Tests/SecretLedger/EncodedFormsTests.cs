// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

public sealed class EncodedFormsTests
{
    private const string Member = "p@ss \"w\\rd\" & <x>";

    private readonly E2E.Internal.Redactor _ledger = Ledger.Of(("member", Member));

    [Fact]
    public void Redacts_the_value_URL_encoded_as_a_query_component_and_as_a_form_body()
    {
        Assert.Equal("/login?pw=<secret:member>", _ledger.Redact("/login?pw=" + Uri.EscapeDataString(Member)));
        Assert.Equal("pw=<secret:member>", _ledger.Redact("pw=p%40ss+%22w%5Crd%22+%26+%3Cx%3E"));
    }

    [Fact]
    public void Redacts_percent_encoding_in_lower_case_hex_as_some_servers_spell_it()
    {
        var path = Ledger.Of(("path", "a/b c?d"));
        Assert.Equal("q=<secret:path>", path.Redact("q=a%2fb%20c%3fd"));
        Assert.Equal("q=<secret:path>", path.Redact("q=a%2Fb+c%3Fd"));
    }

    [Fact]
    public void Redacts_numeric_character_references_decimal_zero_padded_hex_in_either_case_and_apos()
    {
        var quoted = Ledger.Of(("quoted", "it's <ok>"));
        Assert.Equal("<secret:quoted>", quoted.Redact("it&#39;s &#60;ok&#62;"));
        Assert.Equal("<secret:quoted>", quoted.Redact("it&#039;s &lt;ok&gt;"));
        Assert.Equal("<secret:quoted>", quoted.Redact("it&#x27;s &#x3c;ok&#x3E;"));
        Assert.Equal("<secret:quoted>", quoted.Redact("it&apos;s &lt;ok&gt;"));
    }

    [Fact]
    public void Redacts_uXXXX_JSON_escapes_in_either_hex_case_once_and_twice_quoted()
    {
        var quoted = Ledger.Of(("quoted", "it's <ok>"));
        Assert.Equal("""{"v":"<secret:quoted>"}""", quoted.Redact("""{"v":"it's <ok>"}"""));
        Assert.Equal(
            """{"t":"{\"v\":\"<secret:quoted>\"}"}""",
            quoted.Redact("""{"t":"{\"v\":\"it\\u0027s \\u003cok\\u003e\"}"}"""));
    }
}
