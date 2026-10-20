// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using E2E.OAuth;

namespace E2E.Tests.OauthErrors;

public sealed class ReadJsonBodyTests
{
    // The schema `z.object({ id: z.string() })`: a JSON object with a string `id`, other fields dropped.
    private static string? Schema(JsonNode? body)
    {
        return body is JsonObject json ? TokenEndpoint.Text(json, "id") : null;
    }

    [Fact]
    public async Task Returns_the_checked_body_and_fails_the_flow_on_one_that_is_not_JSON_or_not_the_shape()
    {
        Assert.Equal("a", await ReadAsync("""{"id":"a","extra":1}"""));
        var notJson = await Assert.ThrowsAsync<OAuthException>(() => ReadAsync("<html>"));
        Assert.Equal((OAuthException.FlowFailed, "the Acme answer is not JSON"), (notJson.Code, notJson.Message));
        var malformed = await Assert.ThrowsAsync<OAuthException>(() => ReadAsync("null"));
        Assert.Equal((OAuthException.FlowFailed, "the Acme answer is malformed"), (malformed.Code, malformed.Message));
        var missing = await Assert.ThrowsAsync<OAuthException>(() => ReadAsync("{}", "is missing id"));
        Assert.Equal((OAuthException.FlowFailed, "the Acme answer is missing id"), (missing.Code, missing.Message));
    }

    private static async Task<string> ReadAsync(string body, string? invalid = null)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return invalid is null
            ? await TokenEndpoint.ReadJsonBodyAsync(content, Schema, "the Acme answer")
            : await TokenEndpoint.ReadJsonBodyAsync(content, Schema, "the Acme answer", invalid);
    }
}
