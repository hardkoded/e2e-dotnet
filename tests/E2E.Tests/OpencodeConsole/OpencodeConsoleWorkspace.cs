// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.OpencodeConsole;

/// <summary>A local Console and inference: the v1 config at its path, and one canned answer per protocol.</summary>
internal static class OpencodeConsoleWorkspace
{
    public const string ConsoleUrl = "https://opencode.ai/console";

    public static readonly OAuthCredentials Login = new()
    {
        Access = "st_tok",
        Refresh = "rt_1",
        Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000,
        Extra = new Dictionary<string, string> { ["orgId"] = "org_1" },
    };

    /// <summary>The v1 Console config, trimmed to what routing and the listing read.</summary>
    private const string Config = """
        { "config": { "provider": {
          "opencode": {
            "npm": "@ai-sdk/openai-compatible",
            "api": "https://opencode.ai/inference/openai/v1",
            "models": {
              "claude-sonnet-5": { "name": "Claude Sonnet 5", "cost": { "input": 2, "output": 10 }, "modalities": { "input": ["text", "image"] }, "provider": { "npm": "@ai-sdk/anthropic", "api": "https://opencode.ai/inference/anthropic/v1" } },
              "gemini-3.1-pro": { "cost": { "input": 2, "output": 12 }, "provider": { "npm": "@ai-sdk/google", "api": "https://opencode.ai/inference/google/v1beta" } },
              "gpt-5-nano": { "cost": { "input": 0.05, "output": 0.4 }, "provider": { "npm": "@ai-sdk/openai" } },
              "glm-5.3": { "cost": { "input": 1.4, "output": 4.4 } },
              "glm-5.1": { "cost": { "input": 1.4, "output": 4.4 }, "disabled": true },
              "big-pickle": { "cost": { "input": 0, "output": 0 } }
            }
          },
          "opencode-go": {
            "npm": "@ai-sdk/openai-compatible",
            "api": "https://opencode.ai/inference/go/openai/v1",
            "models": { "deepseek-v4.1-flash": { "name": "DeepSeek V4.1 Flash", "modalities": { "input": ["text", "image"] } } }
          },
          "console-anthropic": { "npm": "@ai-sdk/anthropic", "api": "https://opencode.ai/inference/custom/conn_1", "models": { "claude-opus-5-5": {} } }
        } } }
        """;

    public static FakeApi Serve()
    {
        return new FakeApi(request =>
        {
            var path = request.Uri.AbsolutePath;
            if (path == "/console/api/config")
            {
                return (HttpStatusCode.OK, Config);
            }

            if (path.EndsWith("/messages", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, """{ "id": "msg_1", "type": "message", "role": "assistant", "model": "claude-sonnet-5", "content": [{ "type": "text", "text": "hello" }], "stop_reason": "end_turn", "usage": { "input_tokens": 3, "output_tokens": 1 } }""");
            }

            return (HttpStatusCode.NotFound, """{ "error": "not found" }""");
        });
    }
}
