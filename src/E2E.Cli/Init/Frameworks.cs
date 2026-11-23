// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace E2E.Cli;

/// <summary>The parts of <c>package.json</c> a detector reads (upstream's <c>FrameworkManifest</c>).</summary>
/// <param name="Dependencies">The <c>dependencies</c>, by package name.</param>
/// <param name="DevDependencies">The <c>devDependencies</c>, by package name.</param>
/// <param name="Scripts">The <c>scripts</c>, by name.</param>
internal sealed record FrameworkManifest(
    IReadOnlyDictionary<string, string>? Dependencies = null,
    IReadOnlyDictionary<string, string>? DevDependencies = null,
    IReadOnlyDictionary<string, string>? Scripts = null
);

/// <summary>The scripts a pipeline builds and serves a production app with.</summary>
/// <param name="Build">The script the pipeline runs before <c>e2e run</c>.</param>
/// <param name="Start">The script that serves the build.</param>
internal sealed record ProductionScriptNames(string Build, string Start);

/// <summary>What <see cref="Frameworks.DetectFramework"/> finds (upstream's <c>Detection</c>).</summary>
internal abstract record Detection;

/// <summary>An app init can start: the URL it answers on and the package.json scripts that serve it (upstream's <c>FrameworkApp</c>).</summary>
/// <param name="Framework">The framework's name, for the line init prints.</param>
/// <param name="Docs">The docs page for the framework, under the docs site.</param>
/// <param name="Url">The URL the app answers on.</param>
/// <param name="Dev">The script that starts the dev server, which a local run starts or reuses.</param>
/// <param name="Production">
/// The production build CI tests: the script the pipeline runs before <c>e2e run</c>, and the script that serves its output
/// on the same port. Null when the project has no such scripts or they listen elsewhere.
/// </param>
internal sealed record FrameworkApp(
    string Framework,
    string Docs,
    string Url,
    string Dev,
    ProductionScriptNames? Production = null
) : Detection;

/// <summary>A framework init found but cannot start, with the reason, so the user knows to set the target by hand (upstream's <c>UnsupportedFramework</c>).</summary>
/// <param name="Framework">The framework's name.</param>
/// <param name="Docs">The docs page for the framework, under the docs site.</param>
/// <param name="Reason">Why init cannot start it.</param>
internal sealed record UnsupportedFramework(string Framework, string Docs, string Reason) : Detection;

/// <summary>
/// Upstream's <c>cli/init/frameworks.ts</c>: the app frameworks <c>e2e init</c> recognizes in a project's package.json, so a new
/// config's web target starts the app with its own scripts on its own port instead of a placeholder URL. Next.js is the one
/// detector today; a framework such as Vite or Astro adds an entry to <c>Detectors</c> with the same shape.
/// </summary>
internal static partial class Frameworks
{
    private const string DevScript = "dev";
    private const string BuildScript = "build";
    private const string StartScript = "start";

    private static readonly FrameworkDetector[] Detectors =
    [
        new("Next.js", "/frameworks/nextjs", "next", "next", 3000),
    ];

    private static readonly HashSet<string> PortFlags = ["--port", "-p"];

    /// <summary>
    /// The scripts that build and serve the production app of a framework init recognizes, whatever port they use, or null when
    /// the project has no such framework or lacks either script. A pipeline added to an existing config builds when that config
    /// runs the start script.
    /// </summary>
    public static ProductionScriptNames? ProductionScripts(FrameworkManifest manifest)
    {
        if (FindDetector(manifest) is null)
        {
            return null;
        }

        var scripts = manifest.Scripts;
        return scripts is null || !scripts.ContainsKey(BuildScript) || !scripts.ContainsKey(StartScript)
            ? null
            : new ProductionScriptNames(BuildScript, StartScript);
    }

