// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's tracing channel definitions (<c>telemetry/tracing-channel.ts</c>).</summary>
public static class TelemetryTracingChannel
{
    /// <summary><c>AI_SDK_TELEMETRY_TRACING_CHANNEL</c>: the name of the tracing channel the AI SDK publishes to.</summary>
    public const string AiSdkTelemetryTracingChannel = "ai:telemetry";
}

/// <summary><c>TelemetryTracingEventType</c>: the <c>type</c> of a <see cref="TelemetryTracingChannelMessage"/>.</summary>
public static class TelemetryTracingEventType
{
    /// <summary><c>'generateText'</c>.</summary>
    public const string GenerateText = "generateText";

    /// <summary><c>'streamText'</c>.</summary>
    public const string StreamText = "streamText";

    /// <summary><c>'step'</c>.</summary>
    public const string Step = "step";

    /// <summary><c>'languageModelCall'</c>.</summary>
    public const string LanguageModelCall = "languageModelCall";

    /// <summary><c>'executeTool'</c>.</summary>
    public const string ExecuteTool = "executeTool";

    /// <summary><c>'embed'</c>.</summary>
    public const string Embed = "embed";

    /// <summary><c>'embedMany'</c>.</summary>
    public const string EmbedMany = "embedMany";

    /// <summary><c>'rerank'</c>.</summary>
    public const string Rerank = "rerank";

    /// <summary><c>'generateSpeech'</c>.</summary>
    public const string GenerateSpeech = "generateSpeech";

    /// <summary><c>'transcribe'</c>.</summary>
    public const string Transcribe = "transcribe";

    /// <summary><c>'streamTranscribe'</c>.</summary>
    public const string StreamTranscribe = "streamTranscribe";

    /// <summary><c>'experimental_decide'</c>.</summary>
    public const string ExperimentalDecide = "experimental_decide";
}

/// <summary>
/// <c>TelemetryTracingChannelMessage</c>: what a tracing channel publishes. <see cref="Result"/> and <see cref="Error"/> are set, as Node does
/// on the message, before <c>asyncEnd</c> (and <c>error</c>) are published.
/// </summary>
public sealed class TelemetryTracingChannelMessage
{
    /// <summary>One of <see cref="TelemetryTracingEventType"/>.</summary>
    public required string Type { get; init; }

    /// <summary>The operation's start event, with the telemetry settings and the AI SDK version.</summary>
    public required object? Event { get; init; }

    /// <summary>The operation's result, set before <c>asyncStart</c> and <c>asyncEnd</c> when the call records its results.</summary>
    public object? Result { get; set; }

    /// <summary>The operation's error, set before <c>error</c>.</summary>
    public object? Error { get; set; }
}
