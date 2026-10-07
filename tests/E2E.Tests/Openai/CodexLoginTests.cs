// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.Openai;

public sealed class CodexLoginTests
{
    [Fact]
    public void Parses_every_form_the_user_may_paste()
    {
        Assert.Equal(("abc", "st"), CodexProvider.ParseAuthorizationInput("http://localhost:1455/auth/callback?code=abc&state=st"));
        Assert.Equal(("abc", "st"), CodexProvider.ParseAuthorizationInput("abc#st"));
        Assert.Equal(("abc", "st"), CodexProvider.ParseAuthorizationInput("code=abc&state=st"));
        Assert.Equal(("abc", (string?)null), CodexProvider.ParseAuthorizationInput("  abc "));
        Assert.Equal(((string?)null, (string?)null), CodexProvider.ParseAuthorizationInput(""));
    }

    [Fact]
    public void Reads_the_account_id_from_either_token()
    {
        var idToken = FakeJwt.Of("""{"https://api.openai.com/auth":{"chatgpt_account_id":"acct_123"}}""");
        Assert.Equal("acct_123", CodexProvider.AccountId(idToken, null));
        Assert.Equal("org_1", CodexProvider.AccountId(null, FakeJwt.Of("""{"organizations":[{"id":"org_1"}]}""")));
        Assert.Null(CodexProvider.AccountId(null, "opaque"));
    }
}
