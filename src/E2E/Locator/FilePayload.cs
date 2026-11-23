// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>An in-memory file to attach without writing it to disk (upstream's <c>FilePayload</c>).</summary>
/// <param name="Name">The file name the page sees. Not blank.</param>
/// <param name="MimeType">The media type the page sees, such as <c>text/plain</c>.</param>
/// <param name="Buffer">The file's bytes. May be empty.</param>
public sealed record FilePayload(string Name, string MimeType, byte[] Buffer);
