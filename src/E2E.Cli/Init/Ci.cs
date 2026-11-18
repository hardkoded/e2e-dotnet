// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Cli;

/// <summary>Where a service's pipeline goes, relative to the repository root (upstream's <c>CiFile</c>).</summary>
/// <param name="File">The pipeline's path.</param>
/// <param name="Existing">Whether e2e already runs there, so init writes nothing.</param>
/// <param name="Include">For a file the main pipeline must include: the lines to add to it.</param>
/// <param name="Note">Something else the main pipeline needs before the included jobs can run.</param>
/// <param name="JobSuffix">Appended to every job name when the pipeline already has a job of one of those names.</param>
internal sealed record CiFile(
    string File,
    bool Existing,
    string? Include = null,
    string? Note = null,
    string? JobSuffix = null
);

/// <summary>
/// How the iOS job builds the app for the simulator (upstream's <c>IosBuild</c>): Expo generates <c>ios/</c> and builds it, a bare
/// React Native app builds its workspace with <c>xcodebuild</c>, and anything else gets a step that fails until it is replaced.
/// </summary>
internal abstract record IosBuild
{
    private IosBuild() { }

    /// <summary>An Expo app.</summary>
    internal sealed record Expo : IosBuild;

    /// <summary>A bare React Native app.</summary>
    /// <param name="Workspace">The name of the <c>.xcworkspace</c> under <c>ios/</c>, without its extension.</param>
    /// <param name="Bundler">Whether the app has a <c>Gemfile</c>, so the pods install through Bundler.</param>
    internal sealed record ReactNative(string Workspace, bool Bundler) : IosBuild;

    /// <summary>Any other app.</summary>
    internal sealed record Custom : IosBuild;
}

/// <summary>How the Android job builds a release APK (upstream's <c>AndroidBuild</c>).</summary>
internal abstract record AndroidBuild
{
    private AndroidBuild() { }

    /// <summary>An Expo app.</summary>
    /// <param name="Prebuild">Whether Expo still has to generate <c>android/</c>.</param>
    internal sealed record Expo(bool Prebuild) : AndroidBuild;

    /// <summary>A bare React Native app.</summary>
    internal sealed record ReactNative : AndroidBuild;

    /// <summary>Any other app.</summary>
    internal sealed record Custom : AndroidBuild;
}

/// <summary>A phone job: the targets it runs and how it builds the app (the <c>ios</c> and <c>android</c> members of upstream's <c>CiJobs</c>).</summary>
/// <typeparam name="TBuild">The build kind of the platform.</typeparam>
/// <param name="Targets">The names of the targets the job runs.</param>
/// <param name="Build">How the app is built.</param>
internal sealed record CiPhoneJob<TBuild>(IReadOnlyList<string> Targets, TBuild Build);

/// <summary>
/// The jobs a pipeline holds, one per platform (upstream's <c>CiJobs</c>). <c>Web</c> runs every target that is not on a phone: its
/// names when the config has phone targets too, or null for every target of the config (upstream's <c>'all'</c>). A platform
/// with no targets gets no job.
/// </summary>
/// <param name="Web">The web job: its target names, or null for all of them; <c>Web</c> itself is null for no job.</param>
/// <param name="Ios">The iOS job.</param>
/// <param name="Android">The Android job.</param>
internal sealed record CiJobs(
    CiWebJob? Web = null,
    CiPhoneJob<IosBuild>? Ios = null,
    CiPhoneJob<AndroidBuild>? Android = null
)
{
    /// <summary>Whether the pipeline has any job.</summary>
    public bool Any => Web is not null || Ios is not null || Android is not null;
}

/// <summary>The web job of <see cref="CiJobs"/>.</summary>
/// <param name="Targets">The names of the targets it runs; null for every target of the config.</param>
internal sealed record CiWebJob(IReadOnlyList<string>? Targets = null);

/// <summary>A target as the pipeline sees it: its name and the platform its engine drives (upstream's <c>CiTarget</c>).</summary>
/// <param name="Name">The target's name.</param>
/// <param name="Platform">The target's platform.</param>
internal sealed record CiTarget(string Name, string Platform);

/// <summary>What the workflow is rendered from (upstream's <c>WorkflowContext</c>); every path is POSIX and relative to the repository root.</summary>
/// <param name="ProjectFile">The test project's file.</param>
/// <param name="DotnetVersion">The SDK version to set up.</param>
/// <param name="Secrets">Environment variables the run reads from secrets of the same name.</param>
/// <param name="Jobs">The jobs to write.</param>
/// <param name="JobSuffix">Appended to every GitLab job name.</param>
/// <param name="AppDir">The folder of the Expo or React Native app, <c>''</c> for the repository root (upstream's <c>projectDir</c>, which is where its <c>package.json</c> is).</param>
/// <param name="Manager">The app's package manager: <c>npm</c>, <c>pnpm</c>, <c>yarn</c>, or <c>bun</c> (upstream's <c>manager</c>).</param>
/// <param name="YarnBerry">Yarn 2 or later, whose frozen install is <c>--immutable</c> (upstream's <c>yarnBerry</c>).</param>
/// <param name="Junit">Whether the test project writes a JUnit report with the <c>junit</c> logger (upstream's <c>--reporter list,junit</c>).</param>
internal sealed record WorkflowContext(
    string ProjectFile,
    string DotnetVersion,
    IReadOnlyList<string> Secrets,
    CiJobs Jobs,
    string? JobSuffix = null,
    string AppDir = "",
    string Manager = "npm",
    bool YarnBerry = false,
    bool Junit = true
);

/// <summary>One CI service <c>e2e init --ci</c> writes a pipeline for (upstream's <c>CiService</c>).</summary>
/// <param name="Id">The <c>--ci</c> value.</param>
/// <param name="Label">The service's name.</param>
/// <param name="Hint">When the pipeline runs.</param>
/// <param name="Docs">The docs page for the service's mobile jobs and everything init does not write.</param>
/// <param name="Locate">Where the pipeline goes.</param>
/// <param name="Render">The pipeline's text.</param>
/// <param name="SecretHint">How to give the run a model key it reads from the environment.</param>
internal sealed record CiService(
    string Id,
    string Label,
    string Hint,
    string Docs,
    Func<string, CiFile> Locate,
    Func<WorkflowContext, string> Render,
    Func<string, string> SecretHint
);

