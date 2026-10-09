// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;

namespace E2E.Cli.Tests.Helpers;

/// <summary>A live session on an engine, without the host: what a catalog tool needs to run.</summary>
internal sealed class SessionContext : ISessionContext, IAsyncDisposable
{
    private SessionContext(E2ESession session)
    {
        Session = session;
    }

    public E2ESession Session { get; }

    public bool PixelsTainted { get; set; }

    public static async Task<SessionContext> StartAsync(FakeEngine engine)
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "session context",
            CacheEnabled = false,
            CacheMode = CacheMode.Off,
        });
        return new SessionContext(session);
    }

    public async ValueTask DisposeAsync()
    {
        Session.Complete();
        await Session.DisposeAsync();
    }
}
