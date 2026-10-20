// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E.OAuth;

namespace E2E.Tests.OauthErrors;

public sealed class DescribeResponseTests
{
    [Fact]
    public async Task Names_the_failure_from_whichever_field_the_body_carries_preferring_the_description()
    {
        Assert.Equal("400: expired", await DescribeAsync("""{"error":"invalid_grant","error_description":"expired"}"""));
        Assert.Equal("400: Bad credentials", await DescribeAsync("""{"error":{"message":"Bad credentials"},"message":"other"}"""));
        Assert.Equal("400: try again", await DescribeAsync("""{"error":"invalid_grant","message":"try again"}"""));
        Assert.Equal("400: invalid_grant", await DescribeAsync("""{"error":"invalid_grant"}"""));
    }

    [Fact]
    public async Task Quotes_a_body_of_any_other_shape_instead_of_throwing()
    {
        foreach (var body in new[] { "null", "[1]", "\"down\"", """{"error":{"code":5},"error_description":7}""", "<html>" })
        {
            Assert.True("400: " + body == await DescribeAsync(body), body);
        }

        Assert.Equal("400 Bad Request", await DescribeAsync(""));
    }

    private static async Task<string> DescribeAsync(string body)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        return await TokenEndpoint.DescribeAsync(response, CancellationToken.None);
    }
}
