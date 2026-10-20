// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;

namespace E2E.Tests.OauthErrors;

public sealed class ReadJsonBodyTests
{
    // Partial: upstream's `readJsonBody` checks any body against a schema and names the subject and the
    // problem. The port has no such function (#493); this reads a token response through `TokenEndpoint.Read`.
    [Fact]
    public void Returns_the_tokens_and_fails_the_flow_on_a_token_response_that_is_not_JSON()
    {
        Assert.Equal("a", TokenEndpoint.Read("""{"access_token":"a","extra":1}""", "Acme")!.AccessToken);
        var error = Assert.Throws<OAuthException>(() => TokenEndpoint.Read("<html>", "Acme"));
        Assert.Equal((OAuthException.FlowFailed, "the Acme token response is not JSON"), (error.Code, error.Message));
    }
}
