// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace E2E.Tests.TraceCacheReplay;

/// <summary>
/// The pages of upstream's <c>tests/helpers/fixture-app.ts</c> that the trace-cache flows open: the home counter, the record, company,
/// search, feed, twins, repeats, and clear-field pages, served by <see cref="TinySite"/>. <see cref="Variant"/> is
/// <c>FixtureApp.setVariant</c>: every page renders as that variant from now on, as <c>?variant=</c> does for one request.
/// </summary>
internal sealed partial class FlowsApp : IDisposable
{
    private TinySite _site = null!;
    private int _searches;
    private int _feedRequests;

    private FlowsApp()
    {
    }

    /// <summary>The variant every page renders as from now on, or null for the plain one (upstream's <c>setVariant</c>).</summary>
    public string? Variant { get; set; }

    public string Url => _site.Url;

    public static async Task<FlowsApp> StartAsync()
    {
        var app = new FlowsApp();
        app._site = await TinySite.StartAsync(context => app.ServeAsync(context));
        return app;
    }

    public void Dispose() => _site.Dispose();

    private Task ServeAsync(HttpListenerContext context)
    {
        var url = context.Request.Url!;
        var query = context.Request.QueryString;
        var variant = query["variant"] ?? Variant;
        var path = url.AbsolutePath;
        string html;
        if (path == "/")
        {
            html = Home;
        }
        else if (path == "/companies/new")
        {
            html = CompanyForm(variant);
        }
        else if (CompanyPath().Match(path) is { Success: true } company)
        {
            html = Company(Uri.UnescapeDataString(company.Groups[1].Value));
        }
        else if (RecordPath().Match(path) is { Success: true } record)
        {
            html = Record(record.Groups[1].Value == "records" ? "Record" : "Draft", Uri.UnescapeDataString(record.Groups[2].Value), variant);
        }
        else if (path == "/search")
        {
            var q = query["q"];
            if (q is not null)
            {
                Interlocked.Increment(ref _searches);
            }

            html = Search(q, Volatile.Read(ref _searches));
        }
        else if (path == "/feed")
        {
            html = Feed(Interlocked.Increment(ref _feedRequests));
        }
        else if (path == "/repeats")
        {
            html = Repeats(query["reverse"] == "1" || variant == "b", query["still"] != "1");
        }
        else if (path == "/twins-form")
        {
            html = TwinsForm(variant);
        }
        else if (path == "/clear-field")
        {
            html = ClearField;
        }
        else
        {
            context.Response.StatusCode = 404;
            html = "not found";
        }

        return TinySite.RespondAsync(context, html);
    }

    [GeneratedRegex("^/(records|drafts)/([^/]+)$")]
    private static partial Regex RecordPath();

    [GeneratedRegex("^/companies/([^/]+)$")]
    private static partial Regex CompanyPath();

    private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private const string Home = """
        <!doctype html>
        <html>
        <head><title>Fixture Home</title></head>
        <body>
          <h1>Home</h1>
          <a href="/about">About</a>
          <button id="increment" onclick="document.getElementById('count').textContent = String(Number(document.getElementById('count').textContent) + 1)">Increment</button>
          <output id="count" role="status" aria-label="Counter">0</output>
        </body>
        </html>
        """;

    private const string ClearField = """
        <!doctype html>
        <html>
        <head><title>Clear field</title></head>
        <body>
          <h1>Clear field</h1>
          <input placeholder="Draft" oninput="document.getElementById('state').textContent = this.value === '' ? 'cleared' : 'typed'" />
          <output id="state" role="status" aria-label="Draft state">untouched</output>
        </body>
        </html>
        """;

    // One record's page under a minted id, served for /records/<id> and /drafts/<id> alike.
    private static string Record(string kind, string id, string? variant) => $$"""
        <!doctype html>
        <html>
        <head><title>Record</title></head>
        <body>
          <h1>{{Escape(kind)}} {{Escape(id)}}</h1>
          <button onclick="document.getElementById('state').textContent = 'archived'">{{(variant == "renamed" ? "Retire" : "Archive")}}</button>
          <output id="state" role="status" aria-label="Record state">open</output>
        </body>
        </html>
        """;

    // A create form whose submit navigates to the created record's page, the name percent-encoded in the path.
    private static string CompanyForm(string? variant) => $$"""
        <!doctype html>
        <html>
        <head><title>New company</title></head>
        <body>
          <h1>New company</h1>
          <label for="company-name">Company name</label>
          <input id="company-name" />
          <button onclick="location.href = '/companies/' + encodeURIComponent(document.getElementById('company-name').value)">{{(variant == "renamed" ? "Save" : "Create")}}</button>
        </body>
        </html>
        """;

    private static string Company(string name) => $$"""
        <!doctype html>
        <html>
        <head><title>Company</title></head>
        <body>
          <h1>{{Escape(name)}}</h1>
          <output id="state" role="status" aria-label="Company state">created</output>
          <a href="/companies/new">New company</a>
        </body>
        </html>
        """;

