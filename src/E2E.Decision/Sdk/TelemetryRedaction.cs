// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// A value that carries a provider-owned telemetry view (the AI SDK's <c>withTelemetryRedaction</c>, <c>provider-utils/src/telemetry-redaction.ts</c>).
/// .NET-only name: upstream attaches the view to an object with a symbol; a .NET type implements this interface instead. The original value is
/// never changed.
/// </summary>
public interface ITelemetryRedactable
{
    /// <summary>The copy of this value an integration receives, without credentials.</summary>
    object RedactForTelemetry();
}

/// <summary>The AI SDK's <c>redactTelemetryValue</c>.</summary>
internal static class TelemetryRedaction
{
    /// <summary>
    /// Applies the provider's telemetry view and leaves a value that has none unchanged. A redactor that throws yields null, so a broken
    /// redactor neither exposes credentials nor fails the call.
    /// </summary>
    internal static T? RedactTelemetryValue<T>(T? value)
        where T : class
    {
        if (value is not ITelemetryRedactable redactable)
        {
            return value;
        }

        try
        {
            return (T)redactable.RedactForTelemetry();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
