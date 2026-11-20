// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>What the warning logger receives (the options of the AI SDK's <c>LogWarningsFunction</c>).</summary>
/// <param name="Warnings">The warnings the model provider returned.</param>
/// <param name="Provider">The provider id used for the call.</param>
/// <param name="Model">The model id used for the call.</param>
public sealed record LogWarningsOptions(IReadOnlyList<string> Warnings, string? Provider, string? Model);

/// <summary>The AI SDK's <c>logWarnings</c> (<c>logger/log-warnings.ts</c>).</summary>
public static class LogWarnings
{
    /// <summary>The AI SDK's <c>globalThis.AI_SDK_LOG_WARNINGS</c>: the logger that replaces the default one. Null uses the default.</summary>
    public static Action<LogWarningsOptions>? AiSdkLogWarnings { get; set; }

    /// <summary>Logs the warnings, one line each to standard error by default. Does nothing without warnings.</summary>
    internal static void Log(IReadOnlyList<string> warnings, string? provider, string? model)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        if (AiSdkLogWarnings is { } logger)
        {
            logger(new LogWarningsOptions(warnings, provider, model));
            return;
        }

        var scope = provider is not null && model is not null ? $" ({provider} / {model})" : "";
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"AI SDK Warning{scope}: {warning}");
        }
    }
}
