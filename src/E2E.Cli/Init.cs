// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Cli;

/// <summary>How <c>e2e init</c> ended (upstream's <c>InitResult</c>, without the results the port cannot reach: it has no prompts and no examples).</summary>
internal enum InitResult
{
    /// <summary>The project was scaffolded.</summary>
    Scaffolded,

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

    /// <summary>Test seam (.NET-only): the bundled skill, in place of <see cref="Skill.ReadFiles"/>.</summary>
    public Func<IReadOnlyList<SkillFile>>? ReadSkillFiles { get; init; }

    /// <summary>Test seam (.NET-only): the platform whose quoting <c>cd</c> uses, in place of this machine's.</summary>
    public bool? Windows { get; init; }
}

/// <summary>How init ended, with its exit code and what was chosen (upstream's <c>InitRunOutcome</c> without <c>ready</c>, <c>example</c>, and <c>install</c>).</summary>
internal sealed record InitRunOutcome(int ExitCode, InitResult Result, bool Yes, bool ExistingConfig, string? Engine, string? Gateway, bool Skill, bool Mcp);

/// <summary>
/// Upstream's <c>cli/init.ts</c>: non-destructive project scaffolding. The port answers every question from a flag or from
/// <c>--yes</c> (it has no prompt library), writes <c>e2e.config.json</c> where upstream writes <c>e2e.config.ts</c>, and
/// touches no <c>.csproj</c>: the closing line points at <c>dotnet test</c>. Existing config files are never touched. The
/// agent skill and MCP registration are written where the flags say; later runs refresh the locations that exist, and
/// <c>--skills</c> or <c>--mcp</c> replaces that set with exactly the ones it names. Nothing is written through a symlink.
/// </summary>
internal static partial class Init
{
    private const string GatewayDefault = "vercel";

    private static readonly string[] GitignoreEntries =
    [
        ".e2e/results/",
        ".e2e/cache/",
        ".e2e/report.json",
        ".e2e/ai-trace-*.json",
    ];

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Runs <c>e2e init</c> in <paramref name="root"/>, writing its report to <paramref name="output"/>.</summary>
    public static Task<InitRunOutcome> RunAsync(string root, InitOptions options, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        return Task.FromResult(Run(root, options, output));
    }

    private static InitRunOutcome Run(string root, InitOptions options, TextWriter output)
    {
        var log = new InitLog(output);
        log.Intro(options.Directory is null ? "e2e init" : "e2e init " + options.Directory);
        string? engineFact = null;
        string? gatewayFact = null;
        var existingConfig = false;
        var skillFact = false;
        var mcpFact = false;
        InitRunOutcome Done(InitResult result, int exitCode) => new(exitCode, result, options.Yes, existingConfig, engineFact, gatewayFact, skillFact, mcpFact);

        if (!options.Yes)
        {
            log.Error("e2e init asks questions and needs an interactive terminal, and the .NET port has no prompts; pass --yes to accept the defaults (the web engine, the Vercel AI Gateway) or --yes with flags such as --engine for other answers");
            return Done(InitResult.NotInteractive, 2);
        }

        if (File.Exists(root))
        {
            log.Error((options.Directory ?? root) + " is a file, not a directory");
            return Done(InitResult.InvalidProject, 2);
        }

        var cwd = root;
        var bundledSkill = (options.ReadSkillFiles ?? Skill.ReadFiles)();
        if (bundledSkill.Count == 0)
        {
            log.Error(Skill.MissingSkillMessage);
            return Done(InitResult.InvalidProject, 2);
        }

        existingConfig = File.Exists(Path.Combine(cwd, E2EConfig.FileName));
        if (existingConfig)
        {
            log.Warn("Exists, not touching: " + E2EConfig.FileName);
        }

        var gitignorePath = Path.Combine(cwd, ".gitignore");
        var existingIgnore = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        var ignoreLines = Regex.Split(existingIgnore, "\r?\n");
        var missingIgnore = GitignoreEntries.Where(entry => !ignoreLines.Contains(entry)).ToList();

        // A flag answers its question, an earlier run's locations stand when no flag names others, and --yes answers the rest.
        var foundSkillDirs = AgentSkill.FindInstalledSkillDirs(cwd);
        var foundMcpFiles = McpConfig.FindRegisteredMcpFiles(cwd);
        var skillDirs = options.Skills ?? (foundSkillDirs.Count > 0 ? foundSkillDirs : [.. AgentSkill.SkillLocations.Select(location => location.Dir)]);
        var mcpFiles = options.Mcp ?? (foundMcpFiles.Count > 0 ? foundMcpFiles : [.. McpConfig.McpLocations.Select(location => location.File)]);

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
            var selected = options.Engines ?? Engines.DefaultEngineIds;
            // Preset order, whatever order they were typed in, so the config reads the same each time.
            engines = [.. Engines.GetEnginePresets().Select(preset => preset.Id).Where(selected.Contains)];
            var gateway = options.Gateway ?? GatewayDefault;
            if (gateway == "openai-compatible")
            {
                model = new ScaffoldModel(gateway, (options.Endpoint ?? Gateways.DefaultEndpoint).Trim());
            }
            else if (gateway != "none")
            {
                model = new ScaffoldModel(gateway);
            }

            engineFact = engines.Count == 0 ? "none" : string.Join(",", engines);
            gatewayFact = model?.Gateway ?? "none";
        }

