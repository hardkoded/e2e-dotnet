// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>The <c>App.OpenAsync</c> line of a navigation.</summary>
public sealed class NavigationLineTests
{
    private static readonly Uri Base = new("http://127.0.0.1:4100/");

    [Fact]
    public void Writes_a_URL_on_the_apps_origin_as_its_path_so_the_line_holds_on_the_next_runs_port()
    {
        Assert.Equal("await App.OpenAsync(\"/forms\");", SessionTestLines.NavigationLine("/forms", Base));
        Assert.Equal("await App.OpenAsync(\"/forms?tab=2#top\");", SessionTestLines.NavigationLine("http://127.0.0.1:4100/forms?tab=2#top", Base));
        Assert.Equal("await App.OpenAsync();", SessionTestLines.NavigationLine("http://127.0.0.1:4100/", Base));
        Assert.Equal("await App.OpenAsync(\"https://example.com/login\");", SessionTestLines.NavigationLine("https://example.com/login", Base));
        Assert.Equal("await App.OpenAsync(\"/forms\");", SessionTestLines.NavigationLine("/forms", null));
    }
}
