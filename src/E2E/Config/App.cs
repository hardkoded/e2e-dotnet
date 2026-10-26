// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>
/// A declared URL with port 0 asks the run for a free port on its loopback host (upstream's <c>PortRequest</c>).
/// <see cref="Port"/> is the one the run assigned, null until it did: a config resolved outside a run keeps the <c>:0</c> URL.
/// </summary>
/// <param name="Host">The hostname to bind, as the URL spelled it.</param>
/// <param name="Port">The port the run assigned, or null.</param>
internal sealed record PortRequest(string Host, int? Port);

/// <summary>
/// The app one target drives, as <see cref="TargetApps.ResolveTargetApp"/> resolved the target's <c>app</c> declaration (upstream's
/// <c>ResolvedApp</c>): the part of it the port has, the base URL on the run's port and the command that starts the app there.
/// </summary>
/// <param name="Base">The normalized base URL on the run's port, or null without a URL.</param>
/// <param name="PortRequest">The free-port request the declared URL made, or null when it names a port or there is no URL.</param>
/// <param name="Command"><c>app.command</c> with <c>{port}</c> expanded to the port the app is served on.</param>
/// <param name="ReadyUrl">Readiness probe for <paramref name="Command"/>; set whenever the command is.</param>
internal sealed record ResolvedApp(NormalizedBaseUrl? Base, PortRequest? PortRequest, CommandConfig? Command, string? ReadyUrl);

/// <summary>Per-target app resolution (upstream's <c>config/app.ts</c>): the declared <c>app</c> checked, then resolved on the port the run assigned when <c>app.url</c> asked for a free one.</summary>
internal static class TargetApps
{
    /// <summary>The token a command or a readiness URL writes where the app's port goes.</summary>
    private const string PortToken = "{port}";

    /// <summary>
    /// Resolves one target's app from its declaration. <paramref name="port"/> is the free port the run assigned to a URL declared with
    /// port 0: it replaces the 0 in the base URL and the default readiness probe. <c>{port}</c> in the command and its readiness URL
    /// expands to the port the app is served on, assigned or fixed; while a requested port is unassigned that is 0, so the config resolves
    /// the same way before and after assignment. A free port nothing starts on, and <c>reuseExisting</c> on a free port, are refused: what
    /// already answers cannot serve a port the run assigns.
    /// </summary>
    public static ResolvedApp ResolveTargetApp(string targetName, AppConfig app, string projectRoot, int? port = null)
    {
        var where = "target \"" + targetName + "\"";
        var declaredBase = app.Url is null ? null : Urls.NormalizeBaseUrl(app.Url);
        var portRequest = declaredBase is null || !Urls.RequestsFreePort(declaredBase) ? null : new PortRequest(new Uri(declaredBase.Href).Host, port);
        var baseUrl = declaredBase is not null && portRequest?.Port is { } assigned ? Urls.WithPort(declaredBase, assigned) : declaredBase;
        int? appPort = baseUrl is null ? null : Urls.PortOf(baseUrl);

        if (app.Command is null && portRequest is not null)
        {
            throw new ConfigurationException("INVALID_CONFIG", where + " app.url asks for a free port (port 0), but nothing starts on it: add app.command to start the app there on " + PortToken);
        }

        var command = app.Command is null ? null : ExpandCommandPort(app.Command, where + " app.command", appPort);
        if (command is not null)
        {
            Command.CheckLog(command, where + " app.command", projectRoot);
        }

        if (command?.ReuseExisting == true && portRequest is not null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where + " app.command.reuseExisting cannot find an app already running on a free port: port 0 is a new port every run; give the app a fixed port, or drop reuseExisting");
        }

        var readyUrl = app.ReadyUrl is null ? baseUrl?.Href : HttpUrl(ExpandPort(app.ReadyUrl, appPort, where + " app.readyUrl"), where + " app.readyUrl");
        if (command is not null && readyUrl is null)
        {
            throw new ConfigurationException("APP_URL_REQUIRED", where + " declares app.command without a URL to poll: declare app.url or app.readyUrl beside it");
        }

        return new ResolvedApp(baseUrl, portRequest, command, command is null ? null : readyUrl);
    }

    /// <summary>A present value that must be an http(s) URL (upstream's <c>httpUrl</c>).</summary>
    public static string HttpUrl(string value, string label) =>
        Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"
            ? value
            : throw new ConfigurationException("INVALID_CONFIG", label + " must be an http(s) URL");

    /// <summary>Substitutes <c>{port}</c> in one configured string. A target without a URL has no port to offer, so the token there is a config error naming the field.</summary>
    private static string ExpandPort(string value, int? port, string label)
    {
        if (!value.Contains(PortToken, StringComparison.Ordinal))
        {
            return value;
        }

        return port is { } number
            ? value.Replace(PortToken, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            : throw new ConfigurationException("INVALID_CONFIG", label + " uses " + PortToken + ", but the target declares no url to take the port from");
    }

    /// <summary>The command with <c>{port}</c> expanded in every <c>args</c> entry and <c>env</c> value.</summary>
    private static CommandConfig ExpandCommandPort(CommandConfig command, string label, int? port) => new()
    {
        Executable = command.Executable,
        Args = command.Args?.Select(arg => ExpandPort(arg, port, label + ".args")).ToList().AsReadOnly(),
        Cwd = command.Cwd,
        Env = command.Env is null
            ? null
            : new ReadOnlyDictionary<string, string>(command.Env.ToDictionary(pair => pair.Key, pair => ExpandPort(pair.Value, port, label + ".env." + pair.Key), StringComparer.Ordinal)),
        InheritEnv = command.InheritEnv,
        StartupTimeout = command.StartupTimeout,
        ShutdownTimeout = command.ShutdownTimeout,
        Log = command.Log,
        ReuseExisting = command.ReuseExisting,
    };
}
