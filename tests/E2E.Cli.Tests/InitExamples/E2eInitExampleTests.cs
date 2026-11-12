// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Xml.Linq;
using E2E.Cli.Tests.CliInit;

namespace E2E.Cli.Tests.InitExamples;

/// <summary>
/// Upstream's <c>describe('e2e init --example')</c> of <c>tests/unit/init-examples.test.ts</c>. The sample is downloaded
/// from a stub handler, and its project's <c>ProjectReference</c>s become package references (upstream moves the example's
/// package pins to the CLI's ranges).
/// </summary>
public sealed class E2eInitExampleTests : CliInitTestBase
{
    private static string Tag => "https://codeload.github.com/hardkoded/e2e-dotnet/tar.gz/refs/tags/v" + Versions.RunnerVersion;

    [Fact]
    public async Task Copies_the_example_from_the_release_tag_and_runs_the_wizard_on_it_without_adding_scaffold_files()
    {
        var handler = ExampleArchives.StubHandler.Serving(ExampleArchives.SampleTarball());
        var target = Path.Combine(Dir, "my-app");
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Example = "E2E.Sample", Directory = "my-app", ExampleHandler = handler }, target);
        Assert.Equal(new InitRunOutcome(0, InitResult.Scaffolded, true, true, null, null, true, true, "E2E.Sample"), outcome);
        Assert.Equal([Tag], handler.Urls);

        // The sample's project points at this tool's packages and carries what the repository's Directory.Build.props gave it.
        var project = XDocument.Parse(await File.ReadAllTextAsync(Path.Combine(target, "E2E.Sample.csproj")));
        var range = Versions.DependencyRange(Versions.RunnerVersion);
        Assert.Equal(
            [("NUnit", "5.0.0"), ("E2E", range), ("E2E.NUnit", range)],
            project.Descendants("PackageReference").Select(reference => ((string)reference.Attribute("Include")!, (string)reference.Attribute("Version")!)));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Equal(["net10.0", "latest", "enable", "enable"], new[] { "TargetFramework", "LangVersion", "Nullable", "ImplicitUsings" }.Select(name => project.Descendants(name).Single().Value));
        Assert.Equal("{}\n", await File.ReadAllTextAsync(Path.Combine(target, E2EConfig.FileName)));
        Assert.Equal("// tests\n", await File.ReadAllTextAsync(Path.Combine(target, "DaritenTests.cs")));
        Assert.False(File.Exists(Path.Combine(target, "ExampleTests.cs")));
        Assert.True(File.Exists(Path.Combine(target, ".agents/skills/e2e-dotnet/SKILL.md")));
        Assert.True(File.Exists(Path.Combine(target, ".mcp.json")));
        Assert.Contains(".e2e/results/", await File.ReadAllTextAsync(Path.Combine(target, ".gitignore")), StringComparison.Ordinal);
        Assert.DoesNotContain("Add package references", Output, StringComparison.Ordinal);
        Assert.Contains("Next: cd my-app, then dotnet test", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_main_when_the_tag_is_missing_and_says_so()
    {
        var handler = new ExampleArchives.StubHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.Contains("refs/tags/", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(ExampleArchives.SampleTarball("e2e-dotnet-main")) }));
        Assert.Equal(InitResult.Scaffolded, (await RunInitAsync(new InitOptions { Yes = true, Example = "e2e.sample", ExampleHandler = handler })).Result);
        Assert.Equal("https://codeload.github.com/hardkoded/e2e-dotnet/tar.gz/refs/heads/main", handler.Urls[1]);
        Assert.Contains("used the samples on main", Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path_("E2E.Sample.csproj")));
    }

    [Fact]
    public async Task Refuses_a_directory_with_files_in_it_before_downloading_anything()
    {
        var handler = new ExampleArchives.StubHandler((_, _) => throw new InvalidOperationException("nothing may be requested"));
        await File.WriteAllTextAsync(Path_("notes.txt"), "mine");
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Example = "E2E.Sample", ExampleHandler = handler });
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(InitResult.InvalidProject, outcome.Result);
        Assert.Empty(handler.Urls);
        Assert.Equal(["notes.txt"], Directory.EnumerateFileSystemEntries(Dir).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Leaves_nothing_behind_when_the_download_fails()
    {
        var handler = new ExampleArchives.StubHandler((_, _) => throw new HttpRequestException("fetch failed"));
        var target = Path.Combine(Dir, "app");
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Example = "E2E.Sample", ExampleHandler = handler }, target);
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(InitResult.ExampleFailed, outcome.Result);
        Assert.False(Directory.Exists(target));
        Assert.Contains("fetch failed", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_the_download_on_Ctrl_C_removes_the_partial_copy_and_exits_130()
    {
        // Half the archive arrives, then the connection stalls until the request is cancelled, as a real body does.
        var archive = ExampleArchives.SampleTarball();
        var handler = new ExampleArchives.StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ExampleArchives.StallingStream(archive)) }));
        var target = Path.Combine(Dir, "app");
        using var interrupt = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Example = "E2E.Sample", ExampleHandler = handler }, target, interrupt.Token);
        Assert.Equal(130, outcome.ExitCode);
        Assert.Equal(InitResult.Cancelled, outcome.Result);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task Removes_the_copied_example_when_the_wizard_is_cancelled_keeping_what_was_there()
    {
        var handler = ExampleArchives.StubHandler.Serving(ExampleArchives.SampleTarball());
        Directory.CreateDirectory(Path_(".git"));
        Prompts.NextConfirm(false);
        var outcome = await RunInitAsync(new InitOptions { Interactive = true, Example = "E2E.Sample", Skills = [], Mcp = [], ExampleHandler = handler });
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(InitResult.Cancelled, outcome.Result);
        Assert.Equal([".git"], Directory.EnumerateFileSystemEntries(Dir).Select(Path.GetFileName));
    }
}