/// <summary>
/// Upstream's <c>cli/init/ci.ts</c>: the CI pipeline <c>e2e init</c> writes, GitHub Actions or GitLab CI/CD, one job per
/// platform the config's targets run on. The port runs <c>dotnet test</c> where upstream runs <c>e2e run</c>, installs
/// Chromium with the project's <c>playwright.ps1</c> where upstream runs <c>e2e-web install</c>, and has no Node, package
/// manager, or Expo/React Native build to detect: the iOS and Android jobs always get a build step to replace. The pipelines
/// are written as text, not edited from the YAML templates of upstream's <c>templates/ci/</c>, since the port has no YAML library.
/// </summary>
internal static partial class Ci
{
    /// <summary>Where the GitHub Actions workflow goes, relative to the repository root.</summary>
    private const string GithubWorkflow = ".github/workflows/e2e.yml";

    /// <summary>GitLab's pipeline file, and the file init writes beside one that already exists.</summary>
    public const string GitlabPipeline = ".gitlab-ci.yml";

    private const string GitlabIncluded = ".gitlab/e2e.gitlab-ci.yml";

    /// <summary>Where the Android job installs the APK from.</summary>
    private const string Apk = "android/app/build/outputs/apk/release/app-release.apk";

    /// <summary>.NET-only: the package that gives <c>dotnet test</c> the <c>junit</c> logger, which stands for upstream's <c>--reporter junit</c>.</summary>
    public const string JunitLogger = "JunitXml.TestLogger";

    /// <summary>.NET-only: the version of <see cref="JunitLogger"/> init adds to a project.</summary>
    public const string JunitLoggerVersion = "8.1.0";

    /// <summary>Words YAML reads as a boolean or null, which a name must not be left bare as.</summary>
    private static readonly string[] YamlReserved = ["true", "false", "yes", "no", "on", "off", "null", "y", "n"];

    /// <summary>Phone platforms run in their own job, on a runner with a simulator or an emulator.</summary>
    private static readonly string[] Phones = ["ios", "android"];

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The SDK version a pipeline sets up when the project names none.</summary>
    private const string DefaultDotnet = "10.0";

    /// <summary>The jobs the GitLab pipeline defines, which an existing pipeline's own jobs must not share names with.</summary>
    private static readonly string[] GitlabJobs = ["e2e-web", ".e2e-mobile", "e2e-ios", "e2e-android"];

    /// <summary>The keys whose values a service runs as shell: a GitHub step's <c>run</c>, an action's <c>script</c>, and a GitLab job's scripts.</summary>
    private static readonly string[] CommandKeys = ["run", "script", "before_script", "after_script"];

    /// <summary>The services, in the order the flag lists them.</summary>
    public static readonly IReadOnlyList<CiService> Services =
    [
        new(
            "github-actions",
            "GitHub Actions",
            "on every pull request and push to main",
            "/integrations/github",
            LocateGithubWorkflow,
            RenderGithubWorkflow,
            name => $"The workflow reads {name} from the repository secrets; add it with gh secret set {name}"
        ),
        new(
            "gitlab",
            "GitLab CI/CD",
            "in every merge request and on the default branch",
            "/integrations/gitlab",
            LocateGitlabPipeline,
            RenderGitlabPipeline,
            name =>
                $"The pipeline reads {name} from the project's CI/CD variables; add it masked and hidden with glab variable set {name} --masked --hidden --raw"
        ),
    ];

    /// <summary>The service with this id.</summary>
    public static CiService GetCiService(string id) =>
        Services.FirstOrDefault(service => service.Id == id)
        ?? throw new ArgumentException("unknown CI service: " + id, nameof(id));

