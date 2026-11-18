// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;

namespace E2E.Decision;

/// <summary>.NET-only: <c>JSON.stringify</c> of a string for prose, which writes non-ASCII text and quotes as the page showed them.</summary>
internal static class JsonText
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Quote(string text) => JsonSerializer.Serialize(text, Options);

    public static string Stringify<T>(T value) => JsonSerializer.Serialize(value, Options);
}
