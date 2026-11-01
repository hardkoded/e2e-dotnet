// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Changes;

namespace E2E.Tests.HostFilterSeam;

/// <summary>
/// .NET-only (upstream collects tests by running test files and has no reflection discovery): the seam that hands the tests an adapter
/// found by reflection to the test filters, and returns the ones they left out.
/// </summary>
[Collection(ProcessPathCollection.Name)]
public sealed class HostTestFiltersTests : IDisposable
{
    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            GitRepo.RemoveRepo(dir);
        }
    }

    private sealed class NamedFilter(Func<IReadOnlyList<TestFilterCandidate>, TestFilterContext, TestFilterResult> judge) : ITestFilter
    {
        public string Name => "changed-files";

        public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) => Task.FromResult(judge(tests, context));
    }

    /// <summary>A filter that keeps the tests whose file changed and leaves out the rest.</summary>
    private static NamedFilter ByChangedFile() => new((tests, context) => new TestFilterResult
    {
        Exclude = tests.Where(test => !context.Changes.Files.Contains(test.File)).ToDictionary(test => test.Id, test => $"{test.File} did not change"),
    });

    private (string Dir, E2EConfig Config) Repo()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-host-filters-").FullName;
        _dirs.Add(dir);
        GitRepo.InitRepo(dir);
        GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["tests/Cart/CartTests.cs"] = "a\n", ["tests/Login/LoginTests.cs"] = "a\n" }, "base");
        GitRepo.RunGit(dir, "checkout", "--quiet", "-b", "feature");
        GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["tests/Cart/CartTests.cs"] = "b\n" }, "feature");
        return (dir, E2EConfig.Parse("{}", Directory.ResolveLinkTarget(dir, true)?.FullName ?? dir, _ => null));
    }

    [Fact]
    public void Takes_the_base_ref_from_the_host_else_from_E2E_CHANGED_SINCE()
    {
        Assert.Equal("origin/main", E2E.HostTestFilters.ResolveChangedSince("origin/main", _ => "other"));
        Assert.Equal("other", E2E.HostTestFilters.ResolveChangedSince(null, name => name == "E2E_CHANGED_SINCE" ? "other" : null));
        Assert.Equal("other", E2E.HostTestFilters.ResolveChangedSince(" ", _ => "other"));
        Assert.Null(E2E.HostTestFilters.ResolveChangedSince(null, _ => ""));
    }

    [Fact]
    public void Finds_the_file_of_a_test_class_by_its_namespace_layout()
    {
        string[] files = ["tests/Cart/CartTests.cs", "tests/Login/CartTests.cs", "Other/Foo.cs"];
        Assert.Equal("tests/Cart/CartTests.cs", E2E.HostTestFilters.FileOf(Found("App.Tests.Cart", "CartTests"), files));
        Assert.Equal("tests/Login/CartTests.cs", E2E.HostTestFilters.FileOf(Found("App.Tests.Login", "CartTests"), files));
        Assert.Equal("Other/Foo.cs", E2E.HostTestFilters.FileOf(Found("App.Missing", "Foo+Inner"), files));
        Assert.Equal("App/Tests/Gone.cs", E2E.HostTestFilters.FileOf(Found("App.Tests", "Gone"), files));
    }

    [Fact]
    public async Task Leaves_out_the_tests_of_files_the_change_did_not_touch_and_gives_the_reason()
    {
        var (dir, config) = Repo();
        var notices = new List<string>();
        var excluded = await E2E.HostTestFilters.ExcludeAsync(
            config,
            [ByChangedFile()],
            "main",
            [Found("App.Tests.Cart", "CartTests", "Adds_an_item"), Found("App.Tests.Login", "LoginTests", "Signs_in")],
            notices.Add,
            CancellationToken.None);
        var left = Assert.Single(excluded);
        Assert.Equal("App.Tests.Login.LoginTests.Signs_in", left.Key);
        Assert.Equal("tests/Login/LoginTests.cs did not change", left.Value);
        Assert.Contains(notices, line => line.StartsWith("judging 1 file changed since ", StringComparison.Ordinal));
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task Leaves_nothing_out_when_the_config_has_no_filter()
    {
        var (_, config) = Repo();
        var notices = new List<string>();
        var excluded = await E2E.HostTestFilters.ExcludeAsync(config, [], "main", [Found("App.Tests.Login", "LoginTests", "Signs_in")], notices.Add, CancellationToken.None);
        Assert.Empty(excluded);
        Assert.Equal(["warning: --changed-since: the config has no filters, so every test runs"], notices);
    }

    private static DiscoveredTest Found(string ns, string type, string method = "Runs") => new($"{ns}.{type}.{method}", ns, type, method, []);
}
