// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary><c>App</c> on a web target. A <see cref="WebEngine"/> target always declares <c>app.url</c>, so <see cref="BaseUrl"/> is always set. Upstream's <c>WebApp</c> (<c>web/fixtures.ts</c>).</summary>
public sealed class WebApp : App
{
    internal WebApp(App app, string baseUrl)
        : base(app) => BaseUrl = baseUrl;

    /// <summary>The target's <c>app.url</c> with the run's port and no trailing slash: <c>$"{app.BaseUrl}/api/users"</c>.</summary>
    public new string BaseUrl { get; }
}

/// <summary>
/// What a session on a <see cref="WebEngine"/> holds beside <c>Screen</c>: the <see cref="Browser"/> fixture, and <see cref="App"/> with its
/// <see cref="WebApp.BaseUrl"/> always set. Read it from <see cref="Session{TFixtures}.Fixtures"/>. Upstream's <c>WebFixtures</c> (<c>web/fixtures.ts</c>).
/// </summary>
public sealed class WebFixtures
{
    internal WebFixtures(Browser browser, WebApp app)
    {
        Browser = browser;
        App = app;
    }

    /// <summary>The browser fixture.</summary>
    public Browser Browser { get; }

    /// <summary>The app, with its base URL always set.</summary>
    public WebApp App { get; }
}
