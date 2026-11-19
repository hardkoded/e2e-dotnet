// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.FilterHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Filter;

/// <summary>Upstream's <c>describe('decisionFilter')</c> in <c>tests/filter.test.ts</c>.</summary>
public sealed class DecisionFilterTests : IDisposable
{
    private const string Cart = "tests/cart.e2e.ts";
    private const string Login = "tests/login.e2e.ts";
    private const string Search = "tests/search.e2e.ts";

    private static readonly TestFilterCandidate Adds = Candidate(Cart, "cart", "adds an item");
    private static readonly TestFilterCandidate Removes = Candidate(Cart, "cart", "removes an item");
    private static readonly TestFilterCandidate SignsIn = Candidate(Login, "signs in");
    private static readonly TestFilterCandidate Finds = Candidate(Search, "finds a product");

    private const string Example =
        "pass a decision model, e.g. DecisionFilter(new DecisionFilterOptions { Model = new OpenAIDecisionModel(...) })";

    private static readonly JsonSerializerOptions Relaxed = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<ChangedRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    private ChangedRepo Repo(
        Dictionary<string, string> baseFiles,
        Dictionary<string, string> change,
        string project = ".",
        Dictionary<string, string>? config = null
    )
    {
        var repo = ChangedRepoOf(baseFiles, change, project, config);
        _repos.Add(repo);
        return repo;
    }

