// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.RedactedMessages;

/// <summary>A live session on a page that shows a secret in its title and its text, and the secret it must not leak.</summary>
internal static class RedactionSite
{
    public const string Token = "Zq-4471-RedactMe";

    public static readonly string Page = "<!doctype html><html><head><title>Account " + Token + "</title></head><body><p>Token " + Token + "</p></body></html>";

    public static Task<E2ESession> StartAsync(TinySite site, string title, Func<string?, string?>? snapshotDirectoryOf = null)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = title,
            ActionTimeout = TimeSpan.FromSeconds(5),
            TestTimeout = TimeSpan.FromSeconds(30),
            SnapshotDirectoryOf = snapshotDirectoryOf,
        });
    }

    /// <summary>Makes the session know the token, as a fill of it would.</summary>
    public static void Know(E2ESession session) => session.Remember([Secret.Create("token", Token)]);
}
