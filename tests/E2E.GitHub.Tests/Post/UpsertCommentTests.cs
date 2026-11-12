// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using static E2E.GitHub.Tests.FakeGitHub;
using GitHubPost = E2E.GitHub.Post;

namespace E2E.GitHub.Tests.Post;

/// <summary>Upstream's <c>describe('upsertComment')</c> (<c>tests/unit/post.test.ts</c>): the comment protocol against a fake GitHub, find by author and marker, edit or create, and fail with one message.</summary>
public sealed class UpsertCommentTests
{
    private const string Marker = "<!-- e2e-github project=x -->";
    private const string Token = "ghs_token";
    private static readonly object Bot = new { login = "github-actions[bot]", type = "Bot" };

    private static Task<string> Upsert(FakeGitHub fake, CancellationToken cancellationToken = default) =>
        GitHubPost.UpsertCommentAsync(
            new PostParams(fake.Client, "https://api.github.com", Token, "octo/app", 41, Marker, $"{Marker}\n### e2e"),
            cancellationToken);

    private static FakeGitHub Fake(params (string Route, Func<Call, HttpResponseMessage> Answer)[] script) =>
        new(script.ToDictionary(entry => entry.Route, entry => entry.Answer));

    private static List<object> Page(int from, int count) =>
        [.. Enumerable.Range(from, count).Select(id => (object)new { id, user = Bot, body = $"comment {id}" })];

    private static string[] Routes(FakeGitHub fake) => [.. fake.Calls.Select(call => call.Route)];

    private static string CommentUrl(int id) => $"https://github.com/octo/app/pull/41#issuecomment-{id}";

    private static async Task<string> ErrorOf(Task<string> upsert) => (await Assert.ThrowsAsync<InvalidOperationException>(() => upsert)).Message;

    [Fact]
    public async Task Creates_a_comment_when_none_carries_the_marker_with_the_GitHub_headers_and_the_signal()
    {
        var fake = Fake(
            ("GET /repos/octo/app/issues/41/comments", _ => Json(200, new[] { new { id = 1, body = "unrelated" } })),
            ("POST /repos/octo/app/issues/41/comments", _ => Json(201, new { id = 2, html_url = CommentUrl(2) })));

        Assert.Equal(CommentUrl(2), await Upsert(fake));
        Assert.Equal(["GET /user", "GET /repos/octo/app/issues/41/comments", "POST /repos/octo/app/issues/41/comments"], Routes(fake));
        var headers = fake.Calls[1].Headers;
        Assert.Equal("Bearer ghs_token", headers["authorization"]);
        Assert.Equal("application/vnd.github+json", headers["accept"]);
        Assert.Equal("2022-11-28", headers["x-github-api-version"]);

        // .NET-only: upstream sends `@e2e-dev/github`; the port names itself.
        Assert.Equal("E2E.GitHub", headers["user-agent"]);
        Assert.StartsWith("application/json", fake.Calls[2].Headers["content-type"], StringComparison.Ordinal);
        Assert.Equal($"{Marker}\n### e2e", fake.Calls[2].BodyText);
    }

    [Fact]
    public async Task Edits_the_comment_that_carries_the_marker_searching_past_the_first_page()
    {
        var fake = Fake(
            ("GET /repos/octo/app/issues/41/comments?per_page=100&page=1", _ => Json(200, Page(0, 100))),
            ("GET /repos/octo/app/issues/41/comments?per_page=100&page=2", _ => Json(200, new[] { new { id = 500, user = Bot, body = $"{Marker}\nold" } })),
            ("PATCH /repos/octo/app/issues/comments/500", _ => Json(200, new { id = 500, html_url = CommentUrl(500) })));

        Assert.Equal(CommentUrl(500), await Upsert(fake));
        Assert.Equal("PATCH", fake.Calls[^1].Method);
        Assert.DoesNotContain(fake.Calls, call => call.Method == "POST");
    }

