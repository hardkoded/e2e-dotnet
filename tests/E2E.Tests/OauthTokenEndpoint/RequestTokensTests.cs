// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.OauthTokenEndpoint;

public sealed class RequestTokensTests
{
    [Fact]
    public async Task Reports_a_null_body_as_a_failed_flow()
    {
        var error = await Assert.ThrowsAsync<OAuthException>(() => RequestAsync("null"));

        Assert.Equal(OAuthException.FlowFailed, error.Code);
    }

    [Fact]
    public async Task Reports_a_body_that_is_not_JSON_as_a_failed_flow()
    {
        var error = await Assert.ThrowsAsync<OAuthException>(() => RequestAsync("<html>maintenance</html>"));

        Assert.Equal((OAuthException.FlowFailed, "the Acme token response is not JSON"), (error.Code, error.Message));
    }

    // Partial: upstream returns the response without the wrong-typed tokens. The port's record reads them as
    // missing, and its `Raw` keeps the body as sent; `expires_in` is read as a number.
    [Fact]
    public async Task Reads_an_optional_token_of_the_wrong_type_as_missing_and_keeps_vendor_fields()
    {
        var tokens = await RequestAsync("""{"access_token":"at","refresh_token":42,"id_token":null,"expires_in":"60","org_id":"org_1"}""");

        Assert.Equal(("at", null, null, 60d, "org_1", 42), (tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, tokens.ExpiresIn, tokens.Raw["org_id"]!.GetValue<string>(), tokens.Raw["refresh_token"]!.GetValue<int>()));
    }

    private static async Task<TokenResponse> RequestAsync(string body)
    {
        using var http = new HttpClient(new FakeApi(_ => (HttpStatusCode.OK, body)));
        return await TokenEndpoint.RequestAsync(http, "Acme", "https://auth.test/token", new Dictionary<string, string>(), OAuthException.FlowFailed, CancellationToken.None);
    }
}