    /// <summary>A repository whose change edits the cart, with three test files committed at the base.</summary>
    private ChangedRepo CartRepo(Dictionary<string, string>? change = null) =>
        Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = "test('adds an item', async () => {});\ntest('removes an item', async () => {});\n",
                [Login] = "test('signs in', async () => {});\n",
                [Search] = "test('finds a product', async () => {});\n",
            },
            change ?? new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );

    private static ITestFilter Filter(
        IDecisionModel model,
        IReadOnlyDictionary<string, JsonElement>? providerOptions = null
    ) =>
        E2E.Decision.Filter.DecisionFilter(
            new DecisionFilterOptions { Model = model, ProviderOptions = providerOptions }
        );

    /// <summary>The question the filter asks about the test at <paramref name="titlePath"/> in <paramref name="file"/>.</summary>
    private static string Asked(string file, params string[] titlePath) =>
        $"Can the diff change what the e2e test \"{string.Join(" > ", titlePath)}\" in {file} checks? testFiles[\"{file}\"] is its file, hooks included.";

    /// <summary>The test files a call sent, by path.</summary>
    private static Dictionary<string, string> Sent(DecideRequest? request) =>
        request?.State is { } state && state.TryGetProperty("testFiles", out var files)
            ? files.EnumerateObject().ToDictionary(file => file.Name, file => file.Value.GetString()!)
            : [];

    private static string Diff(DecideRequest? request) => request!.State!.Value.GetProperty("diff").GetString()!;

    /// <summary><c>score</c> for each of <paramref name="tests"/>, by id: what a result's <c>Relevance</c> holds when the model gave them all one answer.</summary>
    private static Dictionary<string, double> Scored(IEnumerable<TestFilterCandidate> tests, double score) =>
        tests.ToDictionary(test => test.Id, _ => score);

    /// <summary>A test file of about <paramref name="size"/> characters with one test titled <paramref name="title"/>.</summary>
    private static string Sized(string title, int size) =>
        $"test('{title}', async () => {{}});\n// {new string('x', size)}\n";

    private static void AssertResult(
        TestFilterResult result,
        Dictionary<string, string> exclude,
        Dictionary<string, double>? relevance,
        string? note
    )
    {
        Assert.Equal(
            exclude.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            result.Exclude.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
        Assert.Equal(
            relevance?.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            result.Relevance?.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
        Assert.Equal(note, result.Note);
    }

    private static List<List<string>> Keys(IEnumerable<DecideRequest> requests) =>
        [.. requests.Select(request => Sent(request).Keys.ToList())];

    private static List<int> Counts(IEnumerable<DecideRequest> requests) =>
        [.. requests.Select(request => request.Instructions.Count).Order()];

    [Fact]
    public async Task Asks_the_model_it_is_given_about_every_test_file_that_fits_in_one_call_one_question_per_test()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0.9);
        var context = FilterContext(repo, out var logs);
        var filter = Filter(model);
        Assert.Equal("decision", filter.Name);
        var result = await filter.FilterAsync([Adds, SignsIn, Removes], context);
        AssertResult(result, [], Scored([Adds, SignsIn, Removes], 0.9), "1 call, 10 tokens");
        Assert.Equal(["asking scripted-judge about 3 tests in 2 files"], logs);
        var request = Assert.Single(model.Requests);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["t0"] = Asked(Cart, "cart", "adds an item"),
                ["t1"] = Asked(Cart, "cart", "removes an item"),
                ["t2"] = Asked(Login, "signs in"),
            },
            request.Instructions
        );
        Assert.Equal([Cart, Login], Sent(request).Keys);
        Assert.False(request.AbortSignal.IsCancellationRequested);
    }

    [Fact]
    public async Task Splits_the_test_files_into_calls_that_each_stay_within_the_size_the_model_takes()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = Sized("adds an item", 45_000),
                [Login] = Sized("signs in", 45_000),
                [Search] = Sized("finds a product", 100),
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([Adds, SignsIn, Finds], FilterContext(repo));
        Assert.Equal(
            [
                [Cart],
                [Login, Search],
            ],
            Keys(model.Requests).OrderBy(keys => string.Join(",", keys), StringComparer.Ordinal).ToList()
        );
        foreach (var request in model.Requests)
        {
            var diff = Diff(request);
            Assert.Contains("+export const total = 2;", diff);
            Assert.True(diff.Length + Sent(request).Values.Sum(text => text.Length) <= 80_000);
        }
    }

    [Fact]
    public async Task Asks_at_most_200_questions_in_one_call()
    {
        static List<TestFilterCandidate> Many(string file) =>
            [.. Enumerable.Range(0, 100).Select(i => Candidate(file, $"test {i}"))];
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([.. Many(Cart), .. Many(Login), .. Many(Search)], FilterContext(repo));
        Assert.Equal([100, 200], Counts(model.Requests));
    }

    [Fact]
    public async Task Splits_a_file_with_more_than_200_tests_across_calls_each_sending_the_file()
    {
        var tests = Enumerable.Range(0, 450).Select(i => Candidate(Cart, $"test {i}")).ToList();
        using var repo = CartRepo();
        var model = ScriptedJudge(text => text.Contains("\"test 449\"", StringComparison.Ordinal) ? 0 : 0.9);
        var result = await Filter(model).FilterAsync(tests, FilterContext(repo));
        Assert.Equal([50, 200, 200], Counts(model.Requests));
        Assert.All(Keys(model.Requests), keys => Assert.Equal([Cart], keys));
        var scores = Scored(tests, 0.9);
        scores[tests[449].Id] = 0;
        AssertResult(
            result,
            new Dictionary<string, string> { [tests[449].Id] = "0% likely affected" },
            scores,
            "3 calls, 30 tokens"
        );
    }

    [Fact]
    public async Task Makes_at_most_8_calls_at_once()
    {
        var files = Enumerable.Range(0, 10).Select(i => $"tests/t{i}.e2e.ts").ToList();
        var baseFiles = new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 1;\n" };
        foreach (var file in files)
        {
            baseFiles[file] = Sized("runs", 45_000);
        }

        using var repo = Repo(
            baseFiles,
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var inFlight = 0;
        var most = 0;
        var model = ScriptedJudge(
            _ => 0.9,
            async (_, _) =>
            {
                var now = Interlocked.Increment(ref inFlight);
                int seen;
                while (
                    now > (seen = Volatile.Read(ref most)) && Interlocked.CompareExchange(ref most, now, seen) != seen
                ) { }

                await Task.Delay(50);
                Interlocked.Decrement(ref inFlight);
            }
        );
        await Filter(model).FilterAsync([.. files.Select(file => Candidate(file, "runs"))], FilterContext(repo));
        Assert.Equal(10, model.Requests.Count);
        Assert.Equal(8, most);
    }

    [Fact]
    public async Task Leaves_the_tests_of_a_file_too_large_to_send_with_the_diff_in_and_judges_the_rest()
    {
        const string Huge = "tests/huge.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Huge] = Sized("scrolls", 90_000),
                [Login] = "test('signs in', async () => {});\n",
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(_ => 0);
        var scrolls = Candidate(Huge, "scrolls");
        var result = await Filter(model).FilterAsync([scrolls, SignsIn], FilterContext(repo));
        AssertResult(
            result,
            new Dictionary<string, string> { [SignsIn.Id] = "0% likely affected" },
            new Dictionary<string, double> { [scrolls.Id] = 0.5, [SignsIn.Id] = 0 },
            "scripted-judge did not judge 1 test in 1 file: tests/huge.e2e.ts is too large to send with the diff; 1 call, 10 tokens"
        );
        Assert.Equal(
            [
                [Login],
            ],
            Keys(model.Requests)
        );
    }

    [Fact]
    public async Task Leaves_every_test_in_making_no_call_when_every_file_is_too_large_to_send_with_the_diff()
    {
        const string Huge = "tests/huge.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Huge] = Sized("scrolls", 90_000),
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(_ => 0);
        var scrolls = Candidate(Huge, "scrolls");
        AssertResult(
            await Filter(model).FilterAsync([scrolls], FilterContext(repo)),
            [],
            new Dictionary<string, double> { [scrolls.Id] = 0.5 },
            "scripted-judge did not judge 1 test in 1 file: tests/huge.e2e.ts is too large to send with the diff"
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Sends_the_redacted_diff_and_the_redacted_test_file_under_its_redacted_path()
    {
        var file = $"tests/{TestSecret}.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [file] = $"test('signs in with {TestSecret}', async () => {{}});\n",
            },
            new Dictionary<string, string> { ["src/cart.ts"] = $"export const key = '{TestSecret}';\n" }
        );
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([Candidate(file, $"signs in with {TestSecret}")], FilterContext(repo));
        var request = model.Requests[0];
        Assert.Contains("+export const key = '[redacted]';", Diff(request));
        Assert.DoesNotContain(TestSecret, request.State!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["tests/[redacted].e2e.ts"] = File.ReadAllText(Path.Combine(repo.Root, file))
                    .Replace(TestSecret, "[redacted]", StringComparison.Ordinal),
            },
            Sent(request)
        );
        Assert.Equal(
            new Dictionary<string, string> { ["t0"] = Asked("tests/[redacted].e2e.ts", "signs in with [redacted]") },
            request.Instructions
        );
    }

    [Fact]
    public async Task Sends_test_files_whose_paths_redact_to_the_same_key_in_separate_calls()
    {
        string[] files = ["tests/alpha.e2e.ts", "tests/beta.e2e.ts"];
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [files[0]] = "test('runs a', async () => {});\n",
                [files[1]] = "test('runs b', async () => {});\n",
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model)
            .FilterAsync(
                [Candidate(files[0], "runs a"), Candidate(files[1], "runs b")],
                FilterContext(repo, redact: text => Regex.Replace(text, "alpha|beta", "[redacted]"))
            );
        Assert.Equal(
            ["test('runs a', async () => {});\n", "test('runs b', async () => {});\n"],
            model.Requests.Select(request => Sent(request)["tests/[redacted].e2e.ts"]).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Sends_the_diff_as_redact_returns_it_given_the_whole_diff_at_once()
    {
        using var repo = CartRepo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                ["src/tax.ts"] = "export const rate = 1;\n",
            }
        );
        var given = new List<string>();
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model)
            .FilterAsync([SignsIn], FilterContext(repo, redact: text =>
                {
                    lock (given)
                    {
                        given.Add(text);
                    }

                    return text.Replace("export", "[redacted]", StringComparison.Ordinal);
                }));
        var diffs = given.Where(text => text.StartsWith("diff --git ", StringComparison.Ordinal)).ToList();
        var whole = Assert.Single(diffs);
        Assert.Contains("-export const total = 1;\n+export const total = 2;\n", whole);
        Assert.Contains("+export const rate = 1;\n", whole);
        Assert.Equal(whole.Replace("export", "[redacted]", StringComparison.Ordinal), Diff(model.Requests[0]));
    }

    [Fact]
    public async Task Sends_gits_plain_unified_diff_whatever_the_repositorys_diff_config_says()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                [".gitattributes"] = "*.ts diff=shout\n",
                ["src/cart.ts"] = "export const total = 1;\n",
                [Login] = "test('signs in', async () => {});\n",
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" },
            config: new Dictionary<string, string>
            {
                ["color.ui"] = "always",
                ["diff.external"] = "echo external",
                ["diff.shout.textconv"] = "sed s/total/TOTAL/",
            }
        );
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
        Assert.Matches(
            @"\Adiff --git a/src/cart\.ts b/src/cart\.ts\nindex \w+\.\.\w+ 100644\n--- a/src/cart\.ts\n\+\+\+ b/src/cart\.ts\n@@ -1 \+1 @@\n-export const total = 1;\n\+export const total = 2;\n\z",
            Diff(model.Requests[0])
        );
    }

    [Fact]
    public async Task Sends_an_untracked_file_by_its_name_alone_never_reading_it()
    {
        // A token redact does not know, as CI may write one into the checkout: only leaving the file unread keeps it out.
        const string Token = "sk-ci-8e1f0a9c3b";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = "test('adds an item', async () => {});\n",
            },
            []
        );
        File.WriteAllText(Path.Combine(repo.Root, "src/coupon.ts"), $"export const code = '{Token}';\n");
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([Adds], FilterContext(repo, files: ["src/coupon.ts"]));
        Assert.Equal(
            "diff --git a/src/coupon.ts b/src/coupon.ts\nnot committed, contents not read\n",
            Diff(model.Requests[0])
        );
        Assert.DoesNotContain(Token, model.Requests[0].State!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quotes_an_untracked_name_git_would_quote_so_it_adds_no_line_of_its_own_and_redacts_a_secret_in_one()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = "test('adds an item', async () => {});\n",
            },
            []
        );
        const string Odd = "src/odd\n+export const forged = true;.ts";
        var secretName = $"src/{TestSecret}.ts";
        foreach (var file in new[] { Odd, secretName })
        {
            File.WriteAllText(Path.Combine(repo.Root, file), "export {};\n");
        }

        var model = ScriptedJudge(_ => 0.9);
        await Filter(model)
            .FilterAsync(
                [Adds],
                FilterContext(repo, files: [.. new[] { Odd, secretName }.Order(StringComparer.Ordinal)])
            );
        var diff = Diff(model.Requests[0]);
        Assert.DoesNotContain("+export const forged = true;.ts", diff.Split('\n'));
        Assert.Contains(
            $"diff --git {JsonSerializer.Serialize("a/" + Odd, Relaxed)} {JsonSerializer.Serialize("b/" + Odd, Relaxed)}\nnot committed, contents not read\n",
            diff
        );
        Assert.Contains("diff --git a/src/[redacted].ts b/src/[redacted].ts\nnot committed, contents not read\n", diff);
        Assert.DoesNotContain(TestSecret, model.Requests[0].State!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diffs_only_the_files_the_change_names_so_a_filter_that_narrows_them_narrows_what_the_model_reads()
    {
        using var repo = CartRepo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                ["src/legacy.ts"] = "export const old = 2;\n",
                ["credentials.json"] = "{}\n",
            }
        );
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([Adds], FilterContext(repo, files: ["src/cart.ts"]));
        var diff = Diff(model.Requests[0]);
        Assert.Contains("+export const total = 2;\n", diff);
        Assert.DoesNotContain("legacy", diff);
        Assert.DoesNotContain("credentials", diff);
    }

    [Fact]
    public async Task Diffs_only_what_the_commits_since_the_base_change_and_names_a_file_no_commit_changes_staged_or_not_without_reading_it()
    {
        // A token redact does not know, as CI may write one into a tracked file: only leaving the change unread keeps it out.
        const string Token = "sk-ci-3c7d9e2a10";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                ["src/coupon.ts"] = "export const code = 1;\n",
                ["src/fee.ts"] = "export const fee = 1;\n",
                [Cart] = "test('adds an item', async () => {});\n",
            },
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                ["src/tax.ts"] = "export const rate = 1;\n",
            }
        );
        string[] changed = ["src/cart.ts", "src/coupon.ts", "src/fee.ts", "src/tax.ts"];
        foreach (var file in changed)
        {
            File.WriteAllText(Path.Combine(repo.Root, file), $"export const value = '{Token}';\n");
        }

        Git(repo.Root, "add", "src/fee.ts", "src/tax.ts");
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model).FilterAsync([Adds], FilterContext(repo, files: changed));
        var diff = Diff(model.Requests[0]);
        Assert.Equal(
            [
                "diff --git a/src/cart.ts b/src/cart.ts",
                "diff --git a/src/tax.ts b/src/tax.ts",
                "diff --git a/src/coupon.ts b/src/coupon.ts",
                "diff --git a/src/fee.ts b/src/fee.ts",
            ],
            diff.Split('\n').Where(line => line.StartsWith("diff --git ", StringComparison.Ordinal))
        );
        Assert.Contains("-export const total = 1;\n+export const total = 2;\n", diff);
        Assert.Contains("+++ b/src/tax.ts\n@@ -0,0 +1 @@\n+export const rate = 1;\n", diff);
        Assert.Contains("diff --git a/src/coupon.ts b/src/coupon.ts\nnot committed, contents not read\n", diff);
        Assert.Contains("diff --git a/src/fee.ts b/src/fee.ts\nnot committed, contents not read\n", diff);
        Assert.DoesNotContain(Token, model.Requests[0].State!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_nothing_out_making_no_call_when_git_prints_no_diff_as_for_a_change_a_wrapper_narrowed_to_no_files()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0);
        var result = await Filter(model).FilterAsync([Adds], FilterContext(repo, files: []));
        AssertResult(
            result,
            [],
            new Dictionary<string, double> { [Adds.Id] = 0.5 },
            "git printed no diff of the changed files, so the decision filter left nothing out"
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Asks_nothing_and_logs_nothing_when_a_wrapper_hands_it_no_tests()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0);
        var context = FilterContext(repo, out var logs);
        AssertResult(await Filter(model).FilterAsync([], context), [], null, null);
        Assert.Empty(model.Requests);
        Assert.Empty(logs);
    }

    [Fact]
    public async Task Leaves_nothing_out_making_no_call_when_the_e2e_config_or_a_lockfile_changed_at_any_depth_never_for_a_file_only_named_like_one()
    {
        foreach (
            var file in new[]
            {
                "e2e.config.ts",
                "apps/web/e2e.config.mjs",
                "pnpm-lock.yaml",
                "ios/Podfile.lock",
                "go.sum",
                "gradle/libs.versions.toml",
            }
        )
        {
            using var repo = CartRepo(new Dictionary<string, string> { [file] = "changed\n" });
            var model = ScriptedJudge(_ => 0);
            var result = await Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo));
            AssertResult(
                result,
                [],
                Scored([Adds, SignsIn], 0.5),
                $"{file} changed, so the decision filter left nothing out"
            );
            Assert.Empty(model.Requests);
        }

        using var docs = CartRepo(new Dictionary<string, string> { ["docs/pnpm-lock.yaml.md"] = "about locks\n" });
        var judge = ScriptedJudge(_ => 0);
        var outcome = await Filter(judge).FilterAsync([SignsIn], FilterContext(docs));
        Assert.Equal(new Dictionary<string, string> { [SignsIn.Id] = "0% likely affected" }, outcome.Exclude);
        Assert.Single(judge.Requests);
    }

    [Fact]
    public async Task Still_scores_the_tests_the_change_touches_when_a_lockfile_or_the_config_leaves_nothing_out_so_they_start_first()
    {
        var model = ScriptedJudge(_ => 0);
        using var repo = CartRepo(
            new Dictionary<string, string>
            {
                ["pnpm-lock.yaml"] = "changed\n",
                [Login] = "test('signs in', async () => { /* edited */ });\n",
            }
        );
        var result = await Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo));
        AssertResult(
            result,
            [],
            new Dictionary<string, double> { [Adds.Id] = 0.5, [SignsIn.Id] = 1 },
            "pnpm-lock.yaml changed, so the decision filter left nothing out"
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Keeps_the_tests_whose_test_file_setup_file_or_stored_screenshots_changed_without_asking_and_asks_about_the_rest()
    {
        // macOS keeps the bytes a file was created with, while git names it in NFC: committed composed, handed over decomposed.
        const string Setup = "tests/café.setup.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = "test('adds an item', async () => {});\n",
                [Login] = "test('signs in', async () => {});\n",
                [Search] = "test('finds a product', async () => {});\n",
                [Setup] = "test.setup('sign in', async () => {});\n",
                ["tests/other.e2e.ts"] = "test('other', async () => {});\n",
            },
            new Dictionary<string, string>
            {
                [Cart] = "test('adds an item', async () => { /* edited */ });\n",
                [Setup] = "test.setup('sign in', async () => { /* edited */ });\n",
                [$"{Search}-snapshots/home.png"] = "png\n",
                ["src/cart.ts"] = "export const total = 2;\n",
            }
        );
        var other = Candidate("tests/other.e2e.ts", "other");
        TestFilterCandidate[] tests = [Adds, WithSetup(SignsIn, "tests/café.setup.e2e.ts"), Finds, other];
        var model = ScriptedJudge(_ => 0);
        var context = FilterContext(repo, out var logs);
        var result = await Filter(model).FilterAsync(tests, context);
        AssertResult(
            result,
            new Dictionary<string, string> { [other.Id] = "0% likely affected" },
            new Dictionary<string, double>
            {
                [Adds.Id] = 1,
                [SignsIn.Id] = 1,
                [Finds.Id] = 1,
                [other.Id] = 0,
            },
            "3 tests ran without asking: the test file, setup file, or stored screenshots changed; 1 call, 10 tokens"
        );
        Assert.Equal(["asking scripted-judge about 1 test in 1 file"], logs);
        Assert.Equal(
            [
                ["tests/other.e2e.ts"],
            ],
            Keys(model.Requests)
        );
    }

    [Fact]
    public async Task Makes_no_call_when_the_change_touched_every_test_it_was_given()
    {
        var model = ScriptedJudge(_ => 0);
        using var repo = CartRepo(
            new Dictionary<string, string> { [Login] = "test('signs in', async () => { /* edited */ });\n" }
        );
        var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
        AssertResult(
            result,
            [],
            new Dictionary<string, double> { [SignsIn.Id] = 1 },
            "1 test ran without asking: the test file, setup file, or stored screenshots changed"
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Maps_a_project_below_the_repository_root_its_own_test_setup_and_screenshot_changes_run_without_asking()
    {
        const string Project = "packages/app";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/shared.ts"] = "export const x = 1;\n",
                [$"{Project}/{Cart}"] = "test('adds an item', async () => {});\n",
                [$"{Project}/{Login}"] = "test('signs in', async () => {});\n",
                [$"{Project}/{Search}"] = "test('finds a product', async () => {});\n",
                [$"{Project}/tests/auth.setup.e2e.ts"] = "test.setup('sign in', async () => {});\n",
                // The same paths outside the project are another project's files.
                [Cart] = "test('elsewhere', async () => {});\n",
            },
            new Dictionary<string, string>
            {
                ["src/shared.ts"] = "export const x = 2;\n",
                [$"{Project}/{Cart}"] = "test('adds an item', async () => { /* edited */ });\n",
                [$"{Project}/tests/auth.setup.e2e.ts"] = "test.setup('sign in', async () => { /* edited */ });\n",
                [$"{Project}/{Search}-snapshots/home.png"] = "png\n",
                [Login] = "test('elsewhere', async () => {});\n",
            },
            Project
        );
        var other = Candidate("tests/other.e2e.ts", "other");
        File.WriteAllText(Path.Combine(repo.ProjectRoot, "tests/other.e2e.ts"), "test('other', async () => {});\n");
        var model = ScriptedJudge(_ => 0);
        TestFilterCandidate[] tests = [Adds, WithSetup(SignsIn, "tests/auth.setup.e2e.ts"), Finds, other];
        var result = await Filter(model).FilterAsync(tests, FilterContext(repo));
        Assert.Equal(new Dictionary<string, string> { [other.Id] = "0% likely affected" }, result.Exclude);
        Assert.Contains("3 tests ran without asking", result.Note);
        Assert.Equal(
            [
                [$"{Project}/tests/other.e2e.ts"],
            ],
            Keys(model.Requests)
        );
    }

    [Fact]
    public async Task Leaves_nothing_out_for_the_projects_own_config_or_lockfile_or_one_above_it_and_asks_about_a_sibling_projects_or_a_docs_folders()
    {
        const string Project = "apps/web";
        Dictionary<string, string> BaseFiles() =>
            new()
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [$"{Project}/{Login}"] = "test('signs in', async () => {});\n",
            };
        foreach (
            var file in new[]
            {
                $"{Project}/e2e.config.ts",
                $"{Project}/ios/Podfile.lock",
                "apps/pnpm-lock.yaml",
                "pnpm-lock.yaml",
            }
        )
        {
            var model = ScriptedJudge(_ => 0);
            using var repo = Repo(BaseFiles(), new Dictionary<string, string> { [file] = "changed\n" }, Project);
            var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
            AssertResult(
                result,
                [],
                new Dictionary<string, double> { [SignsIn.Id] = 0.5 },
                $"{file} changed, so the decision filter left nothing out"
            );
            Assert.Empty(model.Requests);
        }

        foreach (
            var file in new[]
            {
                "apps/admin/e2e.config.ts",
                "apps/admin/pnpm-lock.yaml",
                "docs/examples/e2e.config.ts",
                "apps/web-old/yarn.lock",
            }
        )
        {
            var model = ScriptedJudge(_ => 0);
            using var repo = Repo(BaseFiles(), new Dictionary<string, string> { [file] = "changed\n" }, Project);
            var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
            Assert.Equal(new Dictionary<string, string> { [SignsIn.Id] = "0% likely affected" }, result.Exclude);
            Assert.Single(model.Requests);
        }
    }

    [Fact]
    public async Task Leaves_nothing_out_making_no_call_when_the_config_the_run_loaded_changed_whatever_its_name_and_goes_by_name_for_any_other()
    {
        const string Project = "apps/web";
        Dictionary<string, string> BaseFiles() =>
            new()
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [$"{Project}/{Login}"] = "test('signs in', async () => {});\n",
            };
        var loaded = $"{Project}/e2e.ci.config.ts";
        foreach (
            var (file, configFile) in new[] { (loaded, loaded), ("config/café.config.ts", "config/café.config.ts") }
        )
        {
            var model = ScriptedJudge(_ => 0);
            using var repo = Repo(BaseFiles(), new Dictionary<string, string> { [file] = "changed\n" }, Project);
            var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo, configFile));
            AssertResult(
                result,
                [],
                new Dictionary<string, double> { [SignsIn.Id] = 0.5 },
                $"{file} changed, so the decision filter left nothing out"
            );
            Assert.Empty(model.Requests);
        }

        // Another config of the project, one an e2e without `ConfigFile` loaded, goes to the model unless it is named like an e2e config.
        foreach (var configFile in new string?[] { $"{Project}/e2e.config.ts", null })
        {
            var model = ScriptedJudge(_ => 0);
            using var repo = Repo(BaseFiles(), new Dictionary<string, string> { [loaded] = "changed\n" }, Project);
            var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo, configFile));
            Assert.Equal(new Dictionary<string, string> { [SignsIn.Id] = "0% likely affected" }, result.Exclude);
            Assert.Single(model.Requests);
        }
    }

    [Fact]
    public async Task Sends_a_renamed_file_as_a_rename_and_a_deleted_file_without_its_lines()
    {
        var body = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"export const line{i} = {i};"));
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/old.ts"] = body + "\n",
                ["src/gone.ts"] = "export const gone = true;\n",
                [Login] = "test('signs in', async () => {});\n",
            },
            []
        );
        Git(repo.Root, "mv", "src/old.ts", "src/new.ts");
        Git(repo.Root, "rm", "--quiet", "src/gone.ts");
        CommitAll(repo);
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model)
            .FilterAsync([SignsIn], FilterContext(repo, files: ["src/gone.ts", "src/new.ts", "src/old.ts"]));
        var diff = Diff(model.Requests[0]);
        Assert.Contains("rename from src/old.ts\nrename to src/new.ts\n", diff);
        Assert.Contains("deleted file mode 100644", diff);
        Assert.DoesNotContain("line0", diff);
        Assert.DoesNotContain("gone = true", diff);
    }

    [Fact]
    public async Task Diffs_the_whole_repository_from_its_root_and_names_each_test_file_by_its_path_from_there_as_the_diff_does()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["libs/shared/cart.ts"] = "export const total = 1;\n",
                [$"packages/app/{Cart}"] = "test('adds an item', async () => {});\n",
            },
            new Dictionary<string, string> { ["libs/shared/cart.ts"] = "export const total = 2;\n" },
            "packages/app",
            new Dictionary<string, string> { ["diff.relative"] = "true" }
        );
        var model = ScriptedJudge(_ => 0.1);
        var result = await Filter(model).FilterAsync([Adds], FilterContext(repo));
        AssertResult(
            result,
            new Dictionary<string, string> { [Adds.Id] = "10% likely affected" },
            new Dictionary<string, double> { [Adds.Id] = 0.1 },
            "1 call, 10 tokens"
        );
        var request = model.Requests[0];
        Assert.Contains("+++ b/libs/shared/cart.ts\n", Diff(request));
        Assert.Contains("+export const total = 2;\n", Diff(request));
        Assert.Equal(
            new Dictionary<string, string> { [$"packages/app/{Cart}"] = "test('adds an item', async () => {});\n" },
            Sent(request)
        );
        Assert.Equal(
            new Dictionary<string, string> { ["t0"] = Asked($"packages/app/{Cart}", "cart", "adds an item") },
            request.Instructions
        );
    }

    [Fact]
    public async Task Asks_once_about_the_setup_that_saves_the_sessions_of_tests_and_keeps_each_test_it_or_its_setup_is_likely_to_affect()
    {
        const string Setup = "tests/auth.setup.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/login.ts"] = "export const ok = 1;\n",
                [Cart] = "test('adds an item', async () => {});\ntest('removes an item', async () => {});\n",
                [Search] = "test('finds a product', async () => {});\n",
                [Setup] = "setup('signs in', async () => {});\n",
            },
            new Dictionary<string, string> { ["src/login.ts"] = "export const ok = 2;\n" }
        );
        var setupAsked =
            $"Can the diff break the e2e setup in {Setup}, or the session it saves for the tests that use it? testFiles[\"{Setup}\"] is its file.";
        TestFilterCandidate[] tests = [WithSetup(Adds, Setup), WithSetup(Removes, Setup), Finds];
        var model = ScriptedJudge(text => text == setupAsked ? 0.6 : 0.1);
        AssertResult(
            await Filter(model).FilterAsync(tests, FilterContext(repo)),
            new Dictionary<string, string> { [Finds.Id] = "10% likely affected" },
            // A test is as likely to fail as the likelier of itself and its setup.
            new Dictionary<string, double>
            {
                [Adds.Id] = 0.6,
                [Removes.Id] = 0.6,
                [Finds.Id] = 0.1,
            },
            "1 call, 10 tokens"
        );
        Assert.Single(model.Requests[0].Instructions.Values, text => text == setupAsked);
        Assert.Equal([Cart, Search, Setup], Sent(model.Requests[0]).Keys);

        var unlikely = ScriptedJudge(text => text == setupAsked ? 0.1 : 0.05);
        var judged = await Filter(unlikely).FilterAsync(tests, FilterContext(repo));
        Assert.Equal(
            new Dictionary<string, string>
            {
                [Adds.Id] = "10% likely affected",
                [Removes.Id] = "10% likely affected",
                [Finds.Id] = "5% likely affected",
            }.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            judged.Exclude.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
        Assert.Equal(
            new Dictionary<string, double>
            {
                [Adds.Id] = 0.1,
                [Removes.Id] = 0.1,
                [Finds.Id] = 0.05,
            }.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            judged.Relevance!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
    }

    [Theory]
    [InlineData(
        "is too large to send with the diff",
        90_000,
        "tests/auth.setup.e2e.ts is too large to send with the diff; 1 call, 10 tokens"
    )]
    [InlineData("went to a call that failed", 45_000, "Refused: content policy; 1 call, 10 tokens")]
    public async Task Keeps_a_test_whose_setup_the_model_did_not_judge_as_when_the_setup(
        string name,
        int size,
        string why
    )
    {
        const string Setup = "tests/auth.setup.e2e.ts";

        // A test file this large goes to a call of its own, so only the setup goes unanswered.
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/login.ts"] = "export const ok = 1;\n",
                [Cart] = Sized("adds an item", 45_000),
                [Setup] = Sized("signs in", size),
            },
            new Dictionary<string, string> { ["src/login.ts"] = "export const ok = 2;\n" }
        );
        var model = ScriptedJudge(
            _ => 0,
            (_, request) =>
                name.StartsWith("went", StringComparison.Ordinal) && Sent(request).ContainsKey(Setup)
                    ? throw new InvalidOperationException("Refused: content policy")
                    : Task.CompletedTask
        );
        var result = await Filter(model).FilterAsync([WithSetup(Adds, Setup)], FilterContext(repo));
        AssertResult(
            result,
            [],
            new Dictionary<string, double> { [Adds.Id] = 0.5 },
            $"scripted-judge did not judge 1 test in 1 file: {why}"
        );
    }

    [Fact]
    public async Task Scores_a_test_whose_setup_the_model_did_not_judge_as_the_likelier_of_its_own_answer_and_one_in_the_middle()
    {
        const string Setup = "tests/auth.setup.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/login.ts"] = "export const ok = 1;\n",
                [Cart] = Sized("adds an item", 45_000),
                [Setup] = Sized("signs in", 90_000),
            },
            new Dictionary<string, string> { ["src/login.ts"] = "export const ok = 2;\n" }
        );
        var model = ScriptedJudge(instructions => instructions.Contains("adds", StringComparison.Ordinal) ? 0.9 : 0.1);
        var result = await Filter(model)
            .FilterAsync([WithSetup(Adds, Setup), WithSetup(Removes, Setup)], FilterContext(repo));
        Assert.Empty(result.Exclude);
        Assert.Equal(
            new Dictionary<string, double> { [Adds.Id] = 0.9, [Removes.Id] = 0.5 }.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            ),
            result.Relevance!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Scores_every_test_it_is_handed_1_for_one_the_change_touches_the_models_answer_for_one_it_judged_and_0_5_for_one_it_did_not()
    {
        const string Huge = "tests/huge.e2e.ts";
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = "test('adds an item', async () => {});\n",
                [Login] = "test('signs in', async () => {});\n",
                [Huge] = Sized("scrolls", 90_000),
            },
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                [Cart] = "test('adds an item', async () => { /* edited */ });\n",
            }
        );
        var scrolls = Candidate(Huge, "scrolls");
        var model = ScriptedJudge(_ => 0.35);
        var result = await Filter(model).FilterAsync([Adds, SignsIn, scrolls], FilterContext(repo));
        Assert.Equal(
            new Dictionary<string, double>
            {
                [Adds.Id] = 1,
                [SignsIn.Id] = 0.35,
                [scrolls.Id] = 0.5,
            }.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            result.Relevance!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Sends_the_provider_options_it_is_given_with_every_call()
    {
        var providerOptions = new Dictionary<string, JsonElement>
        {
            ["gateway"] = JsonDocument.Parse("""{"zeroDataRetention":true}""").RootElement,
        };
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0.9);
        await Filter(model, providerOptions).FilterAsync([Adds], FilterContext(repo));
        Assert.Equal("""{"zeroDataRetention":true}""", model.Requests[0].ProviderOptions!["gateway"].GetRawText());
    }

    [Fact]
    public async Task Leaves_out_a_test_under_20_percent_likely_affected_and_keeps_one_at_20_percent()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(instructions => instructions.Contains("adds", StringComparison.Ordinal) ? 0.19 : 0.2);
        var result = await Filter(model).FilterAsync([Adds, Removes], FilterContext(repo));
        AssertResult(
            result,
            new Dictionary<string, string> { [Adds.Id] = "19% likely affected" },
            new Dictionary<string, double> { [Adds.Id] = 0.19, [Removes.Id] = 0.2 },
            "1 call, 10 tokens"
        );
    }

    [Fact]
    public async Task Adds_up_what_the_calls_spent_their_tokens_and_the_AI_Gateways_cost()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = Sized("adds an item", 45_000),
                [Login] = Sized("signs in", 45_000),
                [Search] = Sized("finds a product", 45_000),
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(
            _ => 0.9,
            inputTokens: 12_400,
            providerMetadata: _ => """{"gateway":{"cost":"0.0021","marketCost":"0.0042"}}"""
        );
        var result = await Filter(model).FilterAsync([Adds, SignsIn, Finds], FilterContext(repo));
        AssertResult(result, [], Scored([Adds, SignsIn, Finds], 0.9), "3 calls, 37.2k tokens, $0.0063");
    }

    [Fact]
    public async Task Reads_a_token_count_that_rounds_up_to_a_million_as_millions()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0.9, inputTokens: 999_950);
        var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
        Assert.Equal("1 call, 1.0M tokens", result.Note);
    }

    [Fact]
    public async Task Counts_the_list_price_when_the_AI_Gateway_bills_a_route_on_your_own_provider_key()
    {
        using var repo = CartRepo();
        var model = ScriptedJudge(_ => 0.9, providerMetadata: _ => """{"gateway":{"cost":"0","marketCost":"0.25"}}""");
        var result = await Filter(model).FilterAsync([SignsIn], FilterContext(repo));
        AssertResult(result, [], new Dictionary<string, double> { [SignsIn.Id] = 0.9 }, "1 call, 10 tokens, $0.25");
    }

    [Fact]
    public async Task Shows_no_cost_when_a_call_that_answered_reported_none_rather_than_a_partial_sum()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 1;\n",
                [Cart] = Sized("adds an item", 45_000),
                [Login] = Sized("signs in", 45_000),
            },
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(
            _ => 0.9,
            providerMetadata: call => call == 0 ? """{"gateway":{"cost":"0.5"}}""" : null
        );
        var result = await Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo));
        Assert.Equal(2, model.Requests.Count);
        AssertResult(result, [], Scored([Adds, SignsIn], 0.9), "2 calls, 20 tokens");
    }

    [Theory]
    [InlineData("a tracked change")]
    [InlineData("tracked changes to two files, each under it")]
    [InlineData("a change of fewer characters that take more bytes")]
    public async Task Makes_no_call_and_leaves_nothing_out_when_the_change_puts_the_diff_over_60_kB(string name)
    {
        var change = name switch
        {
            "a tracked change" => new Dictionary<string, string> { ["src/cart.ts"] = new string('x', 60_000) + "\n" },
            "tracked changes to two files, each under it" => new Dictionary<string, string>
            {
                ["src/cart.ts"] = new string('x', 30_000) + "\n",
                [Search] = new string('x', 30_000) + "\n",
            },
            _ => new Dictionary<string, string> { ["src/cart.ts"] = new string('日', 20_001) + "\n" },
        };
        using var repo = CartRepo(change);
        var model = ScriptedJudge(_ => 0);
        var result = await Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo));
        AssertResult(
            result,
            [],
            Scored([Adds, SignsIn], 0.5),
            "the diff is over 60 kB, so the decision filter left nothing out"
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Leaves_a_failed_calls_tests_in_and_judges_the_rest()
    {
        var files = Enumerable.Range(0, 4).Select(i => $"tests/t{i}.e2e.ts").ToList();
        var baseFiles = new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 1;\n" };
        foreach (var file in files)
        {
            baseFiles[file] = Sized("runs", 45_000);
        }

        using var repo = Repo(
            baseFiles,
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(
            _ => 0,
            (_, request) =>
                Sent(request).Keys.First() == files[1]
                    ? throw new InvalidOperationException("Refused: content policy\nsee the docs")
                    : Task.CompletedTask
        );
        var tests = files.Select(file => Candidate(file, "runs")).ToList();
        var result = await Filter(model).FilterAsync(tests, FilterContext(repo));
        AssertResult(
            result,
            new Dictionary<string, string>
            {
                [tests[0].Id] = "0% likely affected",
                [tests[2].Id] = "0% likely affected",
                [tests[3].Id] = "0% likely affected",
            },
            new Dictionary<string, double>
            {
                [tests[0].Id] = 0,
                [tests[1].Id] = 0.5,
                [tests[2].Id] = 0,
                [tests[3].Id] = 0,
            },
            "scripted-judge did not judge 1 test in 1 file: Refused: content policy; 3 calls, 30 tokens"
        );
        Assert.Equal(4, model.Requests.Count);
    }

    /// <summary>
    /// Partial: upstream pins exactly 8 requests, which holds because its calls run on one thread, so the failures of 3 calls land before a
    /// worker starts its next. Workers here run in parallel, so a worker whose call failed first may start one more call before the third
    /// failure stops the rest: at most 2 more.
    /// </summary>
    [Fact]
    public async Task Partial_makes_no_more_calls_once_3_have_failed_stops_the_ones_in_flight_and_throws_the_first_failures_first_line_when_none_answered()
    {
        var files = Enumerable.Range(0, 12).Select(i => $"tests/t{i}.e2e.ts").ToList();
        var baseFiles = new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 1;\n" };
        foreach (var file in files)
        {
            baseFiles[file] = Sized("runs", 45_000);
        }

        using var repo = Repo(
            baseFiles,
            new Dictionary<string, string> { ["src/cart.ts"] = "export const total = 2;\n" }
        );
        var model = ScriptedJudge(
            _ => 0,
            async (_, request) =>
            {
                var file = Sent(request).Keys.FirstOrDefault() ?? "";
                if (files.Take(3).Contains(file))
                {
                    // Let the other calls start first, as upstream's single thread does.
                    await Task.Delay(200 + (50 * files.IndexOf(file)));
                    throw new InvalidOperationException($"Unauthorized: no AI Gateway key for {file}\nsee the docs");
                }

                await Task.Delay(Timeout.Infinite, request.AbortSignal);
            }
        );
        var error = await Assert.ThrowsAsync<TestFilterException>(() =>
            Filter(model).FilterAsync([.. files.Select(file => Candidate(file, "runs"))], FilterContext(repo))
        );
        Assert.Equal("Unauthorized: no AI Gateway key for tests/t0.e2e.ts", error.Message);
        Assert.InRange(model.Requests.Count, 8, 10);
        Assert.All(model.Requests, request => Assert.True(request.AbortSignal.IsCancellationRequested));
    }

    /// <summary>
    /// Partial: upstream also checks that the scores sit on a non-enumerable property, so logging or <c>JSON.stringify</c> of the error does not
    /// list every test; C# has no such property, and an exception serializes by its own members.
    /// </summary>
    [Fact]
    public async Task Partial_carries_the_scores_the_change_alone_gives_on_the_error_it_throws_when_every_call_failed_so_the_run_still_starts_the_tests_the_change_touches_first()
    {
        using var repo = CartRepo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                [Login] = "test('signs in', async () => { /* edited */ });\n",
            }
        );
        var model = ScriptedJudge(
            _ => 0,
            (_, _) => throw new InvalidOperationException("Unauthorized: no AI Gateway key\nsee the docs")
        );
        var error = await Assert.ThrowsAsync<TestFilterException>(() =>
            Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo))
        );
        Assert.Equal("Unauthorized: no AI Gateway key", error.Message);
        Assert.Equal(
            new Dictionary<string, double> { [Adds.Id] = 0.5, [SignsIn.Id] = 1 }.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            ),
            error.Relevance!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
        Assert.Single(model.Requests);
    }

    /// <summary>Partial: git's error carries <c>cmd</c> upstream; here it is the inner <c>GitFailure</c>, whose message names the command.</summary>
    [Fact]
    public async Task Partial_carries_the_same_scores_when_git_cannot_diff_against_the_base_the_original_error_and_its_message_kept()
    {
        using var source = CartRepo(
            new Dictionary<string, string>
            {
                ["src/cart.ts"] = "export const total = 2;\n",
                [Login] = "test('signs in', async () => { /* edited */ });\n",
            }
        );
        var repo = source with { Base = new string('0', 40) };
        var model = ScriptedJudge(_ => 0);
        var error = await Assert.ThrowsAsync<TestFilterException>(() =>
            Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo))
        );
        Assert.StartsWith("Command failed: git diff --name-only ", error.Message, StringComparison.Ordinal);
        Assert.IsType<E2E.Internal.GitFailure>(error.InnerException);
        Assert.Equal(
            new Dictionary<string, double> { [Adds.Id] = 0.5, [SignsIn.Id] = 1 }.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            ),
            error.Relevance!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Retries_a_call_that_fails_with_a_retryable_error_up_to_3_times()
    {
        var busy = new ApiCallException(
            "Too many requests",
            429,
            new Dictionary<string, string> { ["retry-after-ms"] = "0" }
        );
        var model = ScriptedJudge(_ => 0, (_, _) => throw busy);
        using var repo = CartRepo();
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            Filter(model).FilterAsync([Adds, Removes, SignsIn], FilterContext(repo))
        );
        Assert.Contains("Failed after 4 attempts. Last error: AI_APICallError: Too many requests", error.Message);
        Assert.Equal(4, model.Requests.Count);
    }

    [Fact]
    public async Task Throws_making_no_call_when_git_cannot_diff_against_the_base()
    {
        var model = ScriptedJudge(_ => 0);
        using var source = CartRepo();
        var repo = source with { Base = new string('0', 40) };
        var error = await Assert.ThrowsAsync<TestFilterException>(() =>
            Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo))
        );
        Assert.StartsWith("Command failed: git diff --name-only ", error.Message, StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Ends_with_the_abort_when_the_run_stops_waiting_for_it()
    {
        using var run = new CancellationTokenSource();
        var model = ScriptedJudge(
            _ => 0,
            (_, _) =>
            {
                run.Cancel();
                throw new InvalidOperationException("This operation was aborted");
            }
        );
        using var repo = CartRepo();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo, signal: run.Token))
        );

        // The abort is the run's own, not an error of the filter's to carry scores on.
        Assert.Equal(run.Token, error.CancellationToken);
        Assert.IsNotType<TestFilterException>(error);
    }

    [Fact]
    public async Task Ends_with_the_abort_making_no_call_when_the_run_stopped_waiting_before_git_answered()
    {
        var model = ScriptedJudge(_ => 0);
        using var repo = CartRepo();
        using var run = new CancellationTokenSource();
        await run.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Filter(model).FilterAsync([Adds, SignsIn], FilterContext(repo, signal: run.Token))
        );
        Assert.Empty(model.Requests);
    }

    [Fact]
    public void Accepts_the_AI_Gateways_typesafe_ai_jev() =>
        Assert.Equal(
            "decision",
            Filter(new E2E.Decision.Sdk.Gateway.GatewayProvider().DecisionModel("typesafe-ai/jev")).Name
        );

    [Fact]
    public void Refuses_no_options_at_config_load_as_INVALID_CONFIG_naming_DecisionFilter() =>
        AssertInvalidConfig(
            () => E2E.Decision.Filter.DecisionFilter(null!),
            $"DecisionFilter() takes an options object; {Example}"
        );

    [Fact]
    public void Refuses_a_decision_model_without_boolean_questions_at_config_load_as_INVALID_CONFIG_naming_DecisionFilter() =>
        AssertInvalidConfig(
            () => Filter(ScriptedDecision((_, _, _) => Pick("done")).Model),
            "DecisionFilter({ model }) got scripted/scripted-1, which does not answer boolean questions"
        );

    [Fact]
    public void Refuses_provider_options_that_are_not_objects_by_provider_at_config_load_as_INVALID_CONFIG_naming_DecisionFilter() =>
        AssertInvalidConfig(
            () =>
                Filter(
                    ScriptedJudge(_ => 0),
                    new Dictionary<string, JsonElement> { ["gateway"] = JsonDocument.Parse("true").RootElement }
                ),
            "DecisionFilter({ providerOptions }) maps provider names to option objects, e.g. { gateway: { zeroDataRetention: true } }"
        );
}
