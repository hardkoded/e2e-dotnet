// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests;

/// <summary>Upstream's <c>tests/filter-helpers.ts</c>.</summary>
internal static class FilterHelpers
{
    /// <summary>A secret every context's <c>Redact</c> replaces (upstream's <c>SECRET</c>).</summary>
    public const string TestSecret = "sk-test-4f9c2b7e1d";

    /// <summary>One decide call as the model saw it: the JSON state, each question's instructions by id, the signal, and the provider options (upstream's <c>DecideRequest</c>).</summary>
    public sealed record DecideRequest(
        JsonElement? State,
        IReadOnlyDictionary<string, string> Instructions,
        CancellationToken AbortSignal,
        IReadOnlyDictionary<string, JsonElement>? ProviderOptions
    );

    /// <summary>The model <see cref="ScriptedJudge"/> builds.</summary>
    public sealed class ScriptedJudgeModel(
        Func<string, double> resolve,
        Func<int, DecideRequest, Task>? before,
        int inputTokens,
        Func<int, string?>? providerMetadata
    ) : IDecisionModel
    {
        private readonly object _gate = new();

        public List<DecideRequest> Requests { get; } = [];

        public string Provider => "scripted";

        public string ModelId => "scripted-judge";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["boolean"];

        public async Task<DecisionModelResult> DoDecideAsync(
            DecisionModelCall call,
            CancellationToken cancellationToken
        )
        {
            var json = call.State.OfType<DecisionJsonPart>().FirstOrDefault();
            var instructions = call.Questions.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Instructions ?? "",
                StringComparer.Ordinal
            );
            var request = new DecideRequest(json?.Value, instructions, cancellationToken, call.ProviderOptions);
            int index;
            lock (_gate)
            {
                Requests.Add(request);
                index = Requests.Count - 1;
            }

            if (before is not null)
            {
                await before(index, request).ConfigureAwait(false);
            }

            var metadata = providerMetadata?.Invoke(index);
            return new DecisionModelResult
            {
                Answers = instructions.ToDictionary(
                    pair => pair.Key,
                    pair => new DecisionAnswer { Type = "boolean", Probability = resolve(pair.Value) },
                    StringComparer.Ordinal
                ),
                Usage = new DecisionUsage(inputTokens, 0),
                ResponseModelId = "scripted-judge",
                ProviderMetadata = metadata is null ? null : JsonDocument.Parse(metadata).RootElement,
            };
        }
    }

    /// <summary>
    /// Upstream's <c>scriptedJudge</c>: a scripted decision model that answers every boolean question with the probability
    /// <paramref name="resolve"/> gives its instructions, and records each call. Each call first awaits <paramref name="before"/>, given its
    /// index from 0 and its request; a throw there fails the call. Each answer reports <paramref name="inputTokens"/> and, when given, the
    /// provider metadata JSON the function returns for the call's index.
    /// </summary>
    public static ScriptedJudgeModel ScriptedJudge(
        Func<string, double> resolve,
        Func<int, DecideRequest, Task>? before = null,
        int inputTokens = 10,
        Func<int, string?>? providerMetadata = null
    ) => new(resolve, before, inputTokens, providerMetadata);

    /// <summary>Runs git in <paramref name="cwd"/> with the user's and the system's config shut out (upstream's <c>git</c>).</summary>
    public static string Git(string cwd, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main" }.Concat(args))
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment["GIT_CONFIG_GLOBAL"] = Path.DirectorySeparatorChar == '/' ? "/dev/null" : "NUL";
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_AUTHOR_NAME"] = "e2e";
        info.Environment["GIT_AUTHOR_EMAIL"] = "e2e@example.test";
        info.Environment["GIT_COMMITTER_NAME"] = "e2e";
        info.Environment["GIT_COMMITTER_EMAIL"] = "e2e@example.test";
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? output.Trim()
            : throw new InvalidOperationException("git " + string.Join(' ', args) + " failed: " + error.Result);
    }

    /// <summary>Writes <paramref name="files"/>, by path relative to <paramref name="root"/> (upstream's <c>writeFiles</c>).</summary>
    public static void WriteFiles(string root, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (relative, content) in files)
        {
            var absolute = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content);
        }
    }

    /// <summary>
    /// A repository in the temp directory with <c>base</c> committed and <c>change</c> committed over it, as a branch's commits are, both by
    /// repository-relative path, removed when the test finishes (upstream's <c>changedRepo</c>). The e2e project is at the root, or at
    /// <c>project</c> inside it; <c>config</c> is set in the repository's own git config.
    /// </summary>
    public static ChangedRepo ChangedRepoOf(
        IReadOnlyDictionary<string, string> baseFiles,
        IReadOnlyDictionary<string, string> change,
        string project = ".",
        IReadOnlyDictionary<string, string>? config = null
    )
    {
        var root = Directory.CreateTempSubdirectory("e2e-decision-filter-").FullName;
        Git(root, "init", "--quiet");
        foreach (var (key, value) in config ?? new Dictionary<string, string>())
        {
            Git(root, "config", key, value);
        }

        WriteFiles(root, baseFiles);
        Git(root, "add", "--all");
        Git(root, "commit", "--quiet", "-m", "base");
        var baseCommit = Git(root, "rev-parse", "HEAD");
        WriteFiles(root, change);
        Git(root, "add", "--all");
        Git(root, "commit", "--quiet", "--allow-empty", "-m", "change");
        return new ChangedRepo(
            root,
            Path.Combine(root, project),
            baseCommit,
            [.. change.Keys.OrderBy(file => file, StringComparer.Ordinal)]
        );
    }

    /// <summary>Commits every change in <paramref name="repo"/> on top of what it has committed (upstream's <c>commitAll</c>).</summary>
    public static void CommitAll(ChangedRepo repo)
    {
        Git(repo.Root, "add", "--all");
        Git(repo.Root, "commit", "--quiet", "-m", "change");
    }

    /// <summary>
    /// A filter context for <paramref name="repo"/>, whose run loaded <paramref name="configFile"/> (repository-relative) when given; its
    /// <c>Log</c> lines land in <paramref name="logs"/> (upstream's <c>filterContext</c>). The overrides stand for upstream's object spread.
    /// </summary>
    public static TestFilterContext FilterContext(
        ChangedRepo repo,
        out List<string> logs,
        string? configFile = null,
        CancellationToken? signal = null,
        IReadOnlyList<string>? files = null,
        Func<string, string>? redact = null
    )
    {
        var lines = new List<string>();
        logs = lines;
        return new TestFilterContext
        {
            ProjectRoot = repo.ProjectRoot,
            Changes = new TestFilterChanges(repo.Base, repo.Root, files ?? repo.Files),
            ConfigFile = configFile,
            Signal = signal ?? CancellationToken.None,
            Log = line =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }
            },
            Redact = redact ?? (text => text.Replace(TestSecret, "[redacted]", StringComparison.Ordinal)),
        };
    }

    /// <inheritdoc cref="FilterContext(ChangedRepo, out List{string}, string?, CancellationToken?, IReadOnlyList{string}?, Func{string, string}?)"/>
    public static TestFilterContext FilterContext(
        ChangedRepo repo,
        string? configFile = null,
        CancellationToken? signal = null,
        IReadOnlyList<string>? files = null,
        Func<string, string>? redact = null
    ) => FilterContext(repo, out _, configFile, signal, files, redact);

    /// <summary>The candidate for the test at <paramref name="titlePath"/> in <paramref name="file"/>, with the id the runner gives it (upstream's <c>candidate</c>).</summary>
    public static TestFilterCandidate Candidate(string file, params string[] titlePath) =>
        new($"{file}::{string.Join("::", titlePath)}", file, titlePath, []);

    /// <summary><paramref name="test"/> with its session saved by the setup test in <paramref name="setupFile"/> (upstream's <c>withSetup</c>).</summary>
    public static TestFilterCandidate WithSetup(TestFilterCandidate test, string setupFile) =>
        test with
        {
            SetupFile = setupFile,
        };

    /// <summary>A repository holding a change, and where its e2e project is (upstream's <c>ChangedRepo</c>).</summary>
    public sealed record ChangedRepo(string Root, string ProjectRoot, string Base, IReadOnlyList<string> Files)
        : IDisposable
    {
        public void Dispose()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
