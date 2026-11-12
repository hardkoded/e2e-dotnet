// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

/// <summary><c>describe('web({ credentialOrigins })')</c>: the extra origins that receive the headers and basic auth, checked when the engine is built.</summary>
public sealed class WebCredentialOriginsTests
{
    private static readonly Dictionary<string, string> Headers = new() { ["x-preview"] = "token" };

    [Fact]
    public void Accepts_exact_http_and_https_origins_beside_headers_or_basicAuth_a_trailing_slash_included()
    {
        _ = new WebEngine(new WebEngineOptions { Headers = Headers, CredentialOrigins = ["https://api.example.com", "http://127.0.0.1:4000/"] });
        _ = new WebEngine(new WebEngineOptions { BasicAuth = new WebBasicAuth("ada", "x"), CredentialOrigins = ["https://api.example.com"] });
    }

    // Not ported: the non-string entry (3) and the non-array list. A list of strings cannot hold either.
    [Fact]
    public void Rejects_an_entry_that_is_not_an_origin_naming_it()
    {
        var cases = new (string Origin, string Message)[]
        {
            ("https://api.example.com/v1", @"\[0\] must be an origin.*got ""https://api\.example\.com/v1"""),
            ("https://api.example.com?x=1", @"\[0\] must be an origin"),
            ("https://api.example.com#top", @"\[0\] must be an origin"),
            ("https://user:pass@api.example.com", @"\[0\] must be an origin"),
            ("ftp://api.example.com", @"\[0\] must be an origin"),
            ("api.example.com", @"\[0\] must be an origin"),
            // These parse as URLs but match no request.
            ("https://*.vercel.app", @"\[0\] must be an exact origin.*wildcard"),
            ("http://*:3000", @"\[0\] must be an exact origin.*wildcard"),
            ("http://127.0.0.1:0", @"\[0\] must name the port.*port 0"),
        };
        foreach (var (origin, message) in cases)
        {
            var error = Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions { Headers = Headers, CredentialOrigins = [origin] }));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Matches(message, error.Message);
        }
    }

    [Fact]
    public void Rejects_credentialOrigins_without_headers_or_basicAuth_to_send()
    {
        var error = Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions { CredentialOrigins = ["https://api.example.com"] }));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("requires headers or basicAuth", error.Message, StringComparison.Ordinal);
    }
}