    [Fact]
    public async Task Matches_only_a_comment_whose_first_line_is_the_marker_not_one_that_quotes_it_or_extends_it()
    {
        var fake = Fake(
            ("GET *", _ => Json(200, new object[]
            {
                new { id = 1, user = Bot, body = $"> {Marker}\n> quoted in a reply" },
                new { id = 2, user = Bot, body = $"### e2e\n{Marker}" },
                new { id = 3, user = Bot, body = $"{Marker.Replace(" -->", " key=a -->", StringComparison.Ordinal)}\nanother job" },
                new { id = 4, user = Bot, body = $"{Marker} extra\nnot the marker line" },
                new { id = 5, user = Bot, body = $"{Marker}\r\nedited on the web" },
            })),
            ("PATCH /repos/octo/app/issues/comments/5", _ => Json(200, new { id = 5, html_url = CommentUrl(5) })));

        Assert.Equal(CommentUrl(5), await Upsert(fake));
        Assert.Equal(["GET", "GET", "PATCH"], fake.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Never_edits_a_comment_another_account_wrote_with_the_marker_and_posts_its_own()
    {
        var fake = Fake(
            ("GET /repos/octo/app/issues/41/comments", _ => Json(200, new object[]
            {
                new { id = 1, user = new { login = "mallory", type = "User" }, body = $"{Marker}\n### e2e: 9 passed" },
                new { id = 2, body = $"{Marker}\nno author" },
            })),
            ("POST /repos/octo/app/issues/41/comments", _ => Json(201, new { id = 3, html_url = CommentUrl(3) })));

        Assert.Equal(CommentUrl(3), await Upsert(fake));
        Assert.Equal(["GET", "GET", "POST"], fake.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Edits_a_GitHub_Apps_own_comment_with_the_apps_installation_token()
    {
        var fake = Fake(
            ("GET /repos/octo/app/issues/41/comments", _ => Json(200, new object[]
            {
                new { id = 1, user = new { login = "mallory", type = "User" }, body = $"{Marker}\nfirst" },
                new { id = 2, user = new { login = "shop-ci[bot]", type = "Bot" }, body = $"{Marker}\nfrom the app" },
            })),
            ("PATCH /repos/octo/app/issues/comments/2", _ => Json(200, new { id = 2, html_url = CommentUrl(2) })));

        Assert.Equal(CommentUrl(2), await Upsert(fake));
        Assert.Equal(["GET /user", "GET /repos/octo/app/issues/41/comments", "PATCH /repos/octo/app/issues/comments/2"], Routes(fake));
    }

    [Fact]
    public async Task Matches_the_account_a_personal_token_belongs_to_as_GET_user_names_it()
    {
        var fake = Fake(
            ("GET /user", _ => Json(200, new { login = "Octo-CI" })),
            ("GET /repos/octo/app/issues/41/comments", _ => Json(200, new object[]
            {
                new { id = 1, user = Bot, body = $"{Marker}\nfrom GITHUB_TOKEN" },
                new { id = 2, user = new { login = "octo-ci", type = "User" }, body = $"{Marker}\nfrom the personal token" },
            })),
            ("PATCH /repos/octo/app/issues/comments/2", _ => Json(200, new { id = 2, html_url = CommentUrl(2) })));

        Assert.Equal(CommentUrl(2), await Upsert(fake));
        Assert.Equal(["GET /user", "GET /repos/octo/app/issues/41/comments", "PATCH /repos/octo/app/issues/comments/2"], Routes(fake));
    }

    [Fact]
    public async Task Stops_searching_after_a_hundred_full_pages_and_posts_a_new_comment()
    {
        var fake = Fake(
            ("GET *", call => Json(200, Page(int.Parse(System.Web.HttpUtility.ParseQueryString(new Uri(call.Url).Query)["page"]!, System.Globalization.CultureInfo.InvariantCulture) * 100, 100))),
            ("POST *", _ => Json(201, new { id = 9, html_url = CommentUrl(9) })));

        Assert.Equal(CommentUrl(9), await Upsert(fake));
        Assert.Equal(101, fake.Calls.Count(call => call.Method == "GET"));
    }

    [Fact]
    public async Task Explains_a_token_that_cannot_write_a_rejected_token_and_anything_else_GitHub_answers()
    {
        Assert.Equal(
            "the token cannot comment on octo/app#41: a pull request from a fork runs with a read-only token, and the job needs `permissions: pull-requests: write`",
            await ErrorOf(Upsert(Fake(("GET *", _ => Json(403, new { message = "Resource not accessible by integration" }))))));
        Assert.Equal(
            "GitHub rejected the token (GITHUB_TOKEN or GH_TOKEN)",
            await ErrorOf(Upsert(Fake(("GET *", _ => Json(401, new { message = "Bad credentials" })), ("GET /user", _ => Json(401, new { }))))));
        Assert.Equal(
            "GitHub responded 502 to GET /repos/octo/app/issues/41/comments?per_page=100&page=1: upstream sad",
            await ErrorOf(Upsert(Fake(("GET *", _ => Text(502, "upstream sad"))))));
        Assert.Equal(
            "GitHub answered GET /user without a login",
            await ErrorOf(Upsert(Fake(("GET /user", _ => Json(200, new { id = 1 }))))));
        Assert.Equal(
            "GitHub responded 502 to GET /user: upstream sad",
            await ErrorOf(Upsert(Fake(("GET /user", _ => Text(502, "upstream sad"))))));
        Assert.Equal(
            "GitHub responded 403 to GET /user: {\"message\":\"API rate limit exceeded\"}",
            await ErrorOf(Upsert(Fake(("GET /user", _ => Json(403, new { message = "API rate limit exceeded" }))))));
        Assert.Equal(
            "GitHub answered the comment list with a body that is not a list",
            await ErrorOf(Upsert(Fake(("GET *", _ => Json(200, new { message = "odd" }))))));
        Assert.Equal(
            "GitHub answered the comment without its html_url",
            await ErrorOf(Upsert(Fake(("GET *", _ => Json(200, Array.Empty<object>())), ("POST *", _ => Json(201, new { id = 3 }))))));
    }

    [Fact]
    public async Task Reports_throttling_not_token_permissions_for_a_rate_limited_request()
    {
        (int Status, (string Name, string Value)[] Headers, string Message)[] throttled =
        [
            (403, [("x-ratelimit-remaining", "0")], "API rate limit exceeded"),
            (403, [("retry-after", "60")], "slow down"),
            (403, [], "You have exceeded a secondary rate limit"),
            (429, [], "too many requests"),
        ];
        foreach (var (status, headers, message) in throttled)
        {
            var fake = Fake(("GET *", _ => Text(status, $"{{\"message\":\"{message}\"}}", headers)));

            Assert.Equal(
                "GitHub rate limited GET /repos/octo/app/issues/41/comments?per_page=100&page=1; wait for the rate-limit reset or Retry-After before rerunning the job",
                await ErrorOf(Upsert(fake)));
            Assert.Equal(["GET /user", "GET /repos/octo/app/issues/41/comments"], Routes(fake));
        }
    }

    /// <remarks>Partial of the upstream <c>it</c>: a .NET cancellation carries no reason, so the test does not check the message upstream's abort reason gives.</remarks>
    [Fact]
    public async Task Partial_Carries_the_signal_on_every_request_and_stops_at_once_when_it_is_aborted()
    {
        var fake = Fake(("GET *", _ => Json(200, Array.Empty<object>())));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Upsert(fake, cts.Token));
        Assert.Single(fake.Calls);
    }

    /// <summary>.NET-only: no upstream <c>it</c> covers a token that GitHub echoes back in an error body.</summary>
    [Fact]
    public async Task Keeps_the_token_out_of_the_error_when_GitHub_echoes_it_back()
    {
        var message = await ErrorOf(Upsert(Fake(("GET *", _ => Text((int)HttpStatusCode.BadGateway, $"bad gateway for {Token}")))));

        Assert.DoesNotContain(Token, message, StringComparison.Ordinal);
        Assert.StartsWith("GitHub responded 502 to GET", message, StringComparison.Ordinal);
    }
}
