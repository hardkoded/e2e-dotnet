// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// <c>describe('command.log')</c> of <c>config.test.ts</c>. Not ported: "enters the config digest like cwd does", since the port has no config digest.
/// </summary>
public sealed class CommandLogTests
{
    private const string AppUrl = "http://localhost:3000";

    private static string Declare(string log) =>
        $$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "log": {{System.Text.Json.JsonSerializer.Serialize(log)}} } }""";

    private static string LogOf(string log, string? root = null) => ResolveConfigTests.Resolved(Declare(log), root).Command!.Log!;

    [Fact]
    public void Accepts_a_relative_or_absolute_path_inside_the_project_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-config-root");
        Assert.Equal(Path.Combine(".e2e", "app.log"), LogOf(Path.Combine(".e2e", "app.log")));
        var absolute = Path.Combine(root, ".e2e", "app.log");
        Assert.Equal(absolute, LogOf(absolute));
    }

    [Fact]
    public void Rejects_an_empty_log_path_naming_the_target()
    {
        foreach (var log in new[] { "", "  " })
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App(Declare(log)));
            Assert.Contains("target \"web\" app.command.log must be a non-empty path", error.Message, StringComparison.Ordinal);
        }

        var number = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App("""{ "url": "http://localhost:3000", "command": { "executable": "x", "log": 7 } }"""));
        Assert.Contains("app.command.log must be a non-empty path", number.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_log_path_that_leaves_the_project_root_or_names_the_root_itself()
    {
        foreach (var log in new[] { Path.Combine("..", "app.log"), Path.Combine(Path.GetTempPath(), "elsewhere.log"), "." })
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.Resolved(Declare(log)));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Contains("app.command.log must be a file inside the project root", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Accepts_an_entry_whose_name_merely_starts_with_two_dots()
    {
        Assert.Equal(Path.Combine("..logs", "out.log"), LogOf(Path.Combine("..logs", "out.log")));
        Assert.Equal("..name", LogOf("..name"));
    }

    [Fact]
    public void Checks_containment_through_symlinks_and_accepts_a_symlinked_project_root()
    {
        var baseDir = Directory.CreateTempSubdirectory("e2e-log-root-").FullName;
        try
        {
            var root = Path.Combine(baseDir, "project");
            var outside = Path.Combine(baseDir, "outside");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "escape"), outside);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException)
            {
                // Windows without the privilege to link.
                return;
            }

            static void Refuses(Action action) =>
                Assert.Contains("app.command.log must be a file inside the project root", Assert.Throws<ConfigurationException>(action).Message, StringComparison.Ordinal);

            Refuses(() => LogOf(Path.Combine("escape", "app.log"), root));
            Refuses(() => LogOf(Path.Combine("escape", "not", "yet", "created", "app.log"), root));
            Directory.CreateSymbolicLink(Path.Combine(root, "dangling"), Path.Combine(outside, "not-yet"));
            Refuses(() => LogOf(Path.Combine("dangling", "app.log"), root));
            Assert.Equal(Path.Combine(".e2e", "logs", "app.log"), LogOf(Path.Combine(".e2e", "logs", "app.log"), root));
            var alias = Path.Combine(baseDir, "alias");
            Directory.CreateSymbolicLink(alias, root);
            Assert.Equal(Path.Combine(".e2e", "logs", "app.log"), LogOf(Path.Combine(".e2e", "logs", "app.log"), alias));
            Assert.Equal(Path.Combine(alias, "app.log"), LogOf(Path.Combine(alias, "app.log"), alias));
            Refuses(() => LogOf(Path.Combine("escape", "app.log"), alias));
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }
}
