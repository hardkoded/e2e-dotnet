// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>isBrowserRuntime</c> (<c>@ai-sdk/provider-utils</c>): whether the code runs in a browser. Upstream keys on a global
/// <c>window</c>; .NET runs in a browser only as WebAssembly in one, which <see cref="OperatingSystem.IsBrowser"/> reports.
/// </summary>
internal static class IsBrowserRuntime
{
    private static readonly AsyncLocal<bool?> Override = new();

    internal static bool Value => Override.Value ?? OperatingSystem.IsBrowser();

    /// <summary>The counterpart of a test setting <c>globalThis.window</c>: reports a browser until the returned scope is disposed.</summary>
    internal static IDisposable Simulate()
    {
        Override.Value = true;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Override.Value = null;
    }
}
