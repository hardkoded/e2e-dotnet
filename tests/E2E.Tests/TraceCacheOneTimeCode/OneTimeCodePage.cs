// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using E2E.Tests.AgentActPixelTools;

namespace E2E.Tests.TraceCacheOneTimeCode;

/// <summary>
/// Upstream's <c>/one-time-code</c> page of <c>tests/helpers/fixture-pages/forms.ts</c>: six one-digit boxes the app marks as a one-time
/// code, and the code it issued for this load. <c>/one-time-code-plain</c> is the same boxes with no declared purpose: nothing marks the
/// code they take. The status reads the verdict once every box holds a digit, so a replay that typed another
/// load's code shows as it would to a user. Every load issues a new code.
/// </summary>
internal static class OneTimeCodePage
{
    public static Task<TinySite> StartAsync() =>
        TinySite.StartAsync(context =>
            TinySite.RespondAsync(
                context,
                Render(
                    RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture),
                    marked: context.Request.Url?.AbsolutePath != "/one-time-code-plain"
                )
            )
        );

    private static string Render(string code, bool marked)
    {
        var purpose = marked ? " autocomplete=\"one-time-code\"" : "";
        var boxes = string.Join(
            "\n    ",
            Enumerable
                .Range(1, 6)
                .Select(n =>
                    $"<input aria-label=\"Digit {n}\" inputmode=\"numeric\" maxlength=\"1\"{purpose} oninput=\"check()\" />"
                )
        );
        return $$"""
            <!doctype html>
            <html>
            <head><title>Confirm</title></head>
            <body>
              <p>Your code is {{code}}</p>
              <div role="group" aria-label="Code">
                {{boxes}}
              </div>
              <output role="status" aria-label="Verdict">waiting</output>
              <script>
                function check() {
                  const typed = [...document.querySelectorAll('input')].map((input) => input.value).join('');
                  if (typed.length < 6) return;
                  document.querySelector('output').textContent = typed === '{{code}}' ? 'Code verified' : 'Incorrect code';
                }
              </script>
            </body>
            </html>
            """;
    }
}
