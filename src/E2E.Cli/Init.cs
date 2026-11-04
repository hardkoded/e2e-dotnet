// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Cli;

/// <summary>How <c>e2e init</c> ended (upstream's <c>InitResult</c>).</summary>
internal enum InitResult
{
    /// <summary>The project was scaffolded.</summary>
    Scaffolded,

    /// <summary>The scaffold was written and the dependencies could not be restored.</summary>
    InstallFailed,

    /// <summary>The example could not be downloaded.</summary>
    ExampleFailed,

    /// <summary>The user left a question or declined the plan; nothing was written.</summary>
    Cancelled,

    /// <summary>The run asked for answers it cannot get without <c>--yes</c>.</summary>
    NotInteractive,

    /// <summary>The directory, a config, or the bundled skill could not be used.</summary>
    InvalidProject,

    /// <summary>Everything init writes was already there.</summary>
    AlreadyInitialized,
}

/// <summary>The answers given as flags (upstream's <c>InitOptions</c>); each one skips its default, and an omitted one takes the <c>--yes</c> default.</summary>
internal sealed record InitOptions
{
    public bool Yes { get; init; }

    /// <summary>
    /// Whether prompts can be answered. The CLI passes its terminal state; a non-interactive run without <c>--yes</c> stops
    /// before asking anything, since a prompt nobody can answer would otherwise wait forever. Unset, it can.
    /// </summary>
    public bool? Interactive { get; init; }

    /// <summary>The directory argument as the user typed it, when the root was chosen from one; the closing <c>Next:</c> line then starts with <c>cd</c> into it.</summary>
    public string? Directory { get; init; }

    /// <summary>Engines for a new config; empty for a config with the default target.</summary>
    public IReadOnlyList<string>? Engines { get; init; }

    /// <summary>A gateway id, or <c>none</c>.</summary>
    public string? Gateway { get; init; }

    /// <summary>The base URL for the <c>openai-compatible</c> gateway.</summary>
    public string? Endpoint { get; init; }

    public IReadOnlyList<string>? Skills { get; init; }

    public IReadOnlyList<string>? Mcp { get; init; }

    /// <summary>An <c>samples/</c> folder name to copy into the directory first; the wizard then runs on the example's project with its config and tests as they are.</summary>
    public string? Example { get; init; }

    /// <summary>Whether to restore the packages after scaffolding; unset, it asks, and <c>--yes</c> takes no.</summary>
    public bool? Install { get; init; }

    /// <summary>.NET-only: the test framework of a new project (<c>nunit</c> or <c>xunit</c>); unset, the framework an existing project uses, else NUnit.</summary>
    public string? Framework { get; init; }

    /// <summary>Test seam (.NET-only): runs the restore in a directory and returns its exit code, in place of mocking <c>spawnSync</c>.</summary>
    public Func<string, CancellationToken, Task<int>>? RunInstall { get; init; }

    /// <summary>Test seam (.NET-only): the handler that answers the example download, in place of stubbing <c>fetch</c>.</summary>
    public HttpMessageHandler? ExampleHandler { get; init; }

    /// <summary>.NET-only: where the questions are asked, in place of <c>@clack/prompts</c>. Unset, they are asked on the console.</summary>
    internal IInitPrompts? Prompts { get; init; }

    /// <summary>Test seam (.NET-only): the bundled skill, in place of <see cref="Skill.ReadFiles"/>.</summary>
    public Func<IReadOnlyList<SkillFile>>? ReadSkillFiles { get; init; }

    /// <summary>Test seam (.NET-only): the platform whose quoting <c>cd</c> uses, in place of this machine's.</summary>
    public bool? Windows { get; init; }
}

/// <summary>How init ended, with its exit code and what was chosen (upstream's <c>InitRunOutcome</c> without <c>ready</c>).</summary>
internal sealed record InitRunOutcome(int ExitCode, InitResult Result, bool Yes, bool ExistingConfig, string? Engine, string? Gateway, bool Skill, bool Mcp, string? Example = null, bool Install = false);

