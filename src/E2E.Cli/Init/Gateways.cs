// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli;

/// <summary>One model gateway `e2e init` offers (upstream's <c>GatewayPreset</c>), written as the config's <c>agents.default</c>.</summary>
/// <param name="Id">The <c>--gateway</c> value.</param>
/// <param name="Label">The gateway's name.</param>
/// <param name="Hint">What it serves.</param>
/// <param name="Provider">The <c>provider</c> of <c>agents.default</c> (the port names the client where upstream constructs a model).</param>
/// <param name="Model">The model id.</param>
/// <param name="Comment">Who serves the model and which variable it reads, or which sign-in it uses.</param>
/// <param name="Login">For a subscription: the <c>e2e login</c> provider that signs in, which init names as the next step.</param>
/// <param name="Keys">The environment variables the provider authenticates with, any one of them enough; the first is the one to set.</param>
internal sealed record GatewayPreset(string Id, string Label, string Hint, string Provider, string Model, string Comment, string? Login = null, IReadOnlyList<string>? Keys = null);

/// <summary>Upstream's <c>cli/init/gateways.ts</c>: the model gateways `e2e init` offers.</summary>
internal static class Gateways
{
    /// <summary>The local server an OpenAI-compatible gateway points at unless an endpoint is given.</summary>
    public const string DefaultEndpoint = "http://127.0.0.1:11434/v1";

    public static readonly IReadOnlyList<GatewayPreset> All =
    [
        new("vercel", "Vercel AI Gateway", "one key for every provider; reads AI_GATEWAY_API_KEY", "gateway", "openai/gpt-6-luna-fast",
            "The Vercel AI Gateway serves the model id and reads AI_GATEWAY_API_KEY, or the OIDC token of a linked Vercel project.",
            Keys: ["AI_GATEWAY_API_KEY", "VERCEL_OIDC_TOKEN"]),
        new("openrouter", "OpenRouter", "one key for every provider; reads OPENROUTER_API_KEY", "openrouter", "openai/gpt-6-luna-fast",
            "OpenRouter serves the model id and reads OPENROUTER_API_KEY.", Keys: ["OPENROUTER_API_KEY"]),
        new("openai-compatible", "OpenAI-compatible endpoint", "any /v1 chat endpoint: Ollama, vLLM, LiteLLM, a vendor API", "openai-compatible", "gpt-6-luna",
            "The endpoint serves the model id over the OpenAI chat API."),
        new("chatgpt", "ChatGPT Plus/Pro subscription", "your ChatGPT plan through the Codex sign-in", "chatgpt", "gpt-6-luna",
            "Your ChatGPT subscription serves the model; sign in once with `e2e login openai`, `e2e models openai` lists the ids.", Login: "openai"),
        new("copilot", "GitHub Copilot subscription", "your Copilot plan: OpenAI, Anthropic, Google, and SpaceXAI models", "copilot", "claude-sonnet-5",
            "Your GitHub Copilot subscription serves the model; sign in once with `e2e login github-copilot`, `e2e models github-copilot` lists the ids.", Login: "github-copilot"),
        new("opencode-console", "OpenCode Console", "the models your OpenCode Console workspace serves", "opencode-console", "deepseek-v4.1-flash",
            "Your OpenCode Console workspace serves the model; sign in once with `e2e login opencode-console`, `e2e models opencode-console` lists the ids.", Login: "opencode-console"),
        new("grok", "SuperGrok subscription", "your SuperGrok or X Premium+ plan", "grok", "grok-4",
            "Your SuperGrok subscription serves the model; sign in once with `e2e login spacexai`, `e2e models spacexai` lists the ids.", Login: "spacexai"),
    ];

    public static GatewayPreset GetGatewayPreset(string id) =>
        All.FirstOrDefault(gateway => gateway.Id == id) ?? throw new ArgumentException("unknown gateway: " + id, nameof(id));

    /// <summary>Why an OpenAI-compatible base URL is refused, by the rule config resolution applies: HTTPS, or HTTP on a loopback host. Null when it is accepted.</summary>
    public static string? ValidateEndpoint(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed == "")
        {
            return "Enter the base URL, e.g. " + DefaultEndpoint;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
        {
            return "Not a URL: " + trimmed;
        }

        if (parsed.Scheme == "https" || (parsed.Scheme == "http" && Urls.IsLoopbackHost(parsed.Host)))
        {
            return null;
        }

        return "Must use HTTPS unless the host is loopback";
    }
}