        skillFact = skillDirs.Count > 0;
        var (copies, links) = AgentSkill.SplitSkillDirs(skillDirs);
        var plannedCopies = AgentSkill.PlanSkillInstall(cwd, copies, bundledSkill);
        var skillInstalls = new List<SkillInstall>();
        foreach (var planned in plannedCopies)
        {
            if (ConfirmSkillInstall(planned, log))
            {
                skillInstalls.Add(planned);
            }
        }

        foreach (var (dir, linksTo) in links)
        {
            var planned = AgentSkill.PlanSkillLink(cwd, dir, linksTo, bundledSkill, AgentSkill.HoldsCopyAfter(linksTo, plannedCopies, skillInstalls));
            if (planned is not null && ConfirmSkillInstall(planned, log))
            {
                skillInstalls.Add(planned);
            }
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

        var scaffold = Scaffold.CreateScaffold(engines, model);
        (string Relative, string Content)? configFile = existingConfig ? null : (E2EConfig.FileName, scaffold.Config);
        if (configFile is null && skillInstalls.Count == 0 && mcpRegistrations.Count == 0 && missingIgnore.Count == 0)
        {
            log.Outro("Nothing to create; project already initialized");
            return Done(InitResult.AlreadyInitialized, 0);
        }

        // The first write; a directory named on the command line comes into being here.
        System.IO.Directory.CreateDirectory(cwd);
        if (configFile is { } config)
        {
            WriteFile(Path.Combine(cwd, config.Relative), config.Content);
            log.Success("Created " + config.Relative);
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
    /// skipped with a warning naming it. The port has no prompt, so a linked skill directory is never replaced.
    /// </summary>
    private static bool ConfirmSkillInstall(SkillInstall planned, InitLog log)
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

        log.Warn("Symlink, not touching: " + AgentSkill.DescribeLinks(planned));
        return false;
    }

    private static void WriteSkill(string cwd, SkillInstall skill, IReadOnlyList<SkillFile> bundled, InitLog log)
    {
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
                log.Success("Linked " + symlink.Relative + " -> " + symlink.Target + (symlink.Existing ? " (replaced the copy)" : ""));
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

        log.Success((copy.Existing ? "Updated " : "Created ") + copy.Relative + "/ (" + copy.Files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " files)");
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

        public void Warn(string text) => output.WriteLine("warning: " + Program.Clean(text));

        public void Error(string text) => output.WriteLine("error: " + Program.Clean(text));
    }
}
