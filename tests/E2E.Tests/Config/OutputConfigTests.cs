// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.Config;

/// <summary>
/// Upstream <c>describe('output')</c> of config.test.ts. Not ported: <c>stays out of the digest</c> (the port has no config digest). The
/// checks against the <c>tests</c> globs are left out of the partial ports: the port has no <c>tests</c> key.
/// </summary>
public sealed class OutputConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-project");

    [Fact]
    public void Defaults_to_dot_e2e_resolves_from_the_project_root_and_output_wins_over_the_config()
    {
        Assert.Equal(Path.Combine(Root, ".e2e"), Resolve("{}").Output);
        Assert.Equal(Path.Combine(Root, "results", "e2e"), Resolve("""{ "output": "results/e2e" }""").Output);
        Assert.Equal(Path.Combine(Root, "out"), Resolve("""{ "output": "results" }""").ResolveOutput("out"));

        // The default cache sits inside the default output, outside anything a run clears.
        Assert.Equal(Path.Combine(Root, ".e2e", "cache"), Resolve("{}").Cache.Directory);
    }

    /// <summary>Partial: without the test globs the output must not hold.</summary>
    [Fact]
    public void Partial_Refuses_a_directory_the_run_cannot_own_with_the_reason()
    {
        Assert.Equal("output must be a non-empty path relative to the project root, got \"\"", Refusal("""{ "output": "" }"""));
        Assert.Contains("output must be a non-empty path", Refusal("""{ "output": 5 }"""), StringComparison.Ordinal);
        Assert.Contains("output \".\" is the project root", Refusal("""{ "output": "." }"""), StringComparison.Ordinal);
        Assert.Contains("--output \"./\" is the project root", Refusal("{}", "./"), StringComparison.Ordinal);
        Assert.Contains($"output \"../elsewhere\" is outside the project root {Root}", Refusal("""{ "output": "../elsewhere" }"""), StringComparison.Ordinal);
        Assert.Contains("is outside the project root", Refusal(Json("output", Path.Combine(Path.GetTempPath(), "e2e-results-elsewhere"))), StringComparison.Ordinal);
        Assert.Contains("is the cache directory .e2e/cache or inside it", Refusal("""{ "output": ".e2e/cache" }""").Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Contains("is the cache directory store or inside it", Refusal("""{ "output": "store/results", "cache": { "dir": "store" } }"""), StringComparison.Ordinal);
        Assert.Contains("would hold cache.dir out/results/cache under results/", Refusal("""{ "output": "out", "cache": { "dir": "out/results/cache" } }""").Replace('\\', '/'), StringComparison.Ordinal);
    }

    /// <summary>Partial: the second refusal upstream shows is the one for the test globs.</summary>
    [Fact]
    public void Partial_Names_the_default_output_when_it_is_the_one_refused()
    {
        Assert.Equal(
            "output \".e2e\" (the default) is the cache directory .e2e or inside it; keep results and the replay cache apart",
            Refusal("""{ "cache": { "dir": ".e2e" } }"""));
        Assert.Contains(
            "output \".e2e\" (the default) is the cache directory .e2e or inside it",
            Refusal("{}", environment: Env(("E2E_CACHE_DIR", ".e2e"))),
            StringComparison.Ordinal);
        Assert.Contains(
            "output \".e2e\" (the default) would hold E2E_CACHE_DIR .e2e/results/cache under results/, which the run owns; move E2E_CACHE_DIR or the output",
            Refusal("{}", environment: Env(("E2E_CACHE_DIR", ".e2e/results/cache"))).Replace('\\', '/'),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compares_paths_through_symlinks_so_one_directory_spelled_two_ways_is_one_directory()
    {
        var baseDirectory = E2E.Internal.Paths.RealPathOfExisting(Directory.CreateTempSubdirectory("e2e-output-").FullName);
        try
        {
            var real = Path.Combine(baseDirectory, "real");
            var link = Path.Combine(baseDirectory, "link");
            Directory.CreateDirectory(real);
            Directory.CreateSymbolicLink(link, real);

            // The project root as the working directory reports it, and a flag spelled through the link.
            Assert.Equal(Path.Combine(link, "out"), Parse("{}", real).ResolveOutput(Path.Combine(link, "out")));
            Assert.Equal(Path.Combine(real, "out"), Parse("{}", link).ResolveOutput(Path.Combine(real, "out")));

            // A cache the run would clear with <output>/results, however it is spelled.
            var spelled = Json("cache", new { dir = Path.Combine(link, ".e2e", "results", "cache") });
            Assert.Equal(
                "output \".e2e\" (the default) would hold cache.dir .e2e/results/cache under results/, which the run owns; move cache.dir or the output",
                Assert.Throws<ConfigurationException>(() => Parse(spelled, real)).Message.Replace('\\', '/'));
            Assert.Equal(
                $"--output {JsonSerializer.Serialize(link)} is the project root; the run clears <output>/results, so name a directory of its own, such as '.e2e'",
                Assert.Throws<ConfigurationException>(() => Parse("{}", real).ResolveOutput(link)).Message);

            // A link whose target does not exist yet is followed: the run's writes land where it points.
            Directory.CreateSymbolicLink(Path.Combine(real, "dangling"), Path.Combine(baseDirectory, "elsewhere", "results"));
            foreach (var output in new[] { "dangling", "dangling/nested" })
            {
                Assert.Equal(
                    $"--output {JsonSerializer.Serialize(output)} is outside the project root {real}; name a directory inside it",
                    Assert.Throws<ConfigurationException>(() => Parse("{}", real).ResolveOutput(output)).Message);
            }

            Directory.CreateSymbolicLink(Path.Combine(real, "pending"), "later");
            Assert.Equal(Path.Combine(real, "pending"), Parse("{}", real).ResolveOutput("pending"));
            Directory.CreateSymbolicLink(Path.Combine(real, "into-cache"), Path.Combine(".e2e", "cache"));
            Assert.Contains(
                "--output \"into-cache\" is the cache directory .e2e/cache or inside it",
                Assert.Throws<ConfigurationException>(() => Parse("{}", real).ResolveOutput("into-cache")).Message.Replace('\\', '/'),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }

    [Fact]
    public void Refuses_an_output_that_is_a_file_or_under_one_at_load()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-output-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "results.txt"), "not a directory");
            var config = Parse("{}", directory);
            Assert.Equal(
                "--output \"results.txt\" is a file; name a directory, which the run creates when it is missing",
                Assert.Throws<ConfigurationException>(() => config.ResolveOutput("results.txt")).Message);
            Assert.StartsWith(
                "--output \"results.txt/e2e\" is under the file results.txt;",
                Assert.Throws<ConfigurationException>(() => config.ResolveOutput("results.txt/e2e")).Message,
                StringComparison.Ordinal);
            Assert.Equal(Path.Combine(directory, "fresh", "e2e"), config.ResolveOutput("fresh/e2e"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Partial: the port has no test globs to sit beside, so only the cache half of the test is here.</summary>
    [Fact]
    public void Partial_Accepts_an_output_beside_the_tests_or_inside_a_glob_rooted_higher_up()
    {
        Assert.Equal(Path.Combine(Root, "results"), Resolve("""{ "output": "results" }""").Output);
        Assert.Equal(Path.Combine(Root, "out", "replays"), Resolve("""{ "output": "out", "cache": { "dir": "out/replays" } }""").Cache.Directory);
    }

    private static string Json(string key, object value) => JsonSerializer.Serialize(new Dictionary<string, object> { [key] = value });

    private static E2EConfig Parse(string json, string root) => E2EConfig.Parse(json, root, Env());

    private static E2EConfig Resolve(string json) => Parse(json, Root);

    // The message of the failure for a config and an --output flag.
    private static string Refusal(string json, string? flag = null, Func<string, string?>? environment = null)
    {
        var error = Assert.Throws<ConfigurationException>(() => (flag is null ? E2EConfig.Parse(json, Root, environment ?? Env()) : E2EConfig.Parse(json, Root, environment ?? Env()).ResolveOutput(flag)));
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error.Message;
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }
}
