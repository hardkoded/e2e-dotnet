// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
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

    [Fact]
    public async Task Drops_an_optional_token_of_the_wrong_type_and_keeps_vendor_fields()
    {
        var tokens = await RequestAsync("""{"access_token":"at","refresh_token":42,"id_token":null,"expires_in":"60","org_id":"org_1"}""");

        Assert.Equal(("at", null, null, "60"), (tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, tokens.ExpiresIn!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"access_token":"at","expires_in":"60","org_id":"org_1"}"""), tokens.Raw), tokens.Raw.ToJsonString());
    }

    private static async Task<TokenResponse> RequestAsync(string body)
    {
        using var http = new HttpClient(new FakeApi(_ => (HttpStatusCode.OK, body)));
        return await TokenEndpoint.RequestAsync(http, "Acme", "https://auth.test/token", new Dictionary<string, string>(), OAuthException.FlowFailed, CancellationToken.None);
    }
}
