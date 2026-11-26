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
