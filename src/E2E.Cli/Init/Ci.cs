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
/// <param name="NpmLockfile">Whether an npm lockfile is committed, which <c>npm ci</c> needs; without one the install is <c>npm install</c>.</param>
/// <param name="Junit">Whether the test project writes a JUnit report with the <c>junit</c> logger (upstream's <c>--reporter list,junit</c>).</param>
/// <param name="WebBuild">The package.json script the web job runs before the tests: the app's production build, which the config serves in CI (upstream's <c>webBuild</c>).</param>
internal sealed record WorkflowContext(
    string ProjectFile,
    string DotnetVersion,
    IReadOnlyList<string> Secrets,
    CiJobs Jobs,
    string? JobSuffix = null,
    string AppDir = "",
    string Manager = "npm",
    bool YarnBerry = false,
    bool Junit = true,
    bool NpmLockfile = true,
    string? WebBuild = null
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
/// are edited from the YAML templates in <c>templates/ci/</c> (the port's own, embedded in the tool), with <see cref="YamlDocument"/> keeping their comments.
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

    /// <summary>
    /// .NET-only: the <c>dependencies</c>, <c>devDependencies</c>, and <c>scripts</c> of the <c>package.json</c> in
    /// <paramref name="appDir"/> (upstream's <c>readPackageManifest</c>); null when it cannot be read.
    /// </summary>
    internal static FrameworkManifest? ReadFrameworkManifest(string appDir)
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
            if (document is not JsonObject root)
            {
                return null;
            }

            return new FrameworkManifest(
                Strings(root["dependencies"]),
                Strings(root["devDependencies"]),
                Strings(root["scripts"])
            );
        }
        catch (Exception cause)
            when (cause is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }

        static Dictionary<string, string>? Strings(JsonNode? node) =>
            node is JsonObject map
                ? map.Where(entry => entry.Value is JsonValue value && value.TryGetValue<string>(out _))
                    .ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<string>())
                : null;
    }

    /// <summary>The command that runs the package.json script <paramref name="name"/> with the given manager (upstream's <c>runScriptCommand</c> in <c>internal/package-manager.ts</c>).</summary>
    internal static string RunScriptCommand(string manager, string name) =>
        manager switch
        {
            "pnpm" => "pnpm " + name,
            "yarn" => "yarn " + name,
            "bun" => "bun run " + name,
            _ => "npm run " + name,
        };

    /// <summary>.NET-only: whether an npm lockfile is committed in <paramref name="appDir"/> or a folder above it, up to <paramref name="repositoryRoot"/>, which <c>npm ci</c> needs.</summary>
    internal static bool HasNpmLockfile(string appDir, string repositoryRoot)
    {
        var stop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        for (
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDir));
            ;
            current = Path.GetDirectoryName(current)!
        )
        {
            if (
                System.IO.File.Exists(Path.Combine(current, "package-lock.json"))
                || System.IO.File.Exists(Path.Combine(current, "npm-shrinkwrap.json"))
            )
            {
                return true;
            }

            if (
                string.Equals(current, stop, StringComparison.OrdinalIgnoreCase)
                || Path.GetDirectoryName(current) is null
            )
            {
                return false;
            }
        }
    }

    /// <summary>Whether an MSBuild file sets <c>ManagePackageVersionsCentrally</c> to <c>true</c>, whatever its attributes (a <c>Condition</c> may hold any character, <c>&gt;</c> included).</summary>
    private static bool SetsCentralVersions(string text)
    {
        try
        {
            return System
                .Xml.Linq.XDocument.Parse(text)
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "ManagePackageVersionsCentrally"
                    && string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                );
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    /// <summary>
    /// .NET-only: whether the project takes its package versions from a <c>Directory.Packages.props</c> (central package
    /// management), where a <c>PackageReference</c> with a <c>Version</c> fails the restore with NU1008. Looks in the project file and in every folder up to
    /// <paramref name="repositoryRoot"/>.
    /// </summary>
    internal static bool UsesCentralPackageVersions(string projectText, string projectDir, string repositoryRoot)
    {
        if (SetsCentralVersions(projectText))
        {
            return true;
        }

        var stop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        for (
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectDir));
            ;
            current = Path.GetDirectoryName(current)!
        )
        {
            foreach (var name in new[] { "Directory.Packages.props", "Directory.Build.props" })
            {
                var file = Path.Combine(current, name);
                if (System.IO.File.Exists(file) && SetsCentralVersions(System.IO.File.ReadAllText(file)))
                {
                    return true;
                }
            }

            if (
                string.Equals(current, stop, StringComparison.OrdinalIgnoreCase)
                || Path.GetDirectoryName(current) is null
            )
            {
                return false;
            }
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
                PackageManagerField(current) is not null
                || new[] { "pnpm-lock.yaml", "yarn.lock", "bun.lock", "bun.lockb", "package-lock.json" }.Any(file =>
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

        // The packageManager field wins, then a lockfile (upstream's detectPackageManager).
        if (PackageManagerField(found) is { } named)
        {
            return (named.Split('@')[0], YarnBerryField().IsMatch(named) || Has(".yarnrc.yml"));
        }

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

    /// <summary>The <c>packageManager</c> field of the package.json in <paramref name="dir"/>, such as <c>pnpm@10.0.0</c>, when it names npm, pnpm, yarn, or bun (upstream's <c>fieldManager</c>).</summary>
    private static string? PackageManagerField(string dir)
    {
        try
        {
            var file = Path.Combine(dir, "package.json");
            if (
                System.IO.File.Exists(file)
                && JsonNode.Parse(System.IO.File.ReadAllText(file)) is JsonObject manifest
                && manifest["packageManager"] is JsonValue field
                && field.TryGetValue<string>(out var text)
                && text.Split('@')[0] is "npm" or "pnpm" or "yarn" or "bun"
            )
            {
                return text;
            }
        }
        catch (Exception cause) when (cause is JsonException or IOException or UnauthorizedAccessException)
        {
            // A package.json that does not read names no manager.
            return null;
        }

        return null;
    }

    [GeneratedRegex(@"^yarn@[2-9]", RegexOptions.None, 1000)]
    private static partial Regex YarnBerryField();

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

    /// <summary>
    /// Whether a config runs the package.json script <paramref name="script"/> as an app command, as in
    /// <c>"args": ["run", "start"]</c> (upstream's <c>configRunsScript</c>, which reads the text of <c>e2e.config.ts</c> outside comments).
    /// </summary>
    internal static bool ConfigRunsScript(JsonObject config, string script)
    {
        static bool Runs(JsonNode? app, string script) =>
            app?["command"]?["args"] is JsonArray { Count: >= 2 } args
            && args[0] is JsonValue first
            && first.TryGetValue<string>(out var run)
            && run == "run"
            && args[1] is JsonValue second
            && second.TryGetValue<string>(out var name)
            && name == script;

        return Runs(config["app"], script)
            || (config["targets"] as JsonArray)?.Any(target => Runs(target?["app"], script)) == true;
    }

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
    /// A pipeline template from <c>templates/ci/</c> (upstream's <c>readTemplate</c>), embedded in the tool. Each is a complete
    /// pipeline for a project at the repository root, so linters and the docs read it as it is; init edits a copy for the project.
    /// </summary>
    internal static YamlDocument ReadTemplate(string name) => YamlDocument.Parse(ReadTemplateText(name));

    /// <summary>The text of a template of <c>templates/ci/</c>.</summary>
    internal static string ReadTemplateText(string name)
    {
        using var stream = typeof(Ci).Assembly.GetManifestResourceStream("templates/ci/" + name)
            ?? throw new InvalidOperationException($"The CI template {name} is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>The map in <paramref name="items"/> whose <paramref name="key"/> holds a value <paramref name="matches"/>, which the template guarantees (upstream's <c>findItem</c>).</summary>
    private static YamlMap FindItem(IEnumerable<YamlNode> items, string key, Func<string, bool> matches) =>
        items.OfType<YamlMap>().FirstOrDefault(item => item.Get(key) is YamlScalar value && matches(value.Value))
        ?? throw new InvalidOperationException($"The CI template has no step with {key} that matches.");

    /// <summary>Sets <paramref name="key"/> in <paramref name="map"/> right before <paramref name="before"/>, or at the end when <paramref name="before"/> is absent (upstream's <c>setBefore</c>).</summary>
    private static void SetBefore(YamlMap map, string key, object value, string before)
    {
        map.Delete(key);
        map.Insert(map.IndexOf(before), key, value);
    }

    /// <summary>The model key the templates read, which the config's own keys replace (upstream's <c>TEMPLATE_SECRET</c>).</summary>
    private const string TemplateSecret = "AI_GATEWAY_API_KEY";

    /// <summary>The variable a job that runs one target names it in.</summary>
    private const string TargetVariable = "E2E_TARGET";

    /// <summary>The command <c>dotnet test</c> the templates run, which the project's own replaces.</summary>
    private const string TemplateTestPrefix = "dotnet test ";

    /// <summary>The path of the report the templates keep.</summary>
    private const string JunitReport = ".e2e/junit.xml";

    /// <summary>
    /// Sets up the parts every GitHub job shares: the SDK version, the build of the tests, and the report the job uploads.
    /// </summary>
    private static void SetUpGithubJob(YamlMap job, WorkflowContext context)
    {
        var steps = job.Get<YamlSeq>("steps").Items;
        var setup = FindItem(steps, "uses", uses => uses.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal));
        setup.Get<YamlMap>("with").Set("dotnet-version", context.DotnetVersion + ".x");
        foreach (
            var build in steps
                .OfType<YamlMap>()
                .Where(step => step.Get("name") is YamlScalar { Value: "Build" or "Build the tests" })
        )
        {
            build.Set("run", BuildCommand(context));
        }

        if (!context.Junit)
        {
            var path = FindItem(steps, "name", name => name == "Upload the report")
                .Get<YamlMap>("with")
                .Get<YamlScalar>("path");
            path.Value = string.Join("\n", path.Value.Split('\n').Where(line => line != JunitReport));
        }
    }

    /// <summary>Points the job's run step at the project's tests, with the model keys and the target the job runs.</summary>
    private static void FinishGithubJob(
        YamlMap job,
        WorkflowContext context,
        YamlMap run,
        IReadOnlyList<string>? targets
    )
    {
        var steps = job.Get<YamlSeq>("steps").Items;
        if (run.Get("with") is YamlMap with)
        {
            // The emulator action runs a script, line by line, instead of a run step.
            var script = with.Get<YamlScalar>("script");
            script.Value = string.Join(
                "\n",
                script
                    .Value.Split('\n')
                    .Select(line =>
                        line.StartsWith(TemplateTestPrefix, StringComparison.Ordinal) ? GithubTestCommand(context)
                        : line.StartsWith("adb install -r ", StringComparison.Ordinal)
                            ? "adb install -r " + Quote(ApkPath(context))
                        : line
                    )
            );
        }
        else
        {
            run.Set("run", GithubTestCommand(context));
        }

        // The template's model key stands for the config's; the rest of the env, such as the emulator's E2E_DEVICE, stays.
        var env = new YamlMap();
        foreach (var secret in context.Secrets)
        {
            env.Set(secret, "${{ secrets." + secret + " }}");
        }

        if (TargetName(targets) is { } target)
        {
            env.Set(TargetVariable, target);
        }

        if (run.Get("env") is YamlMap old)
        {
            env.Items.AddRange(old.Items.Where(pair => pair.Key.Value is not (TemplateSecret or TargetVariable)));
        }

        if (env.Items.Count == 0)
        {
            run.Delete("env");
        }
        else
        {
            run.Set("env", env);
        }
    }

    /// <summary>
    /// Builds the app right before the web job runs the tests (upstream's <c>Build the app</c> step in <c>renderGithubWorkflow</c>).
    /// The port's web job has no Node, so the same Node setup and install the phone jobs use come first, taken from the template.
    /// </summary>
    private static void AddGithubWebBuild(YamlMap job, WorkflowContext context, string webBuild, YamlMap run)
    {
        var steps = job.Get<YamlSeq>("steps");
        var phoneSteps = ((YamlMap)ReadTemplate("github-actions.yml").Contents!)
            .Get<YamlMap>("jobs")
            .Get<YamlMap>("ios")
            .Get<YamlSeq>("steps")
            .Items;
        var setupNode = FindItem(phoneSteps, "uses", uses =>
            uses.StartsWith("actions/setup-node@", StringComparison.Ordinal)
        );
        var install = FindItem(phoneSteps, "name", name => name == "Install JavaScript dependencies");
        var build = (YamlMap)
            YamlNode.Create(
                new KeyValuePair<string, object>[]
                {
                    new("name", "Build the app"),
                    new("run", RunScriptCommand(context.Manager, webBuild)),
                }
            );
        install.Set("run", RunValue(InstallLines(context)));
        if (context.AppDir.Length > 0)
        {
            SetBefore(install, "working-directory", context.AppDir, "run");
            SetBefore(build, "working-directory", context.AppDir, "run");
        }

        build.CommentBefore = $"{E2EConfig.FileName} serves this build in CI.";
        build.SpaceBefore = true;
        run.SpaceBefore = true;
        var at = steps.Items.IndexOf(run);
        steps.Items.InsertRange(at, [setupNode, install, build]);
    }

    /// <summary>Replaces a phone job's app steps with the project's: Node and the install, then the build in the app's folder; a custom app gets the build step that fails until it is replaced.</summary>
    private static void SetGithubBuild(YamlMap job, WorkflowContext context, AppBuild build, bool custom)
    {
        var steps = job.Get<YamlSeq>("steps");
        var buildStep = FindItem(steps.Items, "name", name => name == "Build the app");
        if (custom)
        {
            steps.Items.Remove(
                FindItem(steps.Items, "uses", uses => uses.StartsWith("actions/setup-node@", StringComparison.Ordinal))
            );
            steps.Items.Remove(FindItem(steps.Items, "name", name => name == "Install JavaScript dependencies"));
        }
        else
        {
            var install = FindItem(steps.Items, "name", name => name == "Install JavaScript dependencies");
            install.Set("run", RunValue(InstallLines(context)));
            if (context.AppDir.Length > 0)
            {
                SetBefore(install, "working-directory", context.AppDir, "run");
                SetBefore(buildStep, "working-directory", context.AppDir, "run");
            }
        }

        buildStep.CommentBefore = string.Join("\n", build.Comment);
        buildStep.Set("run", RunValue(build.Lines));
    }

    /// <summary>
    /// The GitHub Actions workflow: one job per platform in <c>Jobs</c>, on pull requests and on pushes to main. The template
    /// (<c>templates/ci/github-actions.yml</c>) holds all three; a platform with no job is deleted, and the actions stay pinned to
    /// the commits upstream's template pins.
    /// </summary>
    internal static string RenderGithubWorkflow(WorkflowContext context)
    {
        var doc = ReadTemplate("github-actions.yml");
        var jobs = ((YamlMap)doc.Contents!).Get<YamlMap>("jobs");
        YamlMap ByName(YamlMap job, string name) =>
            FindItem(job.Get<YamlSeq>("steps").Items, "name", value => value == name);

        foreach (
            var (platform, present) in new[]
            {
                ("web", context.Jobs.Web is not null),
                ("ios", context.Jobs.Ios is not null),
                ("android", context.Jobs.Android is not null),
            }
        )
        {
            if (!present)
            {
                jobs.Delete(platform);
            }
            else
            {
                SetUpGithubJob(jobs.Get<YamlMap>(platform), context);
            }
        }

        // The first job left keeps no blank line from the one it followed in the template.
        if (jobs.Items.Count > 0)
        {
            jobs.Items[0].Key.SpaceBefore = false;
        }

        if (context.Jobs.Web is { } web)
        {
            var job = jobs.Get<YamlMap>("web");
            ByName(job, "Install browsers")
                .Set("run", $"pwsh {PlaywrightScript(context)} install --with-deps chromium");
            var run = ByName(job, "Run e2e");
            if (context.WebBuild is { } webBuild)
            {
                AddGithubWebBuild(job, context, webBuild, run);
            }

            FinishGithubJob(job, context, run, web.Targets);
        }

        if (context.Jobs.Ios is { } ios)
        {
            var job = jobs.Get<YamlMap>("ios");
            SetGithubBuild(job, context, IosBuildOf(context, ios.Build), ios.Build is IosBuild.Custom);
            var boot = ByName(job, "Boot a simulator and install the app").Get<YamlScalar>("run");
            var buildDir = Join(context.AppDir, "build");
            boot.Value = boot
                .Value.Replace("find build ", $"find {Quote(buildDir)} ", StringComparison.Ordinal)
                .Replace("No .app under build/", $"No .app under {buildDir}/", StringComparison.Ordinal);
            FinishGithubJob(job, context, ByName(job, "Run e2e"), ios.Targets);
        }

        if (context.Jobs.Android is { } android)
        {
            var job = jobs.Get<YamlMap>("android");
            SetGithubBuild(job, context, AndroidBuildOf(context, android.Build), android.Build is AndroidBuild.Custom);
            FinishGithubJob(job, context, ByName(job, "Run e2e"), android.Targets);
        }

        return doc.ToString();
    }

    private static string GithubTestCommand(WorkflowContext context) =>
        $"dotnet test -c Release --no-build {Quote(context.ProjectFile)}"
        + (context.Junit ? " --logger \"junit;LogFilePath=${{ github.workspace }}/.e2e/junit.xml\"" : "");

    /// <summary>
    /// The target a job that runs one names in <c>E2E_TARGET</c>, the port's counterpart of upstream's <c>--target</c> (one
    /// name). A job with several sets none and every test uses the config's own choice.
    /// </summary>
    private static string? TargetName(IReadOnlyList<string>? targets) => targets is { Count: 1 } ? targets[0] : null;

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
            _ when context.NpmLockfile => ["npm ci"],
            // `npm ci` needs a committed lockfile.
            _ => ["npm install"],
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

    /// <summary>The value of a <c>run:</c> key: one command as it is, several as a block of lines.</summary>
    private static string RunValue(string[] lines) => lines.Length == 1 ? lines[0] : string.Join("\n", lines) + "\n";

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

    /// <summary>The artifacts of a GitLab job, without the JUnit report when the tests write none.</summary>
    private static void SetUpGitlabArtifacts(YamlMap job, WorkflowContext context)
    {
        if (context.Junit)
        {
            return;
        }

        var artifacts = job.Get<YamlMap>("artifacts");
        var paths = artifacts.Get<YamlSeq>("paths");
        paths.Items.RemoveAll(item => item is YamlScalar { Value: JunitReport });
        artifacts.Delete("reports");
    }

    /// <summary>A GitLab job's variable that names the one target it runs, or none for a job with several.</summary>
    private static void SetGitlabTarget(YamlMap job, IReadOnlyList<string>? targets)
    {
        var variables = job.Get<YamlMap>("variables");
        if (TargetName(targets) is { } target)
        {
            variables.Set(TargetVariable, target);
        }
        else
        {
            variables.Delete(TargetVariable);
            if (variables.Items.Count == 0)
            {
                job.Delete("variables");
            }
        }
    }

    /// <summary>
    /// A phone job's script on a self-managed shell runner: for an Expo or React Native app the install and the build in the
    /// app's folder, then back to the project; for any other app the step that fails until it is replaced. Then the template's
    /// own lines: the tests' build, the device, and the run.
    /// </summary>
    private static void GitlabMobileScript(
        YamlMap job,
        WorkflowContext context,
        AppBuild build,
        bool custom,
        IReadOnlyList<string>? targets
    )
    {
        SetGitlabTarget(job, targets);
        var script = job.Get<YamlSeq>("script");
        var rest = script
            .Items.OfType<YamlScalar>()
            .SkipWhile(item => !item.Value.StartsWith("dotnet build ", StringComparison.Ordinal))
            .ToList();
        var buildDir = Quote(Join(context.AppDir, "build"));
        foreach (var item in rest)
        {
            item.Value = item.Value switch
            {
                var line when line.StartsWith("dotnet build ", StringComparison.Ordinal) => BuildCommand(context),
                var line when line.StartsWith(TemplateTestPrefix, StringComparison.Ordinal) => GitlabTestCommand(
                    context
                ),
                var line => line.Replace("find build ", $"find {buildDir} ", StringComparison.Ordinal)
                    .Replace("install -r " + Apk, "install -r " + Quote(ApkPath(context)), StringComparison.Ordinal),
            };
        }

        var enter = context.AppDir.Length == 0 ? [] : new[] { $"cd \"$CI_PROJECT_DIR/{context.AppDir}\"" };
        string[] leave = context.AppDir.Length == 0 ? [] : ["cd \"$CI_PROJECT_DIR\""];
        string[] buildLines = custom ? [string.Join(" && ", build.Lines)] : [.. build.Lines, .. leave];
        string[] before = custom ? [] : [.. enter, .. InstallLines(context)];
        var items = new List<YamlNode>(before.Concat(buildLines).Select(line => (YamlNode)new YamlScalar(line)));
        items[before.Length].CommentBefore = string.Join("\n", build.Comment);
        script.Items.Clear();
        script.Items.AddRange(items);
        script.Items.AddRange(rest);
    }

    /// <summary>
    /// Builds the app right before the web job runs the tests (upstream's <c>webBuild</c> line in <c>renderGitlabPipeline</c>).
    /// The job's <c>dotnet/sdk</c> image has no Node, so Node and the install come first, in the app's folder.
    /// </summary>
    private static void AddGitlabWebBuild(YamlMap job, WorkflowContext context, string webBuild)
    {
        var script = job.Get<YamlSeq>("script");
        var enter = context.AppDir.Length == 0 ? [] : new[] { $"cd \"$CI_PROJECT_DIR/{context.AppDir}\"" };
        string[] leave = context.AppDir.Length == 0 ? [] : ["cd \"$CI_PROJECT_DIR\""];
        string[] lines =
        [
            "command -v node || (apt-get update -qq && apt-get install -y -qq nodejs npm)",
            .. enter,
            .. InstallLines(context),
            RunScriptCommand(context.Manager, webBuild),
            .. leave,
        ];
        script.Items.InsertRange(script.Items.Count - 1, lines.Select(line => (YamlNode)new YamlScalar(line)));
    }

    private static string GitlabTestCommand(WorkflowContext context) =>
        $"dotnet test -c Release --no-build {Quote(context.ProjectFile)}"
        + (context.Junit ? " --logger \"junit;LogFilePath=$CI_PROJECT_DIR/.e2e/junit.xml\"" : "");

    /// <summary>
    /// The GitLab CI/CD pipeline: one job per platform in <c>Jobs</c>, in merge request pipelines and on the default branch.
    /// The template (<c>templates/ci/gitlab-ci.yml</c>) holds the web job in a <c>dotnet/sdk</c> image on GitLab's runners and
    /// the mobile jobs, which need self-managed ones; a platform with no job is deleted. The rules sit on the jobs, not in a
    /// <c>workflow:</c> block, so the same file works as <c>.gitlab-ci.yml</c> and as a file another pipeline includes.
    /// </summary>
    internal static string RenderGitlabPipeline(WorkflowContext context)
    {
        var doc = ReadTemplate("gitlab-ci.yml");
        var root = (YamlMap)doc.Contents!;
        if (context.Jobs.Web is not { } web)
        {
            root.Delete("e2e-web");
        }
        else
        {
            var job = root.Get<YamlMap>("e2e-web");
            job.Set("image", "mcr.microsoft.com/dotnet/sdk:" + context.DotnetVersion);
            SetGitlabTarget(job, web.Targets);
            var script = job.Get<YamlSeq>("script").Items.OfType<YamlScalar>().ToList();
            script[0].Value = BuildCommand(context);
            script[1].Value = $"pwsh {PlaywrightScript(context)} install --with-deps chromium";
            script[2].Value = GitlabTestCommand(context);
            if (context.WebBuild is { } webBuild)
            {
                AddGitlabWebBuild(job, context, webBuild);
            }

            SetUpGitlabArtifacts(job, context);
        }

        if (context.Jobs.Ios is not { } ios)
        {
            root.Delete("e2e-ios");
        }
        else
        {
            GitlabMobileScript(
                root.Get<YamlMap>("e2e-ios"),
                context,
                IosBuildOf(context, ios.Build),
                ios.Build is IosBuild.Custom,
                ios.Targets
            );
        }

        if (context.Jobs.Android is not { } android)
        {
            root.Delete("e2e-android");
        }
        else
        {
            GitlabMobileScript(
                root.Get<YamlMap>("e2e-android"),
                context,
                AndroidBuildOf(context, android.Build),
                android.Build is AndroidBuild.Custom,
                android.Targets
            );
        }

        if (context.Jobs.Ios is null && context.Jobs.Android is null)
        {
            root.Delete(".e2e-mobile");
        }
        else
        {
            SetUpGitlabArtifacts(root.Get<YamlMap>(".e2e-mobile"), context);
        }

        if (root.Items.Count > 0)
        {
            root.Items[0].Key.SpaceBefore = false;
        }

        if (context.JobSuffix is { } suffix)
        {
            foreach (var pair in root.Items)
            {
                pair.Key.Value += suffix;
                if (pair.Value is YamlMap { } map && map.Get("extends") is YamlScalar extended)
                {
                    extended.Value += suffix;
                }
            }
        }

        return doc.ToString();
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
