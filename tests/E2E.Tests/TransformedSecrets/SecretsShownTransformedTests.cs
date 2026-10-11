// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TransformedSecrets;

/// <summary>
/// Ported from upstream's <c>transformed-secrets.test.ts</c>, through the real Chromium engine: a token echoed
/// under CSS <c>text-transform</c> and a two-line note echoed in a <c>&lt;pre&gt;</c> reach a failed
/// <c>toHaveText</c>, and no form of either value may survive in its message. The port registers a secret
/// when it is filled or an <c>act</c> receives it in its params.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class SecretsShownTransformedTests
{
    private const string Token = "Tok-9f3aC0DeB1e7";

    private const string Note = "first line 4417\nsecond line Qx";

    // Every form of the two values a reader or a transform can produce.
    private static readonly string[] Forms =
    [
        Token,
        Token.ToUpperInvariant(),
        Token.ToLowerInvariant(),
        Note,
        Note.Replace('\n', ' '),
        Note.Replace("\n", "\\n", StringComparison.Ordinal),
    ];

    // Upstream's testbed /echo page body.
    private const string EchoPage = """
        <!doctype html>
        <html>
        <head><title>Echo</title></head>
        <body>
          <label>Token <input id="token" type="text" oninput="render()" /></label>
          <label>Note <textarea id="note" oninput="render()"></textarea></label>
          <p data-testid="plain">nothing yet</p>
          <p data-testid="upper" style="text-transform: uppercase">nothing yet</p>
          <p data-testid="lower" style="text-transform: lowercase">nothing yet</p>
          <pre data-testid="note-echo">nothing yet</pre>
          <script>
            function render() {
              const token = document.getElementById('token').value;
              for (const id of ['plain', 'upper', 'lower']) {
                document.querySelector('[data-testid="' + id + '"]').textContent = token;
              }
              document.querySelector('[data-testid="note-echo"]').textContent = document.getElementById('note').value;
            }
          </script>
        </body>
        </html>
        """;

    [Fact]
    public async Task Fails_on_each_echo_with_the_received_text_redacted()
    {
        using var site = await TinySite.StartAsync(EchoPage);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = "fails on each echo",
            ActionTimeout = TimeSpan.FromSeconds(5),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await session.App.OpenAsync("/");
        await session.Screen.GetByLabel("Token").FillAsync(Secret.Create("token", Token));
        await session.Screen.GetByLabel("Note").FillAsync(Secret.Create("note", Note));

        foreach (var id in new[] { "plain", "upper", "lower", "note-echo" })
        {
            var failure = await Assert.ThrowsAsync<TestException>(() =>
                Expect.That(session.Screen.GetByTestId(id)).ToHaveTextAsync("something else", timeout: TimeSpan.FromMilliseconds(500)));

            Assert.Contains(id == "note-echo" ? "<secret:note>" : "<secret:token>", failure.Message, StringComparison.Ordinal);
            foreach (var form in Forms)
            {
                Assert.DoesNotContain(form, failure.Message, StringComparison.Ordinal);
            }
        }
    }
}
