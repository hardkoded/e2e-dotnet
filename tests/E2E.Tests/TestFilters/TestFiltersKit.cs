// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TestFilters;

/// <summary>A filter named <c>name</c> deciding with <c>decide</c> (upstream's <c>filterOf</c>, <c>tests/unit/test-filters.test.ts</c>).</summary>
internal sealed class FilterOf(string name, Func<IReadOnlyList<TestFilterCandidate>, TestFilterContext, Task<TestFilterResult>> decide) : ITestFilter
{
    public string Name { get; } = name;

    public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) => decide(tests, context);

    public static FilterOf Of(string name, Func<IReadOnlyList<TestFilterCandidate>, TestFilterResult> decide) => new(name, (tests, _) => Task.FromResult(decide(tests)));
}

/// <summary>The fixtures of <c>tests/unit/test-filters.test.ts</c>.</summary>
internal static class TestFiltersKit
{
    public const string SecretKey = "sk-live-0123456789";

    public const string Password = "correct-horse-battery";

    public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "project"));

    public static readonly TestFilterChanges ChangeSet = new(new string('a', 40), Root, ["src/app.ts"]);

    /// <summary>A scope that records its notices and the filter names it wrapped as steps.</summary>
    public static (TestFilterScope Scope, List<string> Notices, List<string> Steps) Scope(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default,
        IReadOnlySet<string>? collectedIds = null,
        IReadOnlyList<(string Name, string Value)>? secrets = null,
        Func<string, Func<Task<FilterCall>>, List<string>, Task<FilterCall>>? step = null)
    {
        var notices = new List<string>();
        var steps = new List<string>();
        var value = new TestFilterScope
        {
            ProjectRoot = Root,
            Changes = ChangeSet,
            ConfigFile = "e2e.ci.config.ts",
            Secrets = secrets ?? [("apiKey", SecretKey), ("admin", Password)],
            CollectedIds = collectedIds ?? new HashSet<string>(),
            Timeout = timeout ?? E2E.TestFilters.FilterTimeout,
            CancellationToken = cancellationToken,
            Notice = notices.Add,
            Step = step is null
                ? async (name, work) =>
                {
                    steps.Add(name);
                    return await work();
                }
                : (name, work) => step(name, work, steps),
        };
        return (value, notices, steps);
    }

    /// <summary>Candidates for plain tests titled <paramref name="titles"/>, in order (upstream's <c>candidatesOf</c>, which collects and selects them).</summary>
    public static IReadOnlyList<TestFilterCandidate> CandidatesOf(params string[] titles) =>
        [.. titles.Select(title => new TestFilterCandidate("tests/a.e2e.ts::" + Uri.EscapeDataString(title), "tests/a.e2e.ts", [title], []))];

    /// <summary>A candidate's own title: the last of its title path.</summary>
    public static string TitleOf(TestFilterCandidate candidate) => candidate.TitlePath[^1];

    /// <summary>The id of the candidate titled <paramref name="title"/>.</summary>
    public static string IdOf(IEnumerable<TestFilterCandidate> candidates, string title) =>
        candidates.FirstOrDefault(entry => TitleOf(entry) == title)?.Id ?? throw new InvalidOperationException($"no candidate titled {title}");

    /// <summary>A filter that records the context of each call and never settles, whatever its signal says.</summary>
    public static (ITestFilter Filter, List<TestFilterContext> Seen) Hanging(string name)
    {
        var seen = new List<TestFilterContext>();
        return (new FilterOf(name, (_, context) =>
        {
            lock (seen)
            {
                seen.Add(context);
            }

            return new TaskCompletionSource<TestFilterResult>().Task;
        }), seen);
    }

    public static TestFilterResult Excluding(params (string Id, string Reason)[] excluded) =>
        new() { Exclude = excluded.ToDictionary(pair => pair.Id, pair => pair.Reason) };
}
