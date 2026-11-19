// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Decision.Sdk;

namespace E2E.Decision;

/// <summary>Upstream's <c>filter.ts</c>.</summary>
public static partial class Filter
{
    /// <summary>A test the model rates less likely than this to be affected is left out.</summary>
    private const double MinProbability = 0.2;

    /// <summary>The relevance of a test whose file, setup file, or stored screenshots the change touches: the likeliest to fail.</summary>
    private const double TouchedRelevance = 1;

    /// <summary>The relevance of a test, or of the setup it uses, that the model did not judge: neither likely nor unlikely.</summary>
    private const double UnjudgedRelevance = 0.5;

    /// <summary>Most UTF-8 bytes of state, the diff and the files, one call sends.</summary>
    private const int MaxStateBytes = 80_000;

    /// <summary>A longer diff is not sent, and the filter leaves nothing out: each call keeps room for the files.</summary>
    private const int MaxDiffBytes = 60_000;

    /// <summary>Most questions one call asks.</summary>
    private const int MaxQuestions = 200;

    /// <summary>Most calls in flight at once.</summary>
    private const int Concurrency = 8;

    /// <summary>Failed calls after which no more are made: the model or its key is likely down.</summary>
    private const int MaxFailures = 3;

    /// <summary>What <c>git diff</c> may print, far past <see cref="MaxDiffBytes"/> so a long diff is measured rather than lost.</summary>
    private const int MaxDiffBuffer = 64 * 1024 * 1024;

    /// <summary>Most characters of paths one <c>git diff</c> names, well within the 32,767 of a Windows command line.</summary>
    private const int MaxPathspecChars = 16_000;

    /// <summary>What follows a test file's name in the directory of its stored screenshots.</summary>
    private const string SnapshotsSuffix = "-snapshots/";

    /// <summary>How a valid model reads, for every model error.</summary>
    private const string Example =
        "pass a decision model, e.g. DecisionFilter(new DecisionFilterOptions { Model = new OpenAIDecisionModel(...) })";

    /// <summary>Plain <c>git diff</c> output whatever the repository's config says.</summary>
    private static readonly string[] DiffFlags = ["--no-color", "--no-ext-diff", "--no-textconv", "-U3"];

    /// <summary>
    /// Files that can change what any test does, by name at any depth: the config files e2e discovers, the lockfiles of every package
    /// manager, and Gradle's version catalog. A change to one leaves nothing out.
    /// </summary>
    private static readonly HashSet<string> RunAll = new(StringComparer.Ordinal)
    {
        "e2e.config.ts",
        "e2e.config.mts",
        "e2e.config.js",
        "e2e.config.mjs",
        "package-lock.json",
        "npm-shrinkwrap.json",
        "pnpm-lock.yaml",
        "yarn.lock",
        "bun.lock",
        "bun.lockb",
        "deno.lock",
        "Podfile.lock",
        "Package.resolved",
        "Cartfile.resolved",
        "gradle.lockfile",
        "buildscript-gradle.lockfile",
        "libs.versions.toml",
        "Gemfile.lock",
        "poetry.lock",
        "uv.lock",
        "Pipfile.lock",
        "pdm.lock",
        "go.sum",
        "Cargo.lock",
        "composer.lock",
        "pubspec.lock",
        "mix.lock",
        "packages.lock.json",
        "flake.lock",
        "MODULE.bazel.lock",
    };

    private static readonly JsonSerializerOptions Quoting = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Upstream's <c>decisionFilter</c>. A test filter named <c>decision</c> that asks the decision model, test by test, whether the change
    /// can affect what it checks, and whether it can break the setup that saves the test's session, and leaves out the tests unlikely on
    /// both counts. A change to the config the run loaded, any file named like an e2e config, or a lockfile leaves nothing out, and a test
    /// whose test file, setup file, or stored screenshots changed always runs. A test the model does not judge runs, and the note says
    /// why. It also scores every test it is handed, so the run starts the likeliest to fail first: 1 for a test the change touches, the
    /// model's probability for one it judged, and 0.5 for one it did not. When every call fails, as without a key, or git or a file read
    /// fails, the filter throws a <see cref="TestFilterException"/>, so the run warns and runs every test; the exception carries the
    /// scores the change alone gives, so the tests it touches still start first.
    /// </summary>
    public static ITestFilter DecisionFilter(DecisionFilterOptions options)
    {
        if (options is null)
        {
            throw new ConfigurationException("INVALID_CONFIG", "DecisionFilter() takes an options object; " + Example);
        }

        Model.CheckModel(options.Model, new ModelNeeds("DecisionFilter", "boolean", Example));
        Model.CheckProviderOptions(options.ProviderOptions, "DecisionFilter");
        return new DecisionTestFilter(
            new DecisionFilterOptions { Model = options.Model, ProviderOptions = options.ProviderOptions }
        );
    }

