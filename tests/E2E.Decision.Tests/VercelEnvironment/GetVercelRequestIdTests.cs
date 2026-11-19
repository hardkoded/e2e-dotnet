// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using Env = E2E.Decision.Sdk.Gateway.VercelEnvironment;

namespace E2E.Decision.Tests.VercelEnvironment;

/// <summary>Upstream's <c>describe('getVercelRequestId')</c> of <c>vercel-environment.test.ts</c> (<c>@ai-sdk/gateway</c>).</summary>
public sealed class GetVercelRequestIdTests
{
    [Fact]
    public void Should_get_request_ID_from_request_headers_when_available()
    {
        using var _ = VercelRequestContext.Use(
            new Dictionary<string, string?> { ["x-vercel-id"] = "req_1234567890abcdef" }
        );

        Assert.Equal("req_1234567890abcdef", Env.GetVercelRequestId());
    }

    [Fact]
    public void Should_return_undefined_when_request_ID_header_is_not_available()
    {
        using var _ = VercelRequestContext.Use(new Dictionary<string, string?>());

        Assert.Null(Env.GetVercelRequestId());
    }

    [Fact]
    public void Should_return_undefined_when_no_headers_are_available()
    {
        using var _ = VercelRequestContext.Use(headers: null);

        Assert.Null(Env.GetVercelRequestId());
    }

    [Fact]
    public void Should_handle_missing_request_context_gracefully()
    {
        Assert.Null(Env.GetVercelRequestId());
    }

    [Fact]
    public void Should_handle_missing_get_method_in_request_context()
    {
        using var _ = VercelRequestContext.Use(get: null);

        Assert.Null(Env.GetVercelRequestId());
    }
}