/// <summary>
/// Upstream's <c>cli/init.ts</c>: non-destructive project scaffolding. Every question is answered from a flag, from
/// <c>--yes</c>, or by asking. The port writes <c>e2e.config.json</c> where upstream writes <c>e2e.config.ts</c>, a
/// <c>.csproj</c> and <c>ExampleTests.cs</c> where it writes <c>package.json</c> and <c>tests/example.e2e.ts</c>, and
/// restores packages with <c>dotnet restore</c> where it installs. Every question is asked before the first write, and
/// existing config and test files are never touched. The agent skill and MCP registration are offered once; later runs refresh
/// the locations that exist, and <c>--skills</c> or <c>--mcp</c> replaces that set with exactly the ones it names. Nothing is
/// written through a symlink unless the user agrees to replace it. With <c>Example</c>, the sample is downloaded into an empty
/// directory before the first prompt and removed again when the wizard is cancelled.
/// </summary>
internal static partial class Init
{
    private const string GatewayDefault = "vercel";

    private static readonly string[] GitignoreEntries =
    [
        "bin/",
        "obj/",
        ".e2e/results/",
        ".e2e/cache/",
        ".e2e/report.json",
        ".e2e/ai-trace-*.json",
        ".e2e/videos/",
    ];

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>.NET-only: how long <c>dotnet restore</c> may run before init kills it.</summary>
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The exit code <see cref="RestoreAsync"/> reports for a restore it killed after <see cref="RestoreTimeout"/> (the code of <c>timeout</c>).</summary>
    private const int RestoreTimedOut = 124;

    /// <summary>A file the run will write: a project, the config, or a test (upstream's <c>files</c> entries).</summary>
    private sealed record PlannedFile(string Relative, string Content, bool Existing);