    /// <summary>
    /// Leaves nothing out when the loaded config or a <c>RunAll</c> file of the project changed, runs the tests the change touches directly
    /// without asking, and asks the model about the rest, and the setup files that save their sessions, against the redacted diff and the
    /// redacted files, in calls of at most <see cref="MaxQuestions"/>, <see cref="Concurrency"/> at a time. A test is left out when both it
    /// and its setup are unlikely to be affected. A failed call leaves its tests in, and after <see cref="MaxFailures"/> no more calls are
    /// made. Every test it is handed is scored for the run's order, however it ends. Throws when git or a file read fails, the first
    /// failure's first line when calls fail and none answers, and on an abort.
    /// </summary>
    private static async Task<TestFilterResult> Judge(
        IReadOnlyList<TestFilterCandidate> given,
        TestFilterContext context,
        DecisionFilterOptions options
    )
    {
        var (changes, signal, log, redact) = (context.Changes, context.Signal, context.Log, context.Redact);
        var touched = TouchedBy(context);
        var tests = given.Where(test => !touched(test.File) && !touched(test.SetupFile)).ToList();
        var relevance = UnaskedRelevance(given, touched);
        var everything = RunAllFile(context);
        if (everything is not null)
        {
            return Result(
                new Dictionary<string, string>(),
                relevance,
                everything + " changed, so the decision filter left nothing out"
            );
        }

        var unasked =
            tests.Count == given.Count
                ? null
                : Counted(given.Count - tests.Count, "test")
                    + " ran without asking: the test file, setup file, or stored screenshots changed";

        // A wrapper may hand on no tests, or the change touched them all: nothing to ask, and no diff to read.
        if (tests.Count == 0)
        {
            return Result(new Dictionary<string, string>(), relevance, unasked);
        }

        var modelId = options.Model.ModelId;
        log("asking " + modelId + " about " + TestsIn(tests));
        var raw = await ChangeDiff(changes, signal).ConfigureAwait(false);
        if (raw.Length == 0)
        {
            return Result(
                new Dictionary<string, string>(),
                relevance,
                "git printed no diff of the changed files, so the decision filter left nothing out"
            );
        }

        if (Encoding.UTF8.GetByteCount(raw) > MaxDiffBytes)
        {
            return Result(
                new Dictionary<string, string>(),
                relevance,
                "the diff is over "
                    + (MaxDiffBytes / 1000).ToString(CultureInfo.InvariantCulture)
                    + " kB, so the decision filter left nothing out"
            );
        }

        var diff = redact(raw);
        var (batches, tooLarge) = Pack(
            await Subjects(tests, context).ConfigureAwait(false),
            Encoding.UTF8.GetByteCount(diff)
        );
        var (answers, spend, failures) = await AskAll(batches, diff, options, signal).ConfigureAwait(false);
        if (spend.Calls == 0 && failures.Count > 0)
        {
            throw new InvalidOperationException(FirstLine(failures[0]), failures[0]);
        }

        var verdict = Verdicts(tests, answers);
        var unjudged =
            verdict.Missed.Count == 0
                ? null
                : modelId + " did not judge " + TestsIn(verdict.Missed) + ": " + UnjudgedBecause(failures, tooLarge);
        var scores = new Dictionary<string, double>(relevance);
        foreach (var (id, score) in verdict.Relevance)
        {
            scores[id] = score;
        }

        return Answered(verdict.Exclude, scores, [unasked, unjudged], spend);
    }