    /// <summary>
    /// Whether a pipeline file runs <c>dotnet test</c> in one of its commands (upstream's <c>runsE2E</c>, which looks for
    /// <c>e2e run</c>). The words in a YAML or shell comment, or in a step's name, run nothing.
    /// </summary>
    internal static bool RunsE2E(string pipeline)
    {
        foreach (var raw in pipeline.Split('\n'))
        {
            var line = CommentPattern().Replace(raw.TrimEnd('\r'), "").Trim();
            line = line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].TrimStart() : line;
            var key = KeyPattern().Match(line);
            if (key.Success && !CommandKeys.Contains(key.Groups[1].Value))
            {
                continue;
            }

            var command = key.Success ? line[key.Length..] : line;
            if (RunPattern().IsMatch(command))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where the GitHub workflow goes: <c>.github/workflows/e2e.yml</c>, unless that file or another workflow already runs
    /// the tests, in which case e2e is set up.
    /// </summary>
    internal static CiFile LocateGithubWorkflow(string root)
    {
        var workflows = Path.Combine(root, ".github", "workflows");
        var running = Directory.Exists(workflows)
            ? Directory
                .GetFiles(workflows)
                .Select(Path.GetFileName)
                .Where(name =>
                    name!.EndsWith(".yml", StringComparison.Ordinal) || name.EndsWith(".yaml", StringComparison.Ordinal)
                )
                .Order(StringComparer.Ordinal)
                .FirstOrDefault(name => RunsE2E(System.IO.File.ReadAllText(Path.Combine(workflows, name!))))
            : null;
        if (running is not null)
        {
            return new CiFile(".github/workflows/" + running, true);
        }

        return new CiFile(GithubWorkflow, System.IO.File.Exists(Path.Combine(root, GithubWorkflow)));
    }

    /// <summary>
    /// Where the GitLab job goes: <c>.gitlab-ci.yml</c> when the project has none, else a file of its own that
    /// <c>.gitlab-ci.yml</c> includes, so init never edits the project's pipeline. A pipeline file that already runs the tests
    /// counts as set up.
    /// </summary>
    internal static CiFile LocateGitlabPipeline(string root)
    {
        var main = Path.Combine(root, GitlabPipeline);
        if (!System.IO.File.Exists(main))
        {
            return new CiFile(GitlabPipeline, false);
        }

        var pipeline = System.IO.File.ReadAllText(main);
        if (RunsE2E(pipeline))
        {
            return new CiFile(GitlabPipeline, true);
        }

        // The jobs run in GitLab's default `test` stage, which a pipeline that lists its own stages may leave out.
        var stages = GitlabStages(pipeline);

        // GitLab merges jobs of the same name across included files, so the jobs step aside for ones the pipeline already has.
        var taken = GitlabJobs.Any(name =>
            Regex.IsMatch(pipeline, "^" + Regex.Escape(name) + @"\s*:", RegexOptions.Multiline, RegexTimeout)
        );
        return new CiFile(
            GitlabIncluded,
            System.IO.File.Exists(Path.Combine(root, GitlabIncluded)),
            "include:\n  - local: " + GitlabIncluded,
            stages is not null && !stages.Contains("test")
                ? "The e2e jobs run in the test stage; add test to the stages in " + GitlabPipeline
                : null,
            taken ? "-run" : null
        );
    }

    /// <summary>The names a pipeline's top-level <c>stages:</c> lists, however the list is laid out; null without one.</summary>
    internal static IReadOnlyList<string>? GitlabStages(string pipeline)
    {
        foreach (var document in DocumentSeparator().Split(pipeline))
        {
            var lines = document.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            var at = lines.FindIndex(line => TopLevelStages().IsMatch(line));
            if (at < 0)
            {
                continue;
            }

            var rest = NodeProperties().Replace(TopLevelStages().Match(lines[at]).Groups[1].Value.Trim(), "");
            if (rest.StartsWith('*'))
            {
                // An alias to an anchored list.
                var anchor = "&" + rest[1..].Trim();
                var anchored = lines.FindIndex(line => AnchorLine(line, anchor));
                if (anchored < 0)
                {
                    continue;
                }

                (at, rest) = (
                    anchored,
                    NodeProperties()
                        .Replace(CommentPattern().Replace(lines[anchored], "").Split(anchor, 2)[1].Trim(), "")
                );
            }

            return ReadList(lines, at, rest);
        }

        return null;
    }

    /// <summary>The names of a command's job and the settings the CI pipelines need, read from a config.</summary>
    internal static IReadOnlyList<CiTarget> ConfigTargets(JsonObject config)
    {
        if (config["targets"] is not JsonArray array)
        {
            return [new CiTarget("web", "web")];
        }

        var targets = new List<CiTarget>();
        foreach (var node in array)
        {
            if (node is not JsonObject target)
            {
                continue;
            }

            var platform =
                target["platform"] is JsonValue p && p.TryGetValue<string>(out var declared) ? declared : "web";
            var name = target["name"] is JsonValue n && n.TryGetValue<string>(out var named) ? named : platform;
            targets.Add(new CiTarget(name, platform));
        }

        return targets;
    }

    /// <summary>
    /// The jobs for a config's targets (upstream's <c>ciJobs</c>). iOS and Android targets get a job each; the rest, web
    /// and HTTP-only alike, run in the web job, which names them only when the config has phone targets too.
    /// </summary>
    internal static CiJobs GetCiJobs(IReadOnlyList<CiTarget> targets, IosBuild ios, AndroidBuild android)
    {
        List<string> Named(string platform) =>
            [.. targets.Where(target => target.Platform == platform).Select(target => target.Name)];
        List<string> others =
        [
            .. targets.Where(target => !Phones.Contains(target.Platform)).Select(target => target.Name),
        ];
        var phones = targets.Any(target => Phones.Contains(target.Platform));
        var (iosTargets, androidTargets) = (Named("ios"), Named("android"));
        return new CiJobs(
            others.Count == 0 ? null : new CiWebJob(phones ? others : null),
            iosTargets.Count == 0 ? null : new CiPhoneJob<IosBuild>(iosTargets, ios),
            androidTargets.Count == 0 ? null : new CiPhoneJob<AndroidBuild>(androidTargets, android)
        );
    }

    /// <summary>
    /// The app build for each phone platform, from the app's <c>package.json</c> and native folders (upstream's
    /// <c>detectMobileBuilds</c>): Expo when it depends on <c>expo</c>, bare React Native when it depends on
    /// <c>react-native</c> and has the native project, else custom.
    /// </summary>
    internal static (IosBuild Ios, AndroidBuild Android) DetectMobileBuilds(string appDir, Func<string, bool> dependsOn)
    {
        var ios = Path.Combine(appDir, "ios");
        var workspace = Directory.Exists(ios)
            ? Directory
                .GetFileSystemEntries(ios)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault(name =>
                    name!.EndsWith(".xcworkspace", StringComparison.Ordinal) && name != "Pods.xcworkspace"
                )
            : null;
        var gradle = System.IO.File.Exists(Path.Combine(appDir, "android", "gradlew"));
        if (dependsOn("expo"))
        {
            return (new IosBuild.Expo(), new AndroidBuild.Expo(!gradle));
        }

        if (dependsOn("react-native"))
        {
            return (
                workspace is null
                    ? new IosBuild.Custom()
                    : new IosBuild.ReactNative(
                        workspace[..^".xcworkspace".Length],
                        System.IO.File.Exists(Path.Combine(appDir, "Gemfile"))
                    ),
                gradle ? new AndroidBuild.ReactNative() : new AndroidBuild.Custom()
            );
        }

        return (new IosBuild.Custom(), new AndroidBuild.Custom());
    }

    /// <summary>
    /// .NET-only: the folder of the JavaScript app the phone jobs build, which upstream finds as its project's own folder: the
    /// nearest directory at or above <paramref name="projectDir"/>, up to <paramref name="repositoryRoot"/>, with a
    /// <c>package.json</c>. Null when there is none.
    /// </summary>
    internal static string? FindAppDir(string projectDir, string repositoryRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        for (
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectDir));
            ;
            current = Path.GetDirectoryName(current)!
        )
        {
            if (System.IO.File.Exists(Path.Combine(current, "package.json")))
            {
                return current;
            }

            if (
                string.Equals(current, root, StringComparison.OrdinalIgnoreCase)
                || Path.GetDirectoryName(current) is null
            )
            {
                return null;
            }
        }
    }

    /// <summary>.NET-only: whether the <c>package.json</c> in <paramref name="appDir"/> lists <paramref name="name"/> among its dependencies.</summary>
    internal static bool DependsOn(string appDir, string name)
    {
        try
        {
            var document = JsonNode.Parse(
                System.IO.File.ReadAllText(Path.Combine(appDir, "package.json")),
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            );
            return new[] { "dependencies", "devDependencies" }.Any(section =>
                (document?[section] as JsonObject)?.ContainsKey(name) == true
            );
        }
        catch (Exception cause)
            when (cause is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>.NET-only: the package manager of the app in <paramref name="appDir"/>, from its lockfile (upstream reads <c>packageManager</c> and the lockfile); npm when there is none.</summary>
    internal static (string Manager, bool YarnBerry) DetectManager(string appDir, string? repositoryRoot = null)
    {
        // A workspace app keeps its lockfile at the workspace root: the nearest folder up to the repository with one.
        var stop = repositoryRoot is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var found = appDir;
        for (
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDir));
            ;
            current = Path.GetDirectoryName(current)!
        )
        {
            if (
                new[] { "pnpm-lock.yaml", "yarn.lock", "bun.lock", "bun.lockb", "package-lock.json" }.Any(file =>
                    System.IO.File.Exists(Path.Combine(current, file))
                )
            )
            {
                found = current;
                break;
            }

            if (
                stop is null
                || string.Equals(current, stop, StringComparison.OrdinalIgnoreCase)
                || Path.GetDirectoryName(current) is null
            )
            {
                break;
            }
        }

        bool Has(string file) => System.IO.File.Exists(Path.Combine(found, file));
        if (Has("pnpm-lock.yaml"))
        {
            return ("pnpm", false);
        }

        if (Has("yarn.lock"))
        {
            return ("yarn", Has(".yarnrc.yml"));
        }

        return Has("bun.lock") || Has("bun.lockb") ? ("bun", false) : ("npm", false);
    }

    /// <summary>The SDK version a project file targets, such as <c>10.0</c> for <c>net10.0</c>; the default when it names none.</summary>
    internal static string DotnetVersionOf(string? project)
    {
        var match = project is null ? Match.Empty : TargetFramework().Match(project);
        return match.Success ? match.Groups[1].Value : DefaultDotnet;
    }

    /// <summary>The repository the project is in: the nearest directory at or above <paramref name="dir"/> with a <c>.git</c> entry, else <paramref name="fallback"/> (upstream's <c>findRepositoryRoot</c>).</summary>
    public static string FindRepositoryRoot(string dir, string fallback)
    {
        for (var current = Path.GetFullPath(dir); ; current = Path.GetDirectoryName(current)!)
        {
            if (System.IO.File.Exists(Path.Combine(current, ".git")) || Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            if (Path.GetDirectoryName(current) is null)
            {
                return Path.GetFullPath(fallback);
            }
        }
    }

    /// <summary>
    /// The service the repository looks hosted on: GitLab for a <c>.gitlab-ci.yml</c> or a remote on a <c>gitlab.</c> host,
    /// GitHub for a <c>.github/</c> directory or a github.com remote. Null when nothing points either way (upstream's <c>detectCiService</c>).
    /// </summary>
    public static string? DetectCiService(string repositoryRoot)
    {
        var remotes = "";
        var gitConfig = Path.Combine(repositoryRoot, ".git", "config");
        if (System.IO.File.Exists(gitConfig))
        {
            remotes = System.IO.File.ReadAllText(gitConfig);
        }

        if (
            System.IO.File.Exists(Path.Combine(repositoryRoot, GitlabPipeline))
            || Regex.IsMatch(remotes, @"url\s*=.*\bgitlab\.", RegexOptions.None, RegexTimeout)
        )
        {
            return "gitlab";
        }

        if (
            Directory.Exists(Path.Combine(repositoryRoot, ".github"))
            || Regex.IsMatch(remotes, @"url\s*=.*github\.com[:/]", RegexOptions.None, RegexTimeout)
        )
        {
            return "github-actions";
        }

        return null;
    }

    /// <summary><paramref name="target"/> relative to <paramref name="root"/> with forward slashes; empty for the root itself (upstream's <c>posixRelative</c>).</summary>
    public static string PosixRelative(string root, string target) =>
        Path.GetRelativePath(root, target).Replace('\\', '/') is var relative && relative == "."
            ? ""
            : relative.Replace('\\', '/');

    /// <summary>The gateways an existing config uses, by its <c>agents.default.provider</c> (upstream's <c>gatewaysInConfig</c>, which reads the import lines).</summary>
    internal static IReadOnlyList<GatewayPreset> GatewaysInConfig(JsonObject config)
    {
        var provider =
            config["agents"]?["default"]?["provider"] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;
        return [.. Gateways.All.Where(gateway => gateway.Provider == provider)];
    }

    /// <summary>Whether a config points an OpenAI-compatible model at a loopback <c>baseUrl</c>, which a CI runner cannot reach (upstream's <c>configUsesLocalModel</c>).</summary>
    internal static bool ConfigUsesLocalModel(JsonObject config) =>
        config["agents"] is JsonObject agents
        && agents.Any(agent =>
            agent.Value?["baseUrl"] is JsonValue url
            && url.TryGetValue<string>(out var text)
            && Uri.TryCreate(text, UriKind.Absolute, out var parsed)
            && Urls.IsLoopbackHost(parsed.Host)
        );

    /// <summary>Whether a config sets an <c>app.command</c>, so the runner can start the app on a fresh CI machine (upstream's <c>configStartsApp</c>).</summary>
    internal static bool ConfigStartsApp(JsonObject config) =>
        (config["app"] as JsonObject)?.ContainsKey("command") == true
        || (config["targets"] as JsonArray)?.Any(target =>
            (target?["app"] as JsonObject)?.ContainsKey("command") == true
        ) == true;

    /// <summary>Reads an existing config as the runner does, with comments and trailing commas allowed; null when it is not a JSON object.</summary>
    internal static JsonObject? ReadConfig(string text)
    {
        try
        {
            return JsonNode.Parse(
                    text,
                    documentOptions: new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    }
                ) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The GitHub Actions workflow: one job per platform in <c>Jobs</c>, on pull requests and on pushes to main. The
    /// actions are pinned to the commits upstream's template pins.
    /// </summary>
    internal static string RenderGithubWorkflow(WorkflowContext context)
    {
        var jobs = new List<string>();
        if (context.Jobs.Web is { } web)
        {
            jobs.Add(GithubJob("web", "ubuntu-latest", 30, context, GithubWebSteps, web.Targets));
        }

        if (context.Jobs.Ios is { } ios)
        {
            jobs.Add(
                GithubJob(
                    "ios",
                    "macos-26",
                    90,
                    context,
                    GithubIosSteps.Replace(
                        "@APPBUILD@",
                        GithubBuildSteps(context, IosBuildOf(context, ios.Build), ios.Build is IosBuild.Custom),
                        StringComparison.Ordinal
                    ),
                    ios.Targets
                )
            );
        }

        if (context.Jobs.Android is { } android)
        {
            jobs.Add(
                GithubJob(
                    "android",
                    "ubuntu-latest",
                    60,
                    context,
                    GithubAndroidSteps.Replace(
                        "@APPBUILD@",
                        GithubBuildSteps(
                            context,
                            AndroidBuildOf(context, android.Build),
                            android.Build is AndroidBuild.Custom
                        ),
                        StringComparison.Ordinal
                    ),
                    android.Targets
                )
            );
        }

        const string Header = """
            # Written by `e2e init --ci github-actions`: the e2e tests on every pull request
            # and every push to main, one job per platform. See https://github.com/hardkoded/e2e-dotnet
            name: e2e

            on:
              pull_request:
              push:
                branches: [main]

            permissions:
              contents: read

            concurrency:
              group: ${{ github.workflow }}-${{ github.ref }}
              cancel-in-progress: ${{ github.event_name == 'pull_request' }}

            jobs:

            """;
        return Header + string.Join("\n\n", jobs) + "\n";
    }

    private static string GithubJob(
        string name,
        string runner,
        int minutes,
        WorkflowContext context,
        string steps,
        IReadOnlyList<string>? targets
    )
    {
        const string Template = """
              @NAME@:
                runs-on: @RUNNER@
                timeout-minutes: @MINUTES@
                steps:
                  - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: @DOTNET@.x
                      cache: true
                      cache-dependency-path: "**/*.csproj"
            @STEPS@
                  - name: Upload the report
                    if: ${{ !cancelled() }}
                    uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
                    with:
                      name: e2e-@NAME@
                      path: |
                        .e2e/report.json
                        .e2e/junit.xml@J@
                        .e2e/results
                      # .e2e is a hidden directory, which upload-artifact skips when it expands a folder.
                      include-hidden-files: true
                      if-no-files-found: warn
                      retention-days: 7
            """;
        var secrets = string.Join(
            "\n",
            [
                .. context.Secrets.Select(secret => "    " + secret + ": ${{ secrets." + secret + " }}"),
                .. TargetEnv(targets).Select(line => "    " + line),
            ]
        );
        var expanded = steps
            .Replace("@ENV@", secrets.Length == 0 ? "" : "\n  env:\n" + secrets, StringComparison.Ordinal)
            .Replace("@SECRETS@\n", secrets.Length == 0 ? "" : secrets + "\n", StringComparison.Ordinal)
            .Replace("@BUILD@", BuildCommand(context), StringComparison.Ordinal)
            .Replace("@TEST@", GithubTestCommand(context), StringComparison.Ordinal)
            .Replace("@PLAYWRIGHT@", PlaywrightScript(context), StringComparison.Ordinal)
            .Replace("@APK@", ApkPath(context), StringComparison.Ordinal)
            .Replace("@BUILDDIR@", Join(context.AppDir, "build"), StringComparison.Ordinal);
        return Junit(
            Template
                .Replace("@STEPS@", Indent(expanded, 6), StringComparison.Ordinal)
                .Replace("@NAME@", name, StringComparison.Ordinal)
                .Replace("@RUNNER@", runner, StringComparison.Ordinal)
                .Replace(
                    "@MINUTES@",
                    minutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal
                )
                .Replace("@DOTNET@", context.DotnetVersion, StringComparison.Ordinal),
            context.Junit
        );
    }

    private const string GithubRunStep = """
        - name: Run e2e
          run: @TEST@@ENV@
        """;

    private const string GithubWebSteps =
        """
            - name: Build
              run: @BUILD@
            - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
              with:
                path: ~/.cache/ms-playwright
                key: playwright-${{ runner.os }}-${{ runner.arch }}-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props', '**/Directory.Build.props') }}
            - name: Install browsers
              run: pwsh @PLAYWRIGHT@ install --with-deps chromium
            """
        + "\n"
        + GithubRunStep;

    private const string GithubIosSteps =
        """
            @APPBUILD@
            - name: Build the tests
              run: @BUILD@
            # Boots the image's own iPhone 17 Pro on a pinned runtime: a simulator made with `simctl create`
            # can spend minutes on its first boot. Bump the runtime on purpose, after a local run on it,
            # since the accessibility tree changes between iOS releases.
            - name: Boot a simulator and install the app
              env:
                IOS_RUNTIME: com.apple.CoreSimulator.SimRuntime.iOS-26-5
              run: |
                set -euo pipefail
                UDID=$(xcrun simctl list devices available -j | jq -r --arg rt "$IOS_RUNTIME" '.devices[$rt][]? | select(.name == "iPhone 17 Pro") | .udid' | head -n 1)
                if [ -z "$UDID" ]; then
                  echo "::error::No iPhone 17 Pro on $IOS_RUNTIME"
                  xcrun simctl list runtimes
                  exit 1
                fi
                xcrun simctl boot "$UDID"
                xcrun simctl bootstatus "$UDID"
                APP=$(find @BUILDDIR@ -maxdepth 5 -name '*.app' -type d | head -n 1)
                if [ -z "$APP" ]; then
                  echo "::error::No .app under @BUILDDIR@/"
                  exit 1
                fi
                xcrun simctl install "$UDID" "$APP"
                # A target with device set from the E2E_DEVICE variable drives this simulator, whatever else the image has.
                echo "E2E_DEVICE=$UDID" >> "$GITHUB_ENV"
            """
        + "\n"
        + GithubRunStep;

    private const string GithubAndroidSteps = """
        - uses: actions/setup-java@b843ebf9b913146b38da2eaa7aeddf76074ae1ad # v6.1.0
          with:
            distribution: temurin
            java-version: 17
        - name: Enable KVM
          run: |
            echo 'KERNEL=="kvm", GROUP="kvm", MODE="0666", OPTIONS+="static_node=kvm"' | sudo tee /etc/udev/rules.d/99-kvm4all.rules
            sudo udevadm control --reload-rules
            sudo udevadm trigger --name-match=kvm
        @APPBUILD@
        - name: Build the tests
          run: @BUILD@
        - name: Run e2e
          uses: reactivecircus/android-emulator-runner@a421e43855164a8197daf9d8d40fe71c6996bb0d # v2.38.0
          with:
            api-level: 36
            arch: x86_64
            target: google_apis
            profile: pixel_7
            emulator-options: -no-window -gpu swiftshader_indirect -no-snapshot -noaudio -no-boot-anim
            disable-animations: true
            script: |
              adb install -r @APK@
              @TEST@
          env:
        @SECRETS@
            # The emulator the action boots, for a target with device set from the E2E_DEVICE variable.
            E2E_DEVICE: emulator-5554
        """;

    private static string GithubTestCommand(WorkflowContext context) =>
        $"dotnet test -c Release --no-build {Quote(context.ProjectFile)}"
        + (context.Junit ? " --logger \"junit;LogFilePath=${{ github.workspace }}/.e2e/junit.xml\"" : "");

    /// <summary>
    /// The env lines that point a job's run at its one target: <c>E2E_TARGET</c>, the port's counterpart of upstream's
    /// <c>--target</c> (one name). A job with several targets sets none and every test uses the config's own choice.
    /// </summary>
    private static string[] TargetEnv(IReadOnlyList<string>? targets) =>
        targets is { Count: 1 } ? ["E2E_TARGET: " + YamlScalar(targets[0])] : [];

    /// <summary>A target name as a YAML value: bare when it is plain, else double-quoted.</summary>
    private static string YamlScalar(string value) =>
        value.Length > 0
        && char.IsAsciiLetter(value[0])
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')
        && !YamlReserved.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value
            : System.Text.Json.JsonSerializer.Serialize(value);

    /// <summary>The lines of <paramref name="text"/> that end in the <c>@J@</c> marker: kept without it when the JUnit report is on, else dropped.</summary>
    private static string Junit(string text, bool on) =>
        string.Join(
            "\n",
            text.Split('\n')
                .Where(line => on || !line.EndsWith("@J@", StringComparison.Ordinal))
                .Select(line => line.EndsWith("@J@", StringComparison.Ordinal) ? line[..^3] : line)
        );

    /// <summary>`dir/file`, or `file` at the root (upstream's <c>join</c>).</summary>
    private static string Join(string dir, string file) => dir.Length == 0 ? file : dir + "/" + file;

    /// <summary>Where every build writes the APK the Android job installs.</summary>
    private static string ApkPath(WorkflowContext context) => Join(context.AppDir, Apk);

    private static string BuildCommand(WorkflowContext context) =>
        $"dotnet build -c Release {Quote(context.ProjectFile)}";

    /// <summary>The comment lines above an app build and its commands (upstream's <c>{ comment, lines }</c>).</summary>
    private sealed record AppBuild(string[] Comment, string[] Lines);

    /// <summary>The package manager's bin runner in a pipeline file (upstream's <c>exec</c>): the short forms the CI docs use.</summary>
    private static string Exec(WorkflowContext context, string command) =>
        context.Manager switch
        {
            "pnpm" => "pnpm exec " + command,
            "yarn" => "yarn " + command,
            "bun" => "bunx " + command,
            _ => "npx " + command,
        };

    /// <summary>The commands that set up the package manager and install the app's dependencies without touching the lockfile.</summary>
    private static string[] InstallLines(WorkflowContext context) =>
        context.Manager switch
        {
            "pnpm" =>
            [
                "command -v corepack || npm install --global corepack",
                "corepack enable",
                "pnpm install --frozen-lockfile",
            ],
            "yarn" when context.YarnBerry =>
            [
                "command -v corepack || npm install --global corepack",
                "corepack enable",
                "yarn install --immutable",
            ],
            "yarn" => ["command -v yarn || npm install --global yarn", "yarn install --frozen-lockfile"],
            "bun" => ["npm install --global bun", "bun install --frozen-lockfile"],
            _ => ["npm ci"],
        };

    /// <summary>The iOS build's commands and the comment above them (upstream's <c>iosBuild</c>); every build leaves the <c>.app</c> under <c>build/</c>.</summary>
    private static AppBuild IosBuildOf(WorkflowContext context, IosBuild build) =>
        build switch
        {
            IosBuild.Expo => new(
                [
                    "Expo generates ios/ (CNG): expo run:ios prebuilds it, installs the pods, and builds without a simulator.",
                ],
                [Exec(context, "expo run:ios --configuration Release --device generic --no-bundler --output build/ios")]
            ),
            IosBuild.ReactNative native => new(
                ["Installs the pods, then builds the workspace for the simulator into build/."],
                [
                    .. native.Bundler
                        ? new[] { "bundle install", "(cd ios && bundle exec pod install)" }
                        : ["(cd ios && pod install)"],
                    $"xcodebuild -workspace ios/{native.Workspace}.xcworkspace -scheme {native.Workspace} -configuration Release -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath build",
                ]
            ),
            _ => new(
                [
                    $"Replace this step with your app's Release build for the iOS simulator, with the .app under {Join(context.AppDir, "build")}/.",
                ],
                [
                    $"echo \"Replace this step with your app's build for the simulator, with the .app under {Join(context.AppDir, "build")}/\" >&2",
                    "exit 1",
                ]
            ),
        };

    /// <summary>The Android build's commands and the comment above them (upstream's <c>androidBuild</c>); every build writes the APK at <see cref="Apk"/>.</summary>
    private static AppBuild AndroidBuildOf(WorkflowContext context, AndroidBuild build)
    {
        const string Gradle = "(cd android && ./gradlew :app:assembleRelease -PreactNativeArchitectures=x86_64)";
        string[] abi = ["Only the emulator's ABI: building all four takes about 25 minutes longer."];
        return build switch
        {
            AndroidBuild.Expo expo => new(
                ["Expo generates android/ (CNG) and signs release builds with the debug keystore.", .. abi],
                [
                    .. expo.Prebuild ? new[] { Exec(context, "expo prebuild --platform android --no-install") } : [],
                    Gradle,
                ]
            ),
            AndroidBuild.ReactNative => new(abi, [Gradle]),
            _ => new(
                [$"Replace this step with your app's release build, an APK at {ApkPath(context)}."],
                [$"echo \"Replace this step with your app's build, an APK at {ApkPath(context)}\" >&2", "exit 1"]
            ),
        };
    }

    /// <summary>
    /// A phone job's app steps for GitHub: for an Expo or React Native app, Node and the install, then the build in the app's
    /// folder; for any other app, the build step that fails until it is replaced.
    /// </summary>
    private static string GithubBuildSteps(WorkflowContext context, AppBuild build, bool custom)
    {
        var steps = new List<string>();
        var directory = context.AppDir.Length == 0 ? "" : "  working-directory: " + Quote(context.AppDir) + "\n";
        if (!custom)
        {
            steps.Add(
                "- uses: actions/setup-node@949feb2413d6458794dcd2491c4babbbce0c15c1 # v7.1.0\n  with:\n    node-version: 24\n"
                    + "- name: Install JavaScript dependencies\n"
                    + directory
                    + "  run: "
                    + RunValue(InstallLines(context))
            );
        }

        steps.Add(
            string.Concat(build.Comment.Select(line => "# " + line + "\n"))
                + "- name: Build the app\n"
                + (custom ? "" : directory)
                + "  run: "
                + RunValue(build.Lines)
        );
        return string.Join("\n", steps);
    }

    /// <summary>The value of a <c>run:</c> key at the step's own indentation: one command inline, several as a block.</summary>
    private static string RunValue(string[] lines) =>
        lines.Length == 1 ? lines[0] : "|\n" + string.Join("\n", lines.Select(line => "    " + line));

    /// <summary>A path as one shell word: quoted when it holds a space or a character the shell or YAML reads.</summary>
    private static string Quote(string path) =>
        path.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '_' or '-')
            ? path
            : "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>The path of <c>playwright.ps1</c> the project's build writes.</summary>
    private static string PlaywrightScript(WorkflowContext context)
    {
        var directory = context.ProjectFile.Contains('/', StringComparison.Ordinal)
            ? context.ProjectFile[..context.ProjectFile.LastIndexOf('/')] + "/"
            : "";
        return Quote($"{directory}bin/Release/net{context.DotnetVersion}/playwright.ps1");
    }

    /// <summary>
    /// The GitLab CI/CD pipeline: one job per platform in <c>Jobs</c>, in merge request pipelines and on the default branch.
    /// The web job runs in a <c>dotnet/sdk</c> image on GitLab's runners; the mobile jobs need self-managed ones. The rules sit
    /// on the jobs, not in a <c>workflow:</c> block, so the same file works as <c>.gitlab-ci.yml</c> and as a file another pipeline includes.
    /// </summary>
    internal static string RenderGitlabPipeline(WorkflowContext context)
    {
        var parts = new List<string>();
        if (context.Jobs.Web is { } web)
        {
            parts.Add(
                GitlabWeb.Replace(
                    "    @TARGETVAR@\n",
                    string.Concat(TargetEnv(web.Targets).Select(line => "    " + line + "\n")),
                    StringComparison.Ordinal
                )
            );
        }

        if (context.Jobs.Ios is not null || context.Jobs.Android is not null)
        {
            parts.Add(GitlabMobile);
        }

        if (context.Jobs.Ios is { } ios)
        {
            parts.Add(
                GitlabPhone(
                    GitlabIos,
                    ios.Targets,
                    GitlabBuildItems(context, IosBuildOf(context, ios.Build), ios.Build is IosBuild.Custom)
                )
            );
        }

        if (context.Jobs.Android is { } android)
        {
            parts.Add(
                GitlabPhone(
                    GitlabAndroid,
                    android.Targets,
                    GitlabBuildItems(
                        context,
                        AndroidBuildOf(context, android.Build),
                        android.Build is AndroidBuild.Custom
                    )
                )
            );
        }

        const string Header = """
            # Written by `e2e init --ci gitlab`: the e2e tests in every merge request and on
            # the default branch, one job per platform. See https://github.com/hardkoded/e2e-dotnet

            """;
        return Header
            + "\n"
            + Junit(
                string.Join("\n\n", parts)
                    .Replace("@SUFFIX@", context.JobSuffix ?? "", StringComparison.Ordinal)
                    .Replace("@DOTNET@", context.DotnetVersion, StringComparison.Ordinal)
                    .Replace("@BUILD@", BuildCommand(context), StringComparison.Ordinal)
                    .Replace("@PLAYWRIGHT@", PlaywrightScript(context), StringComparison.Ordinal)
                    .Replace(
                        "@TEST@",
                        $"dotnet test -c Release --no-build {Quote(context.ProjectFile)}"
                            + (context.Junit ? " --logger \"junit;LogFilePath=$CI_PROJECT_DIR/.e2e/junit.xml\"" : ""),
                        StringComparison.Ordinal
                    )
                    .Replace("@APK@", ApkPath(context), StringComparison.Ordinal)
                    .Replace("@BUILDDIR@", Join(context.AppDir, "build"), StringComparison.Ordinal),
                context.Junit
            )
            + "\n";
    }

    /// <summary>A phone job: its <c>E2E_TARGET</c> variable when it runs one target, and the app's build items.</summary>
    private static string GitlabPhone(string job, IReadOnlyList<string> targets, string items) =>
        job.Replace(
                "  @TARGETVARS@\n",
                TargetEnv(targets) is [var line] ? "  variables:\n    " + line + "\n" : "",
                StringComparison.Ordinal
            )
            .Replace("    @APPBUILD@\n", items, StringComparison.Ordinal);

    /// <summary>
    /// A phone job's app build as script items on a self-managed shell runner: for an Expo or React Native app the install and the
    /// build in the app's folder, then back to the project; for any other app the step that fails until it is replaced.
    /// </summary>
    private static string GitlabBuildItems(WorkflowContext context, AppBuild build, bool custom)
    {
        var comment = string.Concat(build.Comment.Select(line => "    # " + line + "\n"));
        if (custom)
        {
            return comment + "    - " + string.Join(" && ", build.Lines) + "\n";
        }

        var enter = context.AppDir.Length == 0 ? [] : new[] { $"cd \"$CI_PROJECT_DIR/{context.AppDir}\"" };
        string[] leave = context.AppDir.Length == 0 ? [] : ["cd \"$CI_PROJECT_DIR\""];
        return string.Concat(enter.Concat(InstallLines(context)).Select(line => "    - " + line + "\n"))
            + comment
            + string.Concat(build.Lines.Concat(leave).Select(line => "    - " + line + "\n"));
    }

    private const string GitlabWeb = """
        e2e-web@SUFFIX@:
          image: mcr.microsoft.com/dotnet/sdk:@DOTNET@
          interruptible: true
          timeout: 30 minutes
          rules:
            - if: $CI_PIPELINE_SOURCE == "merge_request_event"
            - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
          variables:
            # GitLab caches only paths inside the project.
            NUGET_PACKAGES: $CI_PROJECT_DIR/.nuget
            PLAYWRIGHT_BROWSERS_PATH: $CI_PROJECT_DIR/.cache/ms-playwright
            @TARGETVAR@
          cache:
            key:
              files:
                - "**/*.csproj"
            paths:
              - .nuget/
              - .cache/ms-playwright/
          script:
            - @BUILD@
            - pwsh @PLAYWRIGHT@ install --with-deps chromium
            - @TEST@
          artifacts:
            when: always
            expire_in: 7 days
            paths:
              - .e2e/report.json
              - .e2e/junit.xml@J@
              - .e2e/results/
            reports:@J@
              junit: .e2e/junit.xml@J@
        """;

    private const string GitlabMobile = """
        # GitLab's hosted runners can't run a simulator or an emulator, so the mobile jobs run on
        # self-managed runners with the shell executor: a Mac with Xcode, and Linux with KVM, a JDK,
        # the Android SDK, and the .NET SDK. Replace the tags with your runners' tags.
        .e2e-mobile@SUFFIX@:
          interruptible: true
          timeout: 90 minutes
          rules:
            - if: $CI_PIPELINE_SOURCE == "merge_request_event"
            - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
          artifacts:
            when: always
            expire_in: 7 days
            paths:
              - .e2e/report.json
              - .e2e/junit.xml@J@
              - .e2e/results/
              # The Android job's emulator output, for a boot that fails.
              - .e2e/emulator.log
            reports:@J@
              junit: .e2e/junit.xml@J@
        """;

    private const string GitlabIos = """
        e2e-ios@SUFFIX@:
          extends: .e2e-mobile@SUFFIX@
          tags: [macos]
          @TARGETVARS@
          script:
            @APPBUILD@
            - @BUILD@
            # A simulator of the job's own on a pinned runtime, so the run never drives a device another job left booted.
            - export E2E_DEVICE=$(xcrun simctl create "e2e-$CI_JOB_ID" "iPhone 17 Pro" com.apple.CoreSimulator.SimRuntime.iOS-26-5)
            - echo "$E2E_DEVICE" > "$CI_PROJECT_DIR/.simulator-udid"
            - xcrun simctl bootstatus "$E2E_DEVICE" -b
            - xcrun simctl install "$E2E_DEVICE" "$(find @BUILDDIR@ -maxdepth 5 -name '*.app' -type d | head -n 1)"
            - @TEST@
          after_script:
            # after_script starts a new shell in the project directory, after a failed script too, but not after a timeout.
            - xcrun simctl delete "$(cat "$CI_PROJECT_DIR/.simulator-udid")" || true
        """;

    private const string GitlabAndroid = """
        e2e-android@SUFFIX@:
          extends: .e2e-mobile@SUFFIX@
          tags: [linux-kvm]
          @TARGETVARS@
          script:
            @APPBUILD@
            - @BUILD@
            # An emulator of the job's own: the port comes from CI_CONCURRENT_ID, so parallel jobs on one runner don't share one.
            # It boots an AVD named e2e that you create on the runner.
            - mkdir -p .e2e
            - PORT=$((5554 + 2 * CI_CONCURRENT_ID))
            - export E2E_DEVICE=emulator-$PORT
            - adb -s $E2E_DEVICE emu kill || true
            - $ANDROID_HOME/emulator/emulator -avd e2e -read-only -port $PORT -no-window -no-audio -no-boot-anim -no-snapshot -gpu swiftshader_indirect > .e2e/emulator.log 2>&1 &
            - timeout 300 adb -s $E2E_DEVICE wait-for-device
            - timeout 300 sh -c 'until [ "$(adb -s $E2E_DEVICE shell getprop sys.boot_completed | tr -d "\r")" = 1 ]; do sleep 2; done'
            - adb -s $E2E_DEVICE install -r @APK@
            - @TEST@
          after_script:
            - adb -s emulator-$((5554 + 2 * CI_CONCURRENT_ID)) emu kill || true
        """;

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join("\n", text.Split('\n').Select(line => line.Length == 0 ? line : pad + line));
    }

    private static bool AnchorLine(string line, string anchor) =>
        Regex.IsMatch(
            CommentPattern().Replace(line, ""),
            Regex.Escape(anchor) + @"(?![\w-])",
            RegexOptions.None,
            RegexTimeout
        );

    /// <summary>The items of the list that starts at <paramref name="at"/>: a flow list over any number of lines, or the block items below the key.</summary>
    private static List<string> ReadList(List<string> lines, int at, string rest)
    {
        var items = new List<string>();
        if (rest.StartsWith('['))
        {
            var text = new StringBuilder(rest);
            for (var i = at + 1; i < lines.Count && !text.ToString().Contains(']', StringComparison.Ordinal); i++)
            {
                text.Append(' ').Append(CommentPattern().Replace(lines[i], ""));
            }

            var flow = text.ToString();
            var end = flow.IndexOf(']', StringComparison.Ordinal);
            flow = flow[1..(end < 0 ? flow.Length : end)];
            items.AddRange(flow.Split(',').Select(Unquote).Where(item => item != ""));
            return items;
        }

        for (var i = at + 1; i < lines.Count; i++)
        {
            var line = CommentPattern().Replace(lines[i], "");
            if (line.Trim() == "")
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (
                !trimmed.StartsWith('-')
                || (line.Length == trimmed.Length && !trimmed.StartsWith("- ", StringComparison.Ordinal))
            )
            {
                break;
            }

            items.Add(Unquote(trimmed[1..]));
        }

        return items;
    }

    private static string Unquote(string item) => item.Trim().Trim('\'', '"').Trim();

    [GeneratedRegex(@"(?:^|\s)#.*$", RegexOptions.None, 1000)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"^([A-Za-z_][\w-]*):(?:\s|$)", RegexOptions.None, 1000)]
    private static partial Regex KeyPattern();

    /// <summary><c>dotnet test</c> as words of their own, which <c>dotnet testing</c> and <c>x-dotnet test</c> are not.</summary>
    [GeneratedRegex(@"(?:^|[\s;&|()\[\],'""`])dotnet\s+test(?=$|[\s;&|()\[\],'""`])", RegexOptions.None, 1000)]
    private static partial Regex RunPattern();

    /// <summary>The anchors and tags that may come before a YAML value: <c>&amp;stages</c>, <c>!!seq</c>.</summary>
    [GeneratedRegex(@"^(?:[&!][^\s]*\s*)+", RegexOptions.None, 1000)]
    private static partial Regex NodeProperties();

    [GeneratedRegex(@"^stages:[ \t]*(.*)$", RegexOptions.None, 1000)]
    private static partial Regex TopLevelStages();

    [GeneratedRegex(@"^---.*$", RegexOptions.Multiline, 1000)]
    private static partial Regex DocumentSeparator();

    [GeneratedRegex(@"<TargetFrameworks?>\s*(?:[^<;]*;)*?net(\d+\.\d+)", RegexOptions.None, 1000)]
    private static partial Regex TargetFramework();
}