    // A search page whose results land in the query string as a form submission spells it (?q=E2E+abc), beside a record count that
    // grows with the searches made so far, a timing in milliseconds, and a bare-number badge, and one stable status line.
    private static string Search(string? query, int searches)
    {
        var results = query is null
            ? ""
            : $"""
                  <h2>Results for {Escape(query)}</h2>
                  <p>{Number(searches)} results in {Number(200 + (searches * 137 % 700))}ms</p>
                  <span role="status">{Number(searches)}</span>
                  <output role="status" aria-label="Search state">done</output>

                """;
        return $$"""
            <!doctype html>
            <html>
            <head><title>Search</title></head>
            <body>
              <h1>Search</h1>
              <form action="/search" method="get">
                <label for="q">Query</label>
                <input id="q" name="q" />
                <button type="submit">Search</button>
              </form>
            {{results}}</body>
            </html>
            """;
    }

    // A page whose content changes on every request while its route stays put.
    private static string Feed(int feedRequests)
    {
        var items = string.Concat(Enumerable.Range(0, 3).Select(offset =>
            $"<li><a href=\"/about\">Offer {Number(((feedRequests + offset) % 97) + 1)} — {Number(1000 + (((feedRequests * 37) + (offset * 13)) % 9000))} zl</a></li>"));
        return $$"""
            <!doctype html>
            <html>
            <head><title>Feed</title></head>
            <body>
              <h1>Feed</h1>
              <button id="refresh" onclick="document.getElementById('mark').textContent = 'refreshed'">Refresh feed</button>
              <output id="mark" role="status" aria-label="Marker">idle</output>
              <ul>{{items}}</ul>
            </body>
            </html>
            """;
    }

    // A control repeated per row; the reverse variant lists the offers in the opposite order.
    private static string Repeats(bool reverse, bool widget)
    {
        var rows = string.Join(
            "\n",
            (reverse ? new[] { "C", "B", "A" } : ["A", "B", "C"]).Select(offer =>
                $"    <li><span>Offer {offer}</span> <button name=\"reserve-{offer.ToLowerInvariant()}\" onclick=\"pick('{offer}')\">Reserve now</button></li>"));
        var append = widget
            ? """
                  setTimeout(() => {
                    const widget = document.createElement('div');
                    widget.textContent = 'Chat with us';
                    document.body.prepend(widget);
                  }, 150);
              """
            : "";
        return $$"""
            <!doctype html>
            <html>
            <head><title>Repeats</title></head>
            <body style="margin:0">
              <output id="picked" role="status" aria-label="Picked">none</output>
              <ul style="list-style:none;padding:0">
            {{rows}}
              </ul>
              <script>
                function pick(offer) {
                  document.getElementById('picked').textContent = offer;
                }
            {{append}}
              </script>
            </body>
            </html>
            """;
    }

    // Controls a recording can only re-find by their place: two fields named by placeholder alone, two textboxes with no name in one
    // named group, and three buttons sharing one label outside any row. The b variant prepends a banner and swaps the two placeholder fields.
    private static string TwinsForm(string? variant)
    {
        var moved = variant == "b";
        const string nickname = "    <input placeholder=\"Nickname\" />";
        const string motto = "    <input placeholder=\"Motto\" />";
        var rows = string.Join(
            "\n",
            new[] { "one", "two", "three" }.Select((row, index) =>
                $"    <div><span>Row {row}</span> <button onclick=\"pick({Number(index + 1)})\">Add</button></div>"));
        var banner = moved ? "  <div role=\"note\">Announcement: scheduled maintenance tonight</div>\n" : "";
        var fields = moved ? motto + "\n" + nickname : nickname + "\n" + motto;
        return $$"""
            <!doctype html>
            <html>
            <head><title>Twins form</title></head>
            <body>
            {{banner}}  <h1>Twins form</h1>
              <div>
            {{fields}}
              </div>
              <fieldset>
                <legend>Codenames</legend>
                <div class="anon"><input /></div>
                <div class="anon"><input /></div>
              </fieldset>
            {{rows}}
              <button onclick="summarize()">Submit</button>
              <output id="picked" role="status" aria-label="Picked">none</output>
              <output id="summary" role="status" aria-label="Summary">empty</output>
              <script>
                function pick(row) {
                  document.getElementById('picked').textContent = String(row);
                }
                function summarize() {
                  const value = (selector) => document.querySelector(selector).value;
                  const anon = [...document.querySelectorAll('.anon input')].map((input) => input.value);
                  document.getElementById('summary').textContent =
                    'nickname=' + value('[placeholder="Nickname"]') + ' motto=' + value('[placeholder="Motto"]') +
                    ' first=' + anon[0] + ' second=' + anon[1] + ' picked=' + document.getElementById('picked').textContent;
                }
              </script>
            </body>
            </html>
            """;
    }
}