    /// <summary>How likely each of <paramref name="given"/> is to fail before the model says anything: <see cref="TouchedRelevance"/> for a test the change touches, <see cref="UnjudgedRelevance"/> for the rest.</summary>
    private static Dictionary<string, double> UnaskedRelevance(
        IReadOnlyList<TestFilterCandidate> given,
        Func<string?, bool> touched
    )
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var test in given)
        {
            scores[test.Id] = touched(test.File) || touched(test.SetupFile) ? TouchedRelevance : UnjudgedRelevance;
        }

        return scores;
    }

    /// <summary>
    /// The tests to leave out, each with its reason; how likely each test is to fail, the higher of the model's two answers, with a missing
    /// one counting as <see cref="UnjudgedRelevance"/>; and the tests the model did not answer for, itself or its setup.
    /// </summary>
    private static (
        Dictionary<string, string> Exclude,
        Dictionary<string, double> Relevance,
        List<TestFilterCandidate> Missed
    ) Verdicts(IReadOnlyList<TestFilterCandidate> tests, Answers answers)
    {
        var exclude = new Dictionary<string, string>(StringComparer.Ordinal);
        var relevance = new Dictionary<string, double>(StringComparer.Ordinal);
        var missed = new List<TestFilterCandidate>();
        foreach (var test in tests)
        {
            var hasOwn = answers.Tests.TryGetValue(test.Id, out var own);
            double? setup = 0;
            if (test.SetupFile is not null)
            {
                setup = answers.Setups.TryGetValue(test.SetupFile, out var answered) ? answered : null;
            }

            if (!hasOwn || setup is null)
            {
                missed.Add(test);
            }
            else if (Math.Max(own, setup.Value) < MinProbability)
            {
                exclude[test.Id] =
                    Math.Round(Math.Max(own, setup.Value) * 100, MidpointRounding.AwayFromZero)
                        .ToString(CultureInfo.InvariantCulture) + "% likely affected";
            }

            relevance[test.Id] = Math.Max(hasOwn ? own : UnjudgedRelevance, setup ?? UnjudgedRelevance);
        }

        return (exclude, relevance, missed);
    }

    /// <summary>Why some tests went unjudged: the first failed call's first line, and the files too large to send with the diff.</summary>
    private static string UnjudgedBecause(IReadOnlyList<Exception> failures, IReadOnlyList<Subject> tooLarge)
    {
        var why = new List<string>();
        if (failures.Count > 0)
        {
            why.Add(FirstLine(failures[0]));
        }

        if (tooLarge.Count > 0)
        {
            why.Add(
                (tooLarge.Count == 1 ? tooLarge[0].Key + " is" : Counted(tooLarge.Count, "file") + " are")
                    + " too large to send with the diff"
            );
        }

        return string.Join("; ", why);
    }

    /// <summary>A path from the repository root, in POSIX form and NFC, as git names files on macOS, for a project-relative <paramref name="file"/>; empty for the project root itself when it is the repository root.</summary>
    private static string RepoPath(TestFilterContext context, string file)
    {
        var relative = Path.GetRelativePath(context.Changes.Root, Path.Combine(context.ProjectRoot, file));
        return (relative == "." ? "" : relative.Replace('\\', '/')).Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// The first changed file that leaves nothing out: the config the run loaded, whatever its name, or a <c>RunAll</c> file that belongs
    /// to the project, one in the project directory or inside it, or in a directory above it, as a workspace root's lockfile is. A sibling
    /// project's config or lockfile, or one in a docs folder outside the project, goes to the model. Without <c>ConfigFile</c> only the
    /// <c>RunAll</c> names apply.
    /// </summary>
    private static string? RunAllFile(TestFilterContext context)
    {
        var project = RepoPath(context, ".");
        var config = context.ConfigFile?.Normalize(NormalizationForm.FormC);
        return context.Changes.Files.FirstOrDefault(file =>
        {
            var normalized = file.Normalize(NormalizationForm.FormC);
            if (normalized == config)
            {
                return true;
            }

            var slash = normalized.LastIndexOf('/');
            if (!RunAll.Contains(slash < 0 ? normalized : normalized[(slash + 1)..]))
            {
                return false;
            }

            var directory =
                slash < 0 ? "."
                : slash == 0 ? "/"
                : normalized[..slash];
            return directory == "."
                || project.Length == 0
                || directory == project
                || project.StartsWith(directory + "/", StringComparison.Ordinal)
                || directory.StartsWith(project + "/", StringComparison.Ordinal);
        });
    }

    /// <summary>Whether a project-relative file, or one of its stored screenshots, is in the change, each compared in NFC.</summary>
    private static Func<string?, bool> TouchedBy(TestFilterContext context)
    {
        var changed = new HashSet<string>(
            context.Changes.Files.Select(file => file.Normalize(NormalizationForm.FormC)),
            StringComparer.Ordinal
        );
        var screenshotted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in changed)
        {
            for (
                var at = file.IndexOf(SnapshotsSuffix, StringComparison.Ordinal);
                at != -1;
                at = file.IndexOf(SnapshotsSuffix, at + 1, StringComparison.Ordinal)
            )
            {
                screenshotted.Add(file[..at]);
            }
        }

        return file =>
        {
            if (file is null)
            {
                return false;
            }

            var key = RepoPath(context, file);
            return changed.Contains(key) || screenshotted.Contains(key);
        };
    }

    /// <summary>
    /// The files the model reads: each test file, then each setup file that saves a test's session, keyed by their redacted path from the
    /// repository root, as the diff names files, with their questions.
    /// </summary>
    private static async Task<List<Subject>> Subjects(
        IReadOnlyList<TestFilterCandidate> tests,
        TestFilterContext context
    )
    {
        var (projectRoot, signal, redact) = (context.ProjectRoot, context.Signal, context.Redact);
        var setups = tests
            .Where(test => test.SetupFile is not null)
            .Select(test => test.SetupFile!)
            .Distinct()
            .ToList();
        var files = tests.Select(test => test.File).Concat(setups).Distinct().ToList();
        var read = await Task.WhenAll(
                files.Select(file => File.ReadAllTextAsync(Path.Combine(projectRoot, file), Encoding.UTF8, signal))
            )
            .ConfigureAwait(false);
        var found = new Dictionary<string, Subject>(StringComparer.Ordinal);
        for (var i = 0; i < files.Count; i++)
        {
            var text = redact(read[i]);
            found[files[i]] = new Subject(redact(RepoPath(context, files[i])), text, Encoding.UTF8.GetByteCount(text));
        }

        foreach (var test in tests)
        {
            var subject = found[test.File];
            subject.Questions.Add(
                new Question(
                    "test",
                    test,
                    null,
                    redact(
                        "Can the diff change what the e2e test \""
                            + string.Join(" > ", test.TitlePath)
                            + "\" in "
                            + subject.Key
                            + " checks? "
                            + FileOf(subject.Key)
                            + ", hooks included."
                    )
                )
            );
        }

        foreach (var file in setups)
        {
            var subject = found[file];
            subject.Questions.Add(
                new Question(
                    "setup",
                    null,
                    file,
                    "Can the diff break the e2e setup in "
                        + subject.Key
                        + ", or the session it saves for the tests that use it? "
                        + FileOf(subject.Key)
                        + "."
                )
            );
        }

        return [.. found.Values];
    }

    /// <summary>
    /// Packs the subjects into calls that each stay within <see cref="MaxStateBytes"/> with the diff and ask at most
    /// <see cref="MaxQuestions"/>, splitting a subject's questions across calls where they are more. Two subjects whose keys are the same,
    /// as two paths that redact alike, go to separate calls, so neither replaces the other. A subject too large to send with the diff is
    /// set apart.
    /// </summary>
    private static (List<Batch> Batches, List<Subject> TooLarge) Pack(IReadOnlyList<Subject> found, int diffBytes)
    {
        var batches = new List<Batch>();
        var tooLarge = new List<Subject>();
        var room = 0;
        foreach (var subject in found)
        {
            if (diffBytes + subject.Bytes > MaxStateBytes)
            {
                tooLarge.Add(subject);
                continue;
            }

            for (var start = 0; start < subject.Questions.Count; start += MaxQuestions)
            {
                var chunk = subject.Questions.Skip(start).Take(MaxQuestions).ToList();
                var last = batches.Count == 0 ? null : batches[^1];
                if (
                    last is null
                    || last.TestFiles.ContainsKey(subject.Key)
                    || subject.Bytes > room
                    || last.Questions.Count + chunk.Count > MaxQuestions
                )
                {
                    batches.Add(new Batch(new Dictionary<string, string> { [subject.Key] = subject.Text }, chunk));
                    room = MaxStateBytes - diffBytes - subject.Bytes;
                }
                else
                {
                    last.TestFiles[subject.Key] = subject.Text;
                    last.Questions.AddRange(chunk);
                    room -= subject.Bytes;
                }
            }
        }

        return (batches, tooLarge);
    }

    /// <summary>
    /// Makes one call per batch, <see cref="Concurrency"/> at a time. A failed call's questions stay unanswered; once
    /// <see cref="MaxFailures"/> calls have failed, the calls in flight stop and no more start. Returns the answers, what the calls that
    /// answered spent, and the failures.
    /// </summary>
    private static async Task<(Answers Answers, Spend Spend, List<Exception> Failures)> AskAll(
        IReadOnlyList<Batch> batches,
        string diff,
        DecisionFilterOptions options,
        CancellationToken signal
    )
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(signal);
        var abortSignal = stop.Token;
        var answers = new Answers();
        var spend = new Spend();
        var failures = new List<Exception>();
        var gate = new object();
        var next = -1;

        async Task Work()
        {
            for (
                var index = Interlocked.Increment(ref next);
                index < batches.Count && !stop.IsCancellationRequested;
                index = Interlocked.Increment(ref next)
            )
            {
                var batch = batches[index];
                try
                {
                    var state = JsonSerializer.SerializeToElement(new { diff, testFiles = batch.TestFiles }, Quoting);
                    var questions = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var i = 0; i < batch.Questions.Count; i++)
                    {
                        questions["t" + i.ToString(CultureInfo.InvariantCulture)] = batch.Questions[i].Text;
                    }

                    var decided = await Decide
                        .DecideBooleansAsync(options, [new DecisionJsonPart(state)], questions, abortSignal)
                        .ConfigureAwait(false);
                    var cost = ReadCost(decided.ProviderMetadata);
                    lock (gate)
                    {
                        for (var i = 0; i < batch.Questions.Count; i++)
                        {
                            var question = batch.Questions[i];
                            var probability =
                                decided.Probabilities.TryGetValue(
                                    "t" + i.ToString(CultureInfo.InvariantCulture),
                                    out var p
                                ) && p is { } value
                                    ? value
                                    : 1;
                            if (question.Kind == "test")
                            {
                                answers.Tests[question.Test!.Id] = probability;
                            }
                            else
                            {
                                answers.Setups[question.File!] = probability;
                            }
                        }

                        spend.Calls += 1;
                        spend.Tokens += (decided.Usage.InputTokens ?? 0) + (decided.Usage.OutputTokens ?? 0);
                        if (cost is null)
                        {
                            spend.Uncosted = true;
                        }
                        else
                        {
                            spend.CostUsd = (spend.CostUsd ?? 0) + cost.Value;
                        }
                    }
                }
                catch (Exception error)
                {
                    signal.ThrowIfCancellationRequested();

                    // A call the stop cut short is not a failure of its own.
                    if (stop.IsCancellationRequested)
                    {
                        return;
                    }

                    lock (gate)
                    {
                        failures.Add(error);
                        if (failures.Count >= MaxFailures)
                        {
                            stop.Cancel();
                        }
                    }
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, Math.Min(Concurrency, batches.Count)).Select(_ => Work()))
            .ConfigureAwait(false);
        return (answers, spend, failures);
    }

    /// <summary>The filter's answer: <paramref name="exclude"/>, the scores, and a note of each of <paramref name="problems"/> given, then what the calls spent, such as <c>11 calls, 307.6k tokens, $0.0129</c>, its cost only when every call reported one.</summary>
    private static TestFilterResult Answered(
        Dictionary<string, string> exclude,
        Dictionary<string, double> relevance,
        IReadOnlyList<string?> problems,
        Spend spend
    )
    {
        var cost = spend.CostUsd is null || spend.Uncosted ? "" : ", " + FormatCost(spend.CostUsd.Value);
        var spent =
            spend.Calls == 0
                ? null
                : Counted(spend.Calls, "call") + ", " + FormatTokens(spend.Tokens) + " tokens" + cost;
        return Result(exclude, relevance, string.Join("; ", problems.Append(spent).Where(part => part is not null)));
    }

    /// <summary>The filter's answer from its parts, leaving out the scores when no test has one and the note when it is empty or absent.</summary>
    private static TestFilterResult Result(
        Dictionary<string, string> exclude,
        Dictionary<string, double> relevance,
        string? note
    ) =>
        new()
        {
            Exclude = exclude,
            Relevance = relevance.Count == 0 ? null : relevance,
            Note = string.IsNullOrEmpty(note) ? null : note,
        };

    /// <summary>
    /// One call's cost in USD, where the provider reports it. The Vercel AI Gateway's <c>cost</c> is what it bills; a route on the user's
    /// own provider key reports <c>cost: "0"</c>, so <c>marketCost</c>, the list price, stands in.
    /// </summary>
    private static double? ReadCost(JsonElement? metadata)
    {
        static double? Usd(JsonElement? raw)
        {
            double? value = raw?.ValueKind switch
            {
                JsonValueKind.String
                    when double.TryParse(
                        raw.Value.GetString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    ) => parsed,
                JsonValueKind.Number => raw.Value.GetDouble(),
                _ => null,
            };
            return value is { } number && double.IsFinite(number) && number >= 0 ? number : null;
        }

        JsonElement? Gateway(string name) =>
            metadata is { ValueKind: JsonValueKind.Object } root
            && root.TryGetProperty("gateway", out var gateway)
            && gateway.ValueKind == JsonValueKind.Object
            && gateway.TryGetProperty(name, out var found)
                ? found
                : null;

        var billed = Usd(Gateway("cost"));
        if (billed is > 0)
        {
            return billed;
        }

        return Usd(Gateway("marketCost")) ?? billed;
    }

    /// <summary>A token count: plain under a thousand, then <c>12.4k</c>, then <c>4.2M</c>.</summary>
    private static string FormatTokens(long count)
    {
        if (count < 1_000)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }

        var hundreds = (long)Math.Round(count / 100.0, MidpointRounding.AwayFromZero);
        return hundreds < 10_000
            ? (hundreds / 10.0).ToString("F1", CultureInfo.InvariantCulture) + "k"
            : (count / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M";
    }

    /// <summary>USD with enough precision for sub-cent calls; <c>&lt;$0.0001</c> for a cost that would round to nothing.</summary>
    private static string FormatCost(double costUsd) =>
        costUsd is > 0 and < 0.00005
            ? "<$0.0001"
            : "$" + costUsd.ToString(costUsd < 0.1 ? "F4" : "F2", CultureInfo.InvariantCulture);

    /// <summary>How a question points the model at the file it is about, by its key in <c>testFiles</c>.</summary>
    private static string FileOf(string key) => "testFiles[" + JsonSerializer.Serialize(key, Quoting) + "] is its file";

    /// <summary>Tests as a note counts them: <c>3 tests in 2 files</c>.</summary>
    private static string TestsIn(IReadOnlyCollection<TestFilterCandidate> tests) =>
        Counted(tests.Count, "test") + " in " + Counted(tests.Select(test => test.File).Distinct().Count(), "file");

    /// <summary>
    /// The change as one unified diff of <c>changes.Files</c>, with renames found and a deleted file's lines left out: what the commits
    /// since <c>changes.Base</c> change, never the working tree, then each file no commit changes by name alone, since CI may have
    /// written it. Stops adding files once the diff is past <see cref="MaxDiffBytes"/>.
    /// </summary>
    private static async Task<string> ChangeDiff(TestFilterChanges changes, CancellationToken signal)
    {
        var (@base, root, files) = (changes.Base, changes.Root, changes.Files);
        var committed = new HashSet<string>(
            (
                await Git(
                        root,
                        ["diff", "--name-only", "--no-renames", "--no-ext-diff", "-z", @base, "HEAD", "--"],
                        signal
                    )
                    .ConfigureAwait(false)
            ).Split('\0'),
            StringComparer.Ordinal
        );
        var diff = new StringBuilder();
        foreach (var paths in Pathspecs(files.Where(committed.Contains).ToList()))
        {
            List<string> args =
            [
                "--literal-pathspecs",
                "diff",
                .. DiffFlags,
                "-M",
                "--irreversible-delete",
                @base,
                "HEAD",
                "--",
                .. paths,
            ];
            diff.Append(await Git(root, args, signal).ConfigureAwait(false));
            if (Encoding.UTF8.GetByteCount(diff.ToString()) > MaxDiffBytes)
            {
                return diff.ToString();
            }
        }

        foreach (var file in files)
        {
            if (committed.Contains(file))
            {
                continue;
            }

            diff.Append("diff --git ")
                .Append(DiffPath("a/" + file))
                .Append(' ')
                .Append(DiffPath("b/" + file))
                .Append("\nnot committed, contents not read\n");
            if (Encoding.UTF8.GetByteCount(diff.ToString()) > MaxDiffBytes)
            {
                break;
            }
        }

        return diff.ToString();
    }

    /// <summary>A path as a diff header names it: quoted, as git quotes one, when it holds a quote, a backslash, or a control character, so a name can't add lines of its own.</summary>
    private static string DiffPath(string file) =>
        NeedsQuotes().IsMatch(file) ? JsonSerializer.Serialize(file, Quoting) : file;

    [GeneratedRegex(@"[""\\\p{Cc}]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NeedsQuotes();

    /// <summary><paramref name="files"/> in runs whose paths together stay within <see cref="MaxPathspecChars"/>, one <c>git diff</c> each.</summary>
    private static List<List<string>> Pathspecs(IReadOnlyList<string> files)
    {
        var runs = new List<List<string>>();
        var run = new List<string>();
        var chars = 0;
        foreach (var file in files)
        {
            if (run.Count > 0 && chars + file.Length > MaxPathspecChars)
            {
                runs.Add(run);
                run = [];
                chars = 0;
            }

            run.Add(file);
            chars += file.Length + 1;
        }

        if (run.Count > 0)
        {
            runs.Add(run);
        }

        return runs;
    }

    /// <summary>What <c>git</c> prints in <paramref name="cwd"/>.</summary>
    private static async Task<string> Git(string cwd, IReadOnlyList<string> args, CancellationToken signal)
    {
        var result = await E2E.Internal.Git.RunGitAsync(cwd, args, signal, MaxDiffBuffer).ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.Stdout
            : throw new E2E.Internal.GitFailure("Command failed: git " + string.Join(' ', args) + "\n" + result.Stderr);
    }

    /// <summary><paramref name="count"/> and <paramref name="noun"/>, plural unless the count is 1.</summary>
    private static string Counted(int count, string noun) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? "" : "s");

    /// <summary>The first non-empty line of what <paramref name="error"/> says.</summary>
    private static string FirstLine(Exception error)
    {
        var line = error.Message.Split('\n').FirstOrDefault(candidate => candidate.Trim().Length > 0);
        return line is null ? error.GetType().Name : line.Trim();
    }

    /// <summary>One question to the model, redacted: whether the change can affect a test, or break a setup file and the session it saves.</summary>
    private sealed record Question(string Kind, TestFilterCandidate? Test, string? File, string Text);

    /// <summary>A file the model reads, under its key in <c>testFiles</c>, its size, and the questions asked about it.</summary>
    private sealed record Subject(string Key, string Text, int Bytes)
    {
        public List<Question> Questions { get; } = [];
    }

    /// <summary>The files that go to the model in one call, by key, and the questions it asks.</summary>
    private sealed record Batch(Dictionary<string, string> TestFiles, List<Question> Questions);

    /// <summary>What the calls that answered spent: how many, their tokens, and their cost in USD when the provider reports one.</summary>
    private sealed class Spend
    {
        public int Calls { get; set; }

        public long Tokens { get; set; }

        public double? CostUsd { get; set; }

        /// <summary>Set once a call that answered reported no cost, so a sum of the others would undercount.</summary>
        public bool Uncosted { get; set; }
    }

    /// <summary>What the model answered, by test id and by setup file, as the probability of true.</summary>
    private sealed class Answers
    {
        public Dictionary<string, double> Tests { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, double> Setups { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>The filter <see cref="DecisionFilter"/> returns. .NET-only: upstream returns an object literal.</summary>
    private sealed class DecisionTestFilter(DecisionFilterOptions settings) : ITestFilter
    {
        public string Name => "decision";

        public async Task<TestFilterResult> FilterAsync(
            IReadOnlyList<TestFilterCandidate> tests,
            TestFilterContext context
        )
        {
            try
            {
                return await Judge(tests, context, settings).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // An abort ends with its reason, not the error of a git or file read it cut short.
                context.Signal.ThrowIfCancellationRequested();
                throw new TestFilterException(error.Message, error)
                {
                    Relevance = UnaskedRelevance(tests, TouchedBy(context)),
                };
            }
        }
    }
}
