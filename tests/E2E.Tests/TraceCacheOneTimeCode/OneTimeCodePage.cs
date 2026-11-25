// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Web;
using E2E.Tests.AgentActPixelTools;

namespace E2E.Tests.TraceCacheOneTimeCode;

/// <summary>
/// Upstream's <c>/one-time-code</c> page of <c>tests/helpers/fixture-pages/forms.ts</c>: six one-digit boxes the app marks as a one-time
/// code, and the code it issued for this load. <c>/one-time-code-plain</c> is the same boxes with no declared purpose: nothing marks the
/// code they take. The status reads the verdict once every box holds a digit, so a replay that typed another
/// load's code shows as it would to a user. Every load issues a new code. <c>/one-time-code-plain</c> also takes <c>?parts=2</c>, which
/// splits the code three and three (boxes named "Part n"); <c>?code=</c>, which fixes the code, as a test environment that issues a known
/// code does; and <c>?label=1</c>, which adds an "Enter the 6-digit code" line, a digit of its own that a code may share.
/// </summary>
internal static class OneTimeCodePage
{
    public static Task<TinySite> StartAsync() =>
        TinySite.StartAsync(context =>
        {
            var url = context.Request.Url;
            var query = HttpUtility.ParseQueryString(url?.Query ?? "");
            var fixedCode = query["code"];
            var code =
                fixedCode is { Length: 6 } && fixedCode.All(char.IsAsciiDigit)
                    ? fixedCode
                    : RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            var plain = url?.AbsolutePath == "/one-time-code-plain";
            return TinySite.RespondAsync(
                context,
                Render(code, !plain, plain && query["parts"] == "2" ? 2 : 6, plain && query["label"] == "1")
            );
        });

    private static string Render(string code, bool marked, int parts, bool lengthHint)
    {
        var purpose = marked ? " autocomplete=\"one-time-code\"" : "";
        var label = parts == 6 ? "Digit" : "Part";
        var boxes = string.Join(
            "\n    ",
            Enumerable
                .Range(1, parts)
                .Select(n =>
                    $"<input aria-label=\"{label} {n}\" inputmode=\"numeric\" maxlength=\"{6 / parts}\"{purpose} oninput=\"check()\" />"
                )
        );
        var hint = lengthHint ? "  <p>Enter the 6-digit code</p>\n" : "";
        return $$"""
            <!doctype html>
            <html>
            <head><title>Confirm</title></head>
            <body>
              <p>Your code is {{code}}</p>
            {{hint}}              <div role="group" aria-label="Code">
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
