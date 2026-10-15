// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests;

/// <summary>The pages of upstream's <c>tests/helpers/fixture-app.ts</c> that the CDP and media tests use.</summary>
internal static class FixtureApp
{
    private const string Login = """
        <!doctype html>
        <html>
        <head><title>Fixture Login</title></head>
        <body style="margin:0;background:#fff">
        <h1>Login</h1>
        <label>User <input name="user" value="ada" style="width:200px;height:40px;background:#fff;border:1px solid #fff"></label>
        <label>Password <input type="password" name="password" style="width:200px;height:40px;background:#fff;border:1px solid #fff"></label>
        </body>
        </html>
        """;

    private const string Form = """
        <!doctype html>
        <html>
        <head><title>Fixture Form</title></head>
        <body>
        <h1>Form</h1>
        <label>First <input name="first" value="alpha"></label>
        <label>Second <input name="second" value="beta"></label>
        <label>Third <input name="third" value="alpha"></label>
        </body>
        </html>
        """;

    // Styled by the media features the browser reports, and recording what a script in the head read before
    // first paint, so a test can tell an emulation the page loaded with from one applied after.
    private const string Media = """
        <!doctype html>
        <html>
        <head>
        <title>Fixture Media</title>
        <style>
          p { color: rgb(0, 0, 0); transition-property: opacity; transition-duration: 0.3s; }
          @media (prefers-color-scheme: dark) { p { color: rgb(255, 255, 255); } }
          @media (prefers-reduced-motion: reduce) { p { transition-duration: 0s; } }
        </style>
        <script>
          window.atLoad = { dark: matchMedia('(prefers-color-scheme: dark)').matches, reduce: matchMedia('(prefers-reduced-motion: reduce)').matches };
        </script>
        </head>
        <body><p>media</p></body>
        </html>
        """;

    /// <summary>Starts the fixture app: <c>/login</c>, <c>/form</c>, and <c>/media</c>.</summary>
    public static Task<TinySite> StartAsync()
    {
        return TinySite.StartAsync(context => TinySite.RespondAsync(context, context.Request.Url!.AbsolutePath switch
        {
            "/form" => Form,
            "/media" => Media,
            _ => Login,
        }));
    }
}