    /// <summary>Runs <c>e2e init</c> in <paramref name="root"/>, writing its report to <paramref name="output"/>.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> stopped the restore.</exception>
    public static async Task<InitRunOutcome> RunAsync(string root, InitOptions options, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        var copied = new StrongBox<ExampleCopy?>();
        try
        {
            return await RunCoreAsync(root, options, output, copied, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (copied.Value is not null && !cancellationToken.IsCancellationRequested)
        {
            // A failure after the copy leaves no half-written example behind.
            copied.Value.Remove();
            throw;
        }
    }

    private static async Task<InitRunOutcome> RunCoreAsync(string root, InitOptions options, TextWriter output, StrongBox<ExampleCopy?> copied, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var log = new InitLog(output);
        var prompts = options.Prompts ?? new ConsolePrompts(Console.In, output);
        log.Intro(options.Directory is null ? "e2e init" : "e2e init " + options.Directory);
        string? engineFact = null;
        string? gatewayFact = null;
        string? exampleFact = null;
        var existingConfig = false;
        var skillFact = false;
        var mcpFact = false;
        var installFact = false;
        ExampleCopy? exampleCopy = null;
        InitRunOutcome Done(InitResult result, int exitCode)
        {
            // An example copied before the prompts goes again when the run ends without writing its own files.
            if (result is not (InitResult.Scaffolded or InitResult.InstallFailed))
            {
                exampleCopy?.Remove();
            }

            return new(exitCode, result, options.Yes, existingConfig, engineFact, gatewayFact, skillFact, mcpFact, exampleFact, installFact);
        }

        InitRunOutcome Cancel() => Done(InitResult.Cancelled, log.Cancelled());

        if (options.Interactive == false && !options.Yes)
        {
            log.Error("e2e init asks questions and needs an interactive terminal; run it from a terminal, or pass --yes to accept the defaults (the web engine, the Vercel AI Gateway) or --yes with flags such as --engine for other answers");
            return Done(InitResult.NotInteractive, 2);
        }

        if (File.Exists(root))
        {
            log.Error((options.Directory ?? root) + " is a file, not a directory");
            return Done(InitResult.InvalidProject, 2);
        }

        var example = options.Example is null ? null : Examples.FindExample(options.Example);
        if (options.Example is not null && example is null)
        {
            log.Error("Unknown example " + options.Example);
            return Done(InitResult.InvalidProject, 2);
        }

        if (example is not null)
        {
            exampleFact = example.Id;
            if (!Examples.IsEmptyDirectory(root))
            {
                log.Error((options.Directory ?? root) + " is not empty; --example needs a new or empty directory, such as e2e init my-app --example " + example.Id);
                return Done(InitResult.InvalidProject, 2);
            }

            log.Info("Downloading the " + example.Id + " example from GitHub");
            var fellBack = (string?)null;
            try
            {
                exampleCopy = await Examples.CopyExampleAsync(example, root, new CopyOptions { OnFallback = tag => fellBack = tag, Signal = cancellationToken, Handler = options.ExampleHandler }).ConfigureAwait(false);
                copied.Value = exampleCopy;
            }
            catch (ExampleCopyCancelled)
            {
                output.WriteLine("Cancelled; no changes were made");
                return Done(InitResult.Cancelled, 130);
            }
            catch (ExampleDownloadError cause)
            {
                log.Error("Could not download the " + example.Id + " example: " + cause.Message + "; check the connection and retry, or copy it from https://github.com/hardkoded/e2e-dotnet/tree/main/samples/" + example.Id);
                return Done(InitResult.ExampleFailed, 2);
            }

            log.Success("Copied the " + example.Id + " example (" + exampleCopy.Ref + ")");
            if (fellBack is not null)
            {
                log.Warn("GitHub has no " + fellBack + " tag; used the samples on main");
            }
        }

        // The e2e project: the directory itself.
        var cwd = root;

        // A new project is made for the framework the flag names, else NUnit; an existing one keeps the framework it uses.
        var requested = options.Framework is null ? null : Frameworks.Get(options.Framework);
        PackageProject pkg;
        try
        {
            pkg = Package.ReadPackage(cwd, requested ?? Frameworks.Get(Frameworks.DefaultFrameworkId));
        }
        catch (InvalidOperationException cause)
        {
            log.Error(cause.Message);
            return Done(InitResult.InvalidProject, 2);
        }

        var detected = Frameworks.Detect(Package.DeclaredDependencies(pkg));
        if (requested is not null && detected is not null && requested.Id != detected.Id)
        {
            log.Error($"{pkg.FileName} already uses {detected.Label}; --framework {requested.Id} would add a second test framework.");
            return Done(InitResult.InvalidProject, 2);
        }

        var framework = requested ?? detected ?? Frameworks.Get(Frameworks.DefaultFrameworkId);
        var bundledSkill = (options.ReadSkillFiles ?? Skill.ReadFiles)();
        if (bundledSkill.Count == 0)
        {
            log.Error(Skill.MissingSkillMessage);
            return Done(InitResult.InvalidProject, 2);
        }

        existingConfig = File.Exists(Path.Combine(cwd, E2EConfig.FileName));
        if (existingConfig && example is null)
        {
            log.Warn("Exists, not touching: " + E2EConfig.FileName);
        }

        var gitignorePath = Path.Combine(cwd, ".gitignore");
        var existingIgnore = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        var ignoreLines = Regex.Split(existingIgnore, "\r?\n");
        var missingIgnore = GitignoreEntries.Where(entry => !ignoreLines.Contains(entry)).ToList();

        // A flag answers its question, an earlier run's locations stand when no flag names others, --yes answers the rest, and a missing one is asked.
        var foundSkillDirs = AgentSkill.FindInstalledSkillDirs(cwd);
        var foundMcpFiles = McpConfig.FindRegisteredMcpFiles(cwd);
        IReadOnlyList<string>? givenSkills = options.Skills ?? (foundSkillDirs.Count > 0 ? foundSkillDirs : options.Yes ? [.. AgentSkill.SkillLocations.Select(location => location.Dir)] : null);
        IReadOnlyList<string>? givenMcp = options.Mcp ?? (foundMcpFiles.Count > 0 ? foundMcpFiles : options.Yes ? [.. McpConfig.McpLocations.Select(location => location.File)] : null);

        // Engine and model gateway are choices for a new config only; an existing config keeps its own.
        IReadOnlyList<string> engines = [];
        ScaffoldModel? model = null;
        var configFlags = new List<string>();
        if (options.Engines is not null)
        {
            configFlags.Add("--engine");
        }

        if (options.Gateway is not null)
        {
            configFlags.Add("--gateway");
        }

        if (options.Endpoint is not null)
        {
            configFlags.Add("--endpoint");
        }

        if (existingConfig && configFlags.Count > 0)
        {
            log.Warn("Ignoring " + string.Join(", ", configFlags) + ": " + E2EConfig.FileName + " exists, and these apply to a new config only");
        }

        if (!existingConfig)
        {
            var presets = Engines.GetEnginePresets();
            var selected = options.Engines ?? (options.Yes ? Engines.DefaultEngineIds : prompts.MultiSelect(
                "Which engines? Select none for the default target.",
                [.. presets.Select(preset => new PromptOption(preset.Id, preset.Label, preset.Hint))],
                Engines.DefaultEngineIds));
            if (selected is null)
            {
                return Cancel();
            }

            if (selected.Count > 1)
            {
                // The flag refuses the same (Flags), and nothing is written yet.
                log.Error("the .NET port runs one target; choose one engine");
                return Done(InitResult.InvalidProject, 2);
            }

            // Preset order, whatever order they were toggled or typed in, so the config reads the same each time.
            engines = [.. presets.Select(preset => preset.Id).Where(selected.Contains)];

            // The gateway is a visible choice, not a runner default: the config names its provider.
            var gateway = options.Gateway ?? (options.Yes ? GatewayDefault : prompts.Select(
                "Which model gateway for agent steps?",
                [.. Gateways.All.Select(preset => new PromptOption(preset.Id, preset.Label, preset.Hint)), new PromptOption("none", "None", "deterministic tests only; add a gateway later")],
                GatewayDefault));
            if (gateway is null)
            {
                return Cancel();
            }

            if (gateway == "openai-compatible")
            {
                var endpoint = options.Endpoint ?? (options.Yes ? Gateways.DefaultEndpoint : prompts.Text(
                    "Base URL of the OpenAI-compatible API",
                    Gateways.DefaultEndpoint,
                    Gateways.ValidateEndpoint));
                if (endpoint is null)
                {
                    return Cancel();
                }

                model = new ScaffoldModel(gateway, endpoint.Trim());
            }
            else if (gateway != "none")
            {
                model = new ScaffoldModel(gateway);
            }

            engineFact = engines.Count == 0 ? "none" : string.Join(",", engines);
            gatewayFact = model?.Gateway ?? "none";
        }

        var skillDirs = givenSkills ?? AskLocations(
            prompts,
            [.. AgentSkill.SkillLocations.Select(location => new PromptOption(location.Dir, location.Dir, location.Hint))],
            "Install the e2e skill for coding agents?");
        if (skillDirs is null)
        {
            return Cancel();
        }

        skillFact = skillDirs.Count > 0;
        var (copies, links) = AgentSkill.SplitSkillDirs(skillDirs);
        var plannedCopies = AgentSkill.PlanSkillInstall(cwd, copies, bundledSkill);
        var skillInstalls = new List<SkillInstall>();
        foreach (var planned in plannedCopies)
        {
            var proceed = ConfirmSkillInstall(planned, options.Yes, prompts, log);
            if (proceed is null)
            {
                return Cancel();
            }

            if (proceed == true)
            {
                skillInstalls.Add(planned);
            }
        }

        foreach (var (dir, linksTo) in links)
        {
            var planned = AgentSkill.PlanSkillLink(cwd, dir, linksTo, bundledSkill, AgentSkill.HoldsCopyAfter(linksTo, plannedCopies, skillInstalls));
            if (planned is null)
            {
                continue;
            }

            var proceed = ConfirmSkillInstall(planned, options.Yes, prompts, log);
            if (proceed is null)
            {
                return Cancel();
            }

            if (proceed == true)
            {
                skillInstalls.Add(planned);
            }
        }

        var mcpFiles = givenMcp ?? AskLocations(
            prompts,
            [.. McpConfig.McpLocations.Select(location => new PromptOption(location.File, location.File, location.Hint))],
            "Register the e2e MCP server for coding agents?");
        if (mcpFiles is null)
        {
            return Cancel();
        }

        mcpFact = mcpFiles.Count > 0;
        IReadOnlyList<McpRegistration> mcpRegistrations;
        try
        {
            var plan = McpConfig.PlanMcpRegistration(cwd, mcpFiles);
            mcpRegistrations = plan.Registrations;
            foreach (var link in plan.Links)
            {
                log.Warn("Symlink, not touching: " + link.Relative + " -> " + link.Target + " (e2e mcp server)");
            }
        }
        catch (InvalidOperationException cause)
        {
            log.Error(cause.Message);
            return Done(InitResult.InvalidProject, 2);
        }

        var projectName = pkg.FileName is null ? ProjectNameOf(cwd) : Path.GetFileNameWithoutExtension(pkg.FileName);
        var scaffold = Scaffold.CreateScaffold(engines, model, framework, RootNamespaceOf(projectName));

        // A config with an example test of its own keeps it; a re-run adds no second one.
        var tests = existingConfig && scaffold.Tests.Any(test => File.Exists(Path.Combine(cwd, test.Name + ".cs")))
            ? []
            : scaffold.Tests.Select(test => new PlannedFile(test.Name + ".cs", test.Content, false)).ToList();
        var newTests = tests.Where(test => !File.Exists(Path.Combine(cwd, test.Relative))).ToList();
        if (example is null)
        {
            foreach (var test in tests.Except(newTests))
            {
                log.Warn("Exists, not touching: " + test.Relative);
            }
        }

        var additions = Package.AddDependencies(pkg, scaffold.Dependencies);
        if (example is null && additions.Count > 0)
        {
            log.Info("Add package references: " + string.Join(", ", additions.Select(addition => addition.Key + "@" + addition.Value)));
        }

        // An example's project, config, and tests stay as the example has them.
        var files = new List<PlannedFile>();
        if (example is null)
        {
            if (pkg.Original is null || additions.Count > 0)
            {
                files.Add(new PlannedFile(pkg.FileName ?? projectName + ".csproj", Package.SerializePackage(pkg), pkg.Original is not null));
            }

            if (!existingConfig)
            {
                files.Add(new PlannedFile(E2EConfig.FileName, scaffold.Config, false));
            }

            files.AddRange(newTests);
        }

        if (example is null && files.Count == 0 && skillInstalls.Count == 0 && mcpRegistrations.Count == 0 && missingIgnore.Count == 0)
        {
            log.Outro("Nothing to create; project already initialized");
            return Done(InitResult.AlreadyInitialized, 0);
        }

        if (!options.Yes)
        {
            var actions = new List<string>();
            if (example is not null)
            {
                actions.Add("Keep the " + example.Id + " example in " + (options.Directory ?? "this directory") + " (cancelling removes it)");
            }

            actions.AddRange(files.Select(file => (file.Existing ? "Update " : "Create ") + file.Relative));

            foreach (var install in skillInstalls)
            {
                actions.Add(install is SkillSymlink symlink
                    ? "Link " + symlink.Relative + " -> " + symlink.Target + LinkReplaces(symlink, "replacing")
                    : (install.Links.Count > 0 ? "Replace " : install.Existing ? "Update " : "Create ") + install.Relative + "/");
            }

            actions.AddRange(mcpRegistrations.Select(registration => (registration.Existing ? "Update " : "Create ") + registration.Relative));
            if (missingIgnore.Count > 0)
            {
                actions.Add("Update .gitignore");
            }

            // One change per line: a dozen paths in one sentence is unreadable.
            log.Info(string.Join("\n", ["Changes:", .. actions.Select(action => "  - " + action)]));
            if (prompts.Confirm("Apply these changes?") is not true)
            {
                return Cancel();
            }
        }

        // The restore is asked once the plan is accepted and runs after everything is written.
        var restore = options.Install ?? (options.Yes ? false : prompts.Confirm("Install dependencies with dotnet restore?", initialValue: true));
        if (restore is null)
        {
            return Cancel();
        }

        installFact = restore.Value;

        // The first write; a directory named on the command line comes into being here.
        System.IO.Directory.CreateDirectory(cwd);
        foreach (var file in files)
        {
            WriteFile(Path.Combine(cwd, file.Relative), file.Content);
            log.Success((file.Existing ? "Updated " : "Created ") + file.Relative);
        }

        foreach (var skill in skillInstalls)
        {
            WriteSkill(cwd, skill, bundledSkill, log);
        }

        if (skillDirs.Count == 0)
        {
            log.Info("Skipped the agent skill; agents can still print it with e2e guide");
        }

        foreach (var registration in mcpRegistrations)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(registration.Absolute)!);
            WriteFile(registration.Absolute, registration.Content);
            log.Success((registration.Existing ? "Updated " : "Created ") + registration.Relative + " (e2e mcp server)");
        }

        if (mcpFiles.Count == 0)
        {
            log.Info("Skipped the MCP server; register it later with: " + McpConfig.RegisterCommand);
        }

        if (missingIgnore.Count > 0)
        {
            var prefix = existingIgnore.Length == 0 || existingIgnore.EndsWith('\n') ? "" : "\n";
            WriteFile(gitignorePath, existingIgnore + prefix + string.Join("\n", missingIgnore) + "\n");
            log.Success("Updated .gitignore (" + missingIgnore.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " entries)");
        }

        if (restore.Value)
        {
            int exitCode;
            string reason;
            try
            {
                exitCode = await (options.RunInstall ?? RestoreAsync)(cwd, cancellationToken).ConfigureAwait(false);
                reason = exitCode == RestoreTimedOut ? "timed out after " + RestoreTimeout.TotalMinutes + " minutes" : "exit " + exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.ComponentModel.Win32Exception cause)
            {
                (exitCode, reason) = (-1, cause.Message);
            }
            catch (OperationCanceledException)
            {
                output.WriteLine("Cancelled; the scaffold is saved, run dotnet restore to finish");
                return Done(InitResult.Cancelled, 130);
            }

            if (exitCode != 0)
            {
                log.Error("Installation failed (" + reason + "); retry with dotnet restore");
                log.Outro("Scaffold saved");
                return Done(InitResult.InstallFailed, 2);
            }
        }

        // A subscription gateway has no key to set; the sign-in is the step before the first run.
        var subscription = model is null ? null : Gateways.GetGatewayPreset(model.Gateway).Login;
        var before = new List<string>();
        if (options.Directory is not null)
        {
            before.Add("cd " + ShellArgument(options.Directory, options.Windows ?? OperatingSystem.IsWindows()));
        }

        if (subscription is not null)
        {
            before.Add("e2e login " + subscription);
        }

        log.Outro("Next: " + string.Join(", then ", [.. before, "dotnet test"]));
        return Done(InitResult.Scaffolded, 0);
    }

