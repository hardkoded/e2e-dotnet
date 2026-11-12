// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.OAuth;

/// <summary>
/// Which subscription login a model reads, so <c>e2e doctor</c> can check that login without calling the model (upstream's
/// <c>oauth/login-model.ts</c>). Upstream records it on the model instance (<c>withLogin</c>, <c>loginOf</c>), because its config holds
/// instances. The port's config names the model by <c>provider</c>, so the provider name decides.
/// </summary>
internal static class LoginModel
{
    /// <summary>The <c>e2e login</c> provider id whose stored login models of the config <paramref name="provider"/> read, or null for a provider that reads an API key.</summary>
    public static string? LoginOf(string provider) => provider switch
    {
        "chatgpt" => "openai",
        "copilot" => "github-copilot",
        "grok" => "spacexai",
        "opencode-console" => "opencode-console",
        _ => null,
    };
}
