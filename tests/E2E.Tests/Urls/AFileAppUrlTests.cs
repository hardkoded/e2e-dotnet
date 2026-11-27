// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Urls;

/// <summary>A <c>file:</c> app URL names one file on this machine (upstream's <c>describe('a file app URL')</c> of urls.test.ts).</summary>
public sealed class AFileAppUrlTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-file-app-").FullName;

    public AFileAppUrlTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "site", "pages"));
        foreach (var file in new[] { "site/index.html", "site/pages/about.html", "site/..notes.html", "secret.html" })
        {
            File.WriteAllText(Path.Combine(_directory, file), "<p>hi</p>");
        }

        // Links inside the directory: one to the directory above it, one to a file there.
        Directory.CreateSymbolicLink(Path.Combine(_directory, "site", "up"), _directory);
        File.CreateSymbolicLink(
            Path.Combine(_directory, "site", "linked.html"),
            Path.Combine(_directory, "secret.html")
        );
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string At(string file) => new Uri(Path.Combine(_directory, file)).AbsoluteUri;

    [Fact]
    public void Names_one_file_on_this_machine_with_an_origin_no_other_URL_has_and_no_port()
    {
        var url = At("site/index.html");
        var baseUrl = Urls_.Normalize(url);
        Assert.Equal(url, baseUrl.Href);
        Assert.True(E2E.Internal.Urls.IsFileBase(baseUrl));
        Assert.Equal("file://", baseUrl.Origin);
        Assert.Null(E2E.Internal.Urls.PortOf(baseUrl));
        // A single slash is the same file, localhost is this machine, and the file may not exist yet.
        Assert.Equal(baseUrl.Href, Urls_.Normalize(url.Replace("file:///", "file:/", StringComparison.Ordinal)).Href);
        Assert.Equal(
            baseUrl.Href,
            Urls_.Normalize(url.Replace("file://", "file://localhost", StringComparison.Ordinal)).Href
        );
        Assert.Equal(At("site/not-built-yet.html"), Urls_.Normalize(At("site/not-built-yet.html")).Href);
        Assert.False(E2E.Internal.Urls.IsFileBase(Urls_.Normalize("http://localhost:3000")));
    }

    [Fact]
    public void Refuses_a_host_a_directory_a_query_and_a_fragment()
    {
        Refused("file://fileserver/share/index.html", "with no host");
        Refused(At("site") + "/", "not a directory");
        // A directory named without its slash is still one: the browser would list it.
        Refused(At("site"), "not a directory");
        Refused(At("site/index.html") + "?tab=1", "query");
        Refused(At("site/index.html") + "#top", "fragment");
    }

    private string FileBase => At("site/index.html");

    private static string Resolve(string input, string? baseUrl) => E2E.Internal.Routes.Resolve(baseUrl, input);

    private static void Denied(string pattern, Action act)
    {
        var error = Assert.Throws<TestException>(act);
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Matches(pattern, error.Message);
    }

    [Fact]
    public void Navigates_to_a_file_in_the_app_files_directory_or_below_it()
    {
        Assert.Equal(At("site/pages/about.html"), Resolve("pages/about.html", FileBase));
        Assert.Equal(At("site/index.html") + "?tab=2#top", Resolve("index.html?tab=2#top", FileBase));
        Assert.Equal(At("site/pages/about.html"), Resolve(At("site/pages/about.html"), FileBase));
        Assert.Equal("https://other.test/x", Resolve("https://other.test/x", FileBase));
        // Two leading dots are a name, not a way up; a file that does not exist yet is still in the directory.
        Assert.Equal(At("site/..notes.html"), Resolve("..notes.html", FileBase));
        Assert.Equal(At("site/pages/new.html"), Resolve("pages/new.html", FileBase));
    }

    [Theory]
    [InlineData("up/secret.html")]
    [InlineData("linked.html")]
    [InlineData("up/site/../secret.html")]
    [InlineData("pages/a%00b.html")]
    [InlineData("LONG")]
    [InlineData("../secret.html")]
    [InlineData("pages/../../secret.html")]
    [InlineData("/etc/passwd")]
    [InlineData("file:///etc/passwd")]
    [InlineData("..%2fsecret.html")]
    [InlineData("pages/..%2F..%2Fsecret.html")]
    [InlineData("%2e%2e/secret.html")]
    [InlineData("file://fileserver/share/index.html")]
    public void Denies_a_file_outside_the_directory(string input)
    {
        var target = input == "LONG" ? "pages/" + new string('a', 300) + ".html" : input;
        Denied("stays in the directory of the app's file", () => Resolve(target, FileBase));
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("pages/")]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData("up/site/pages")]
    public void Denies_the_directory_its_listing_links_to_its_parent(string input) =>
        Denied("not a directory|stays in the directory", () => Resolve(input, FileBase));

    [Fact]
    public void Keeps_every_wrapper_scheme_denied()
    {
        Denied("forbidden URL scheme: view-source:", () => Resolve("view-source:" + At("site/index.html"), FileBase));
        Denied("forbidden URL scheme: data:", () => Resolve("data:text/html,x", FileBase));
    }

    [Fact]
    public void Refuses_what_a_model_would_observe_once_the_page_has_left_the_directory()
    {
        var check = (string? location, string? app) => E2E.Internal.Urls.CheckObservedLocation(location, app);
        check(At("site/pages/about.html"), FileBase);
        check(At("site/index.html") + "#top", FileBase);
        check("https://other.test/x", FileBase);
        check("about:blank", FileBase);
        check(null, FileBase);
        Denied("a file app stays in the directory of the app's file", () => check(At("secret.html"), FileBase));
        Denied("stays in the directory", () => check(At("site/linked.html"), FileBase));
        Denied("opens a file, not a directory", () => check(At("site/pages") + "/", FileBase));
        Denied("stays in the directory", () => check("file:///", FileBase));
        // Another app's observations are not this rule's: an http app cannot be led to a file at all.
        check(At("secret.html"), "http://localhost:3000/");
        check(At("secret.html"), null);
    }

    [Fact]
    public void Stays_denied_on_a_target_whose_app_is_not_a_file()
    {
        Denied("forbidden URL scheme: file:", () => Resolve(At("site/index.html"), "http://localhost:3000/app/"));
        Denied("forbidden URL scheme: file:", () => Resolve(At("site/index.html"), null));
    }

    private static void Refused(string raw, string message)
    {
        var error = Assert.Throws<ConfigurationException>(() => Urls_.Normalize(raw));
        Assert.Equal("INVALID_APP_URL", error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    private static class Urls_
    {
        public static NormalizedBaseUrl Normalize(string raw) => E2E.Internal.Urls.NormalizeBaseUrl(raw);
    }
}