    /// <summary>
    /// The framework the project's package.json declares in <c>dependencies</c> or <c>devDependencies</c>, with how to start it,
    /// or null when it declares none init knows. A project with the framework but no <c>dev</c> script, or a <c>dev</c> script
    /// whose port init cannot read as a fixed number, is <see cref="UnsupportedFramework"/>.
    /// </summary>
    public static Detection? DetectFramework(FrameworkManifest manifest)
    {
        if (FindDetector(manifest) is not { } detector)
        {
            return null;
        }

        var scripts = manifest.Scripts;
        if (scripts is null || !scripts.TryGetValue(DevScript, out var dev))
        {
            return new UnsupportedFramework(detector.Name, detector.Docs, $"package.json has no {DevScript} script");
        }

        if (ScriptPort(dev, detector) is not { } port)
        {
            return new UnsupportedFramework(
                detector.Name,
                detector.Docs,
                $"init cannot read a fixed port from the {DevScript} script"
            );
        }

        var production =
            scripts.ContainsKey(BuildScript)
            && scripts.TryGetValue(StartScript, out var start)
            && ScriptPort(start, detector) == port;
        return new FrameworkApp(
            detector.Name,
            detector.Docs,
            // localhost, never 127.0.0.1: Next.js 16 dev blocks cross-origin dev resources, so a 127.0.0.1 page renders but never hydrates.
            $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}",
            DevScript,
            production ? new ProductionScriptNames(BuildScript, StartScript) : null
        );
    }

    /// <summary>The detector for the framework the project's package.json declares in <c>dependencies</c> or <c>devDependencies</c>.</summary>
    private static FrameworkDetector? FindDetector(FrameworkManifest manifest) =>
        Detectors.FirstOrDefault(detector =>
            manifest.Dependencies?.ContainsKey(detector.Dependency) == true
            || manifest.DevDependencies?.ContainsKey(detector.Dependency) == true
        );

    /// <summary>The words of one shell command, with the quotes around each stripped.</summary>
    private static string[] Words(string command) =>
        [.. WhitespaceRun().Split(command).Select(word => QuoteEdges().Replace(word, "")).Where(word => word != "")];

    /// <summary>
    /// The port one command sets: the value of its <c>--port</c> or <c>-p</c> flag (<c>--port 3100</c>, <c>--port=3100</c>,
    /// <c>-p 3100</c>, <c>-p3100</c>), else a <c>PORT=</c> variable, else null. The value is raw, so <c>$PORT</c> stays as written.
    /// </summary>
    private static string? PortSetting(IReadOnlyList<string> tokens)
    {
        string? variable = null;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var flag = PortFlags.Contains(token)
                ? index + 1 < tokens.Count
                    ? tokens[index + 1]
                    : ""
                : LongPortFlag().Match(token) is { Success: true } longFlag
                    ? longFlag.Groups[1].Value
                    : ShortPortFlag().Match(token) is { Success: true } shortFlag
                        ? shortFlag.Groups[1].Value
                        : null;
            if (flag is not null)
            {
                return flag;
            }

            if (variable is null && PortVariable().Match(token) is { Success: true } assignment)
            {
                variable = assignment.Groups[1].Value;
            }
        }

        return variable;
    }

    /// <summary>
    /// The port a script's server listens on, read from the command that runs the framework's binary (<c>next</c> in
    /// <c>node check.js -p 8080 &amp;&amp; next dev</c>), else the framework's default port. Null when init cannot write the port
    /// into <c>app.url</c>: a value that is not a fixed port, such as <c>$PORT</c> or <c>0</c>, or a port another command in the
    /// script sets, which may or may not reach the server. A script that never runs the binary, such as <c>turbo run dev</c>,
    /// is read as one command.
    /// </summary>
    private static int? ScriptPort(string script, FrameworkDetector detector)
    {
        var commands = CommandSeparator().Split(script).Select(Words).ToList();
        var server = commands.FindIndex(command => command.Contains(detector.Binary));
        if (
            server != -1
            && commands.Where((command, index) => index != server && PortSetting(command) is not null).Any()
        )
        {
            return null;
        }

        var value = PortSetting(server == -1 ? [.. commands.SelectMany(command => command)] : commands[server]);
        if (value is null)
        {
            return detector.DefaultPort;
        }

        if (
            !FixedPort().IsMatch(value)
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
        )
        {
            return null;
        }

        return port is >= 1 and <= 65_535 ? port : null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"^[""']|[""']$", RegexOptions.None, 1000)]
    private static partial Regex QuoteEdges();

    [GeneratedRegex(@"&&|\|\||[;&|]", RegexOptions.None, 1000)]
    private static partial Regex CommandSeparator();

    [GeneratedRegex(@"^--port=(.*)\z", RegexOptions.None, 1000)]
    private static partial Regex LongPortFlag();

    [GeneratedRegex(@"^-p(\d.*)\z", RegexOptions.None, 1000)]
    private static partial Regex ShortPortFlag();

    [GeneratedRegex(@"^PORT=(.*)\z", RegexOptions.None, 1000)]
    private static partial Regex PortVariable();

    [GeneratedRegex(@"^\d+\z", RegexOptions.None, 1000)]
    private static partial Regex FixedPort();

    /// <summary>One framework init recognizes (upstream's <c>FrameworkDetector</c>).</summary>
    /// <param name="Name">The framework's name.</param>
    /// <param name="Docs">The docs page.</param>
    /// <param name="Dependency">The package whose dependency marks the framework.</param>
    /// <param name="Binary">The command a script runs to start the framework's server, whose arguments carry the port.</param>
    /// <param name="DefaultPort">The port the dev server and the production server listen on when their scripts set none.</param>
    private sealed record FrameworkDetector(
        string Name,
        string Docs,
        string Dependency,
        string Binary,
        int DefaultPort
    );
}