    /// <summary>
    /// .NET-only: the name a new project takes from its directory, as <c>dotnet new</c> names it, with the characters a file
    /// name cannot hold made <c>-</c> (upstream's <c>package.json</c> has no name to choose).
    /// </summary>
    internal static string ProjectNameOf(string directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character)).Trim('.', ' ');
        return clean.Length == 0 ? "E2ETests" : clean;
    }

    /// <summary>.NET-only: the namespace <c>dotnet new</c> gives a project of this name: every part an identifier.</summary>
    internal static string RootNamespaceOf(string projectName) =>
        string.Join('.', projectName.Split('.').Select(part =>
        {
            var identifier = string.Concat(part.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_'));
            return identifier.Length == 0 || char.IsDigit(identifier[0]) ? "_" + identifier : identifier;
        }));

    /// <summary>
    /// Runs <c>dotnet restore</c> in <paramref name="cwd"/> with the terminal of this process (upstream's <c>spawnSync(manager,
    /// ['install'], { stdio: 'inherit' })</c>), bounded by <see cref="RestoreTimeout"/>. A cancel or a timeout kills the process tree.
    /// </summary>
    private static async Task<int> RestoreAsync(string cwd, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = cwd, UseShellExecute = false };
        start.ArgumentList.Add("restore");
        using var process = Process.Start(start) ?? throw new Win32Exception("could not start dotnet");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RestoreTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It exited between the cancel and the kill.
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return RestoreTimedOut;
        }
    }

    /// <summary>
    /// A path as the user will paste it into their shell: bare while every character is safe, otherwise quoted for the
    /// platform (single quotes for POSIX shells, double quotes for cmd and PowerShell), so <c>apps/my web</c> stays one argument.
    /// </summary>
    internal static string ShellArgument(string value, bool windows)
    {
        if (SafeShellText().IsMatch(value))
        {
            return value;
        }

        return windows ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    /// <summary>
    /// Whether a planned skill write goes ahead. An entry a write cannot pass, or a symlink init would write through, is
    /// skipped with a warning naming it; a linked skill directory is replaced when the user agrees, never under
    /// <c>--yes</c>. Null when the user leaves the question (upstream's cancel symbol).
    /// </summary>
    private static bool? ConfirmSkillInstall(SkillInstall planned, bool yes, IInitPrompts prompts, InitLog log)
    {
        if (planned.Obstacles.Count > 0)
        {
            log.Warn("Broken, not touching: " + AgentSkill.DescribeObstacles(planned));
            return false;
        }

        if (planned.Links.Count == 0)
        {
            return true;
        }

        if (yes || !AgentSkill.ReplaceableLinks(planned))
        {
            log.Warn("Symlink, not touching: " + AgentSkill.DescribeLinks(planned));
            return false;
        }

        var replacement = planned is SkillSymlink symlink ? "a link to " + symlink.Target : "a copy of the skill";
        return prompts.Confirm("Replace the symlink " + AgentSkill.DescribeLinks(planned) + " with " + replacement + "?", initialValue: false);
    }

    /// <summary>What a link takes the place of, for the summary (<c>replacing</c>) and the success line (<c>replaced</c>); empty when nothing is there.</summary>
    private static string LinkReplaces(SkillInstall install, string verb)
    {
        var what = install.Links.Count > 0 ? "the symlink" : install.Existing ? "the copy" : null;
        return what is null ? "" : " (" + verb + " " + what + ")";
    }

    /// <summary>Asks where an agent-facing artifact (the skill, the MCP registration) goes, every location preselected.</summary>
    private static IReadOnlyList<string>? AskLocations(IInitPrompts prompts, IReadOnlyList<PromptOption> locations, string message) =>
        prompts.MultiSelect(message, locations, [.. locations.Select(location => location.Value)]);

    /// <summary>Removes a symlink without touching what it leads to.</summary>
    private static void RemoveLink(string absolute)
    {
        // FileInfo.Attributes reads the link itself and never throws, even for a link whose target is gone.
        if (OperatingSystem.IsWindows() && ((int)new FileInfo(absolute).Attributes & (int)FileAttributes.Directory) != 0)
        {
            System.IO.Directory.Delete(absolute);
            return;
        }

        File.Delete(absolute);
    }

    private static void WriteSkill(string cwd, SkillInstall skill, IReadOnlyList<SkillFile> bundled, InitLog log)
    {
        foreach (var link in skill.Links)
        {
            RemoveLink(Path.GetFullPath(Path.Combine(cwd, link.Relative)));
        }

        if (skill is SkillSymlink symlink)
        {
            var absolute = Path.GetFullPath(Path.Combine(cwd, symlink.Relative));
            if (symlink.Existing)
            {
                System.IO.Directory.Delete(absolute, recursive: true);
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            try
            {
                System.IO.Directory.CreateSymbolicLink(absolute, symlink.Target.Replace('/', Path.DirectorySeparatorChar));
                log.Success("Linked " + symlink.Relative + " -> " + symlink.Target + LinkReplaces(symlink, "replaced"));
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or PlatformNotSupportedException)
            {
                // Windows lets only a privileged account create a symlink; a copy serves the agent the same.
                log.Warn("Could not link " + symlink.Relative + " (" + error.Message + "); writing a copy instead");
                foreach (var file in bundled)
                {
                    var target = Path.Combine(absolute, file.Relative);
                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    WriteFile(target, file.Content);
                }

                log.Success("Created " + symlink.Relative + "/ (" + bundled.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " files)");
                return;
            }
        }

        var copy = (SkillCopy)skill;
        foreach (var file in copy.Files)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file.Absolute)!);
            WriteFile(file.Absolute, file.Content);
        }

        log.Success((copy.Links.Count > 0 ? "Replaced " : copy.Existing ? "Updated " : "Created ") + copy.Relative + "/ (" + copy.Files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " files)");
    }

    /// <summary>Writes through a unique temp name, then moves it over the target, so a crash never leaves a half-written file (.NET-only; upstream writes in place).</summary>
    private static void WriteFile(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content, Utf8);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_./@%+=:,-]+\z")]
    private static partial Regex SafeShellText();

    /// <summary>.NET-only: the plain lines the port prints where upstream uses clack's log (<c>clack.log.info</c>, <c>warn</c>, <c>error</c>, <c>success</c>, <c>intro</c>, <c>outro</c>).</summary>
    private sealed class InitLog(TextWriter output)
    {
        public void Intro(string text) => output.WriteLine(Program.Clean(text));

        public void Outro(string text) => output.WriteLine(Program.Clean(text));

        public void Info(string text) => output.WriteLine(Program.Clean(text));

        public void Success(string text) => output.WriteLine(Program.Clean(text));

        public int Cancelled()
        {
            output.WriteLine("Cancelled; no changes were made");
            return 0;
        }

        public void Warn(string text) => output.WriteLine("warning: " + Program.Clean(text));

        public void Error(string text) => output.WriteLine("error: " + Program.Clean(text));
    }
}
