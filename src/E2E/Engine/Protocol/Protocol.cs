// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine.Protocol;

/// <summary>One file of <see cref="WireSetInputFilePayloads"/>: <paramref name="Buffer"/> is the file bytes as base64.</summary>
public sealed record WireFilePayload(string Name, string MimeType, string Buffer);

/// <summary>Upstream's <c>WireLocatorAction</c> for <c>setInputFilePayloads</c>: file bytes, like snapshot pixels, cross as base64.</summary>
public sealed record WireSetInputFilePayloads(IReadOnlyList<WireFilePayload> Files);

/// <summary>The engine protocol's wire encoding of a locator action (upstream's <c>engine/protocol/protocol.ts</c>).</summary>
public static class Protocol
{
    /// <summary>
    /// Encodes file payload bytes without changing other locator actions. Returns a <see cref="WireSetInputFilePayloads"/> for
    /// <see cref="LocatorAction.SetInputFilePayloads"/> and the same action instance for any other kind: upstream's
    /// <c>WireLocatorAction</c> is a union of the two.
    /// </summary>
    public static object EncodeLocatorAction(LocatorAction action) =>
        action is LocatorAction.SetInputFilePayloads payloads
            ? new WireSetInputFilePayloads([
                .. payloads.Files.Select(file => new WireFilePayload(
                    file.Name,
                    file.MimeType,
                    Convert.ToBase64String(file.Buffer)
                )),
            ])
            : action;

    /// <summary>Restores file payload bytes at the engine protocol boundary.</summary>
    public static LocatorAction DecodeLocatorAction(object action) =>
        action switch
        {
            WireSetInputFilePayloads wire => new LocatorAction.SetInputFilePayloads([
                .. wire.Files.Select(file => new FilePayload(
                    file.Name,
                    file.MimeType,
                    Convert.FromBase64String(file.Buffer)
                )),
            ]),
            LocatorAction other => other,
            _ => throw new ArgumentException($"Not a wire locator action: {action.GetType().Name}", nameof(action)),
        };
}
