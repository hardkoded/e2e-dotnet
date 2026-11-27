// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;
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
/// <c>ResolvedApp</c>). Navigation policy, cache and session identity, the report's target record, and the app process all read from here.
/// </summary>
/// <param name="Base">The normalized base URL on the run's port, or null without a URL.</param>
/// <param name="PortRequest">The free-port request the declared URL made, or null when it names a port or there is no URL.</param>
/// <param name="Site">The site of the base URL, as <see cref="Urls.SiteOf"/> reads it. Null without a URL, which is no policy at all.</param>
/// <param name="Environment"><c>test</c>, <c>staging</c>, or <c>production</c>.</param>
/// <param name="Identity">
/// Stable identity keying cache and session entries: the declared identity, else the base URL's origin and path as declared (a free
/// port still 0), else the bundle id, else the build path. Null when the target declares none of them, so entries key on the target
/// and environment alone.
/// </param>
/// <param name="BundleId"><c>app.bundleId</c>.</param>
/// <param name="AppPath"><c>app.appPath</c>.</param>
/// <param name="LinkBase">The link base <c>App.OpenAsync(path)</c> joins on a device target; see <see cref="AppConfig.LinkBase"/>.</param>
/// <param name="LaunchArguments"><c>app.launchArguments</c>, checked: a list of strings or an object of argument name to value.</param>
/// <param name="Permissions"><c>app.permissions</c> as a map of permission name to state.</param>
/// <param name="AllPermissions"><c>app.permissions: 'all'</c>.</param>
/// <param name="Command"><c>app.command</c> with <c>{port}</c> expanded to the port the app is served on.</param>
/// <param name="ReadyUrl">Readiness probe for <paramref name="Command"/>; set whenever the command is.</param>
internal sealed record ResolvedApp(
    NormalizedBaseUrl? Base,
    PortRequest? PortRequest,
    string? Site,
    string Environment,
    string? Identity,
    string? BundleId,
    string? AppPath,
    string? LinkBase,
    ResolvedLaunchArguments? LaunchArguments,
    IReadOnlyDictionary<string, AppPermissionState>? Permissions,
    bool AllPermissions,
    CommandConfig? Command,
    string? ReadyUrl
);

/// <summary>Per-target app resolution (upstream's <c>config/app.ts</c>): the declared <c>app</c> checked, then resolved on the port the run assigned when <c>app.url</c> asked for a free one.</summary>
internal static partial class TargetApps
{
    /// <summary>The token a command or a readiness URL writes where the app's port goes.</summary>
    private const string PortToken = "{port}";

    /// <summary>
    /// The link <c>app.open(path)</c> opens on a target with a link base: an absolute link as given, else the path joined to the base with one slash,
    /// none after a bare scheme (<c>shopapp://</c> and <c>/orders/42</c> join to <c>shopapp://orders/42</c>). Upstream's <c>joinLink</c>.
    /// </summary>
    public static string JoinLink(string linkBase, string path)
    {
        ArgumentNullException.ThrowIfNull(linkBase);
        ArgumentNullException.ThrowIfNull(path);
        if (LinkScheme().IsMatch(path))
        {
            return path;
        }

        var tail = path.TrimStart('/');
        return linkBase.EndsWith("://", StringComparison.Ordinal)
            ? linkBase + tail
            : linkBase.TrimEnd('/') + "/" + tail;
    }

    /// <summary>
    /// Config load: checks the target's app against the engine that will drive it (upstream's <c>checkTargetApp</c>, run once per target): the
    /// link base against the engine that must open its links, a <c>file:</c> URL against the engine's <c>FileApps</c>, then the engine's own
    /// <see cref="IEngine.ValidateApp"/>, told that the app is a file by the harness's one reading of the URL.
    /// </summary>
    public static void CheckTargetApp(TargetConfig target, IEngine engine)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(engine);
        var where = $"target \"{target.Name}\" app";
        CheckLinkBase(target.App.LinkBase, engine, where);
        CheckLaunchArgumentsFor(target.App, engine, where);
        CheckAllPermissions(target.App.AllPermissions, engine, where);
        var file = target.App.Url is { } url && Urls.NamesFile(url);
        if (file && !engine.FileApps)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where}.url is a file, and engine \"{engine.Name}\" does not declare that it opens one from this machine's disk: use an engine version that does, or serve the app over http(s)"
            );
        }

        engine.ValidateApp(
            EngineAppDeclarationOf(target.Name, target.App),
            new EngineAppCheckInfo { TargetName = target.Name, FileApp = file ? true : null }
        );
    }

    /// <summary>
    /// <see cref="CheckTargetApp(TargetConfig, IEngine)"/> for an engine handle, the form upstream's <c>checkTargetApp</c> takes: the same
    /// checks against what the handle declares, then its <see cref="EngineHandle.ValidateApp"/>, which a handle may leave out.
    /// </summary>
    public static void CheckTargetApp(TargetConfig target, EngineHandle engine)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(engine);
        var where = $"target \"{target.Name}\" app";
        CheckLinkBase(target.App.LinkBase, engine, where);
        CheckLaunchArgumentsFor(target.App, engine, where);
        CheckAllPermissions(target.App.AllPermissions, engine, where);
        var file = target.App.Url is { } url && Urls.NamesFile(url);
        if (file && engine.FileApps != true)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where}.url is a file, and engine \"{engine.Name}\" does not declare that it opens one from this machine's disk: use an engine version that does, or serve the app over http(s)"
            );
        }

        engine.ValidateApp?.Invoke(
            EngineAppDeclarationOf(target.Name, target.App),
            new EngineAppCheckInfo { TargetName = target.Name, FileApp = file ? true : null }
        );
    }

    /// <summary>
    /// A <c>file:</c> app URL against the engine that will open it, for a session started with a base URL and no target config: the engine
    /// must declare <see cref="IEngine.FileApps"/>, and its own <see cref="IEngine.ValidateApp"/> is told <see cref="EngineAppCheckInfo.FileApp"/>.
    /// Any other URL passes. The same two checks as <see cref="CheckTargetApp(TargetConfig, IEngine)"/>.
    /// </summary>
    public static void CheckFileAppUrl(EngineAppDeclaration app, IEngine engine, string targetName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(engine);
        if (app.Url is not { } url || !Urls.NamesFile(url))
        {
            return;
        }

        if (!engine.FileApps)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{targetName}\" app.url is a file, and engine \"{engine.Name}\" does not declare that it opens one from this machine's disk: use an engine version that does, or serve the app over http(s)"
            );
        }

        engine.ValidateApp(app, new EngineAppCheckInfo { TargetName = targetName, FileApp = true });
    }

    /// <summary>
    /// Checks <c>app.linkBase</c>: an absolute URL, or a bare custom scheme (<c>shopapp://</c>), with no query or fragment for a path to land after, on
    /// an engine that opens links. The message never echoes the value: it may carry a token. Upstream's <c>checkLinkBase</c>.
    /// </summary>
    public static string? CheckLinkBase(string? linkBase, IEngine? engine, string where) =>
        CheckLinkBase(
            linkBase,
            engine?.Name,
            engine is not null && (engine.Capabilities & EngineCapabilities.OpenLink) != 0,
            where
        );

    /// <summary><see cref="CheckLinkBase(string?, IEngine?, string)"/> for an engine handle, which opens links when it declares <c>Session.OpenLink</c>.</summary>
    public static string? CheckLinkBase(string? linkBase, EngineHandle? engine, string where) =>
        CheckLinkBase(linkBase, engine?.Name, engine?.Session?.OpenLink is not null, where);

    private static string? CheckLinkBase(string? linkBase, string? engineName, bool opensLinks, string where)
    {
        if (linkBase is null)
        {
            return null;
        }

        if (linkBase.Length == 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", where + ".linkBase must be a non-empty string");
        }

        if (!opensLinks)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + ".linkBase opens links in a device app, and engine "
                    + (engineName ?? "none")
                    + " opens none; a browser target opens paths against app.url"
            );
        }

        if (!Uri.TryCreate(linkBase.Trim(), UriKind.Absolute, out var url))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + ".linkBase must be an absolute URL such as https://shop.example.com or a scheme such as shopapp://"
            );
        }

        if (RefusesLink(linkBase.Trim()))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where + ".linkBase names a refused scheme, " + url.Scheme + ":"
            );
        }

        // Checked on the raw string, which a path joins as written: the URL parser trims whitespace and drops an empty `?` or `#`.
        if (linkBase.AsSpan().IndexOfAny('?', '#') >= 0)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where + ".linkBase must have no query or fragment: a path joins its end"
            );
        }

        if (!string.Equals(linkBase.Trim(), linkBase, StringComparison.Ordinal))
        {
            throw new ConfigurationException("INVALID_CONFIG", where + ".linkBase must have no surrounding whitespace");
        }

        return linkBase;
    }

    /// <summary>
    /// Checks that the engine takes the form <c>app.launchArguments</c> was declared in: an object only on an engine that declares
    /// <see cref="IEngine.LaunchArgumentValues"/>. Upstream's refusal in <c>checkTargetApp</c>, which the port runs where it runs <see cref="CheckLinkBase(string?, IEngine?, string)"/>.
    /// </summary>
    public static void CheckLaunchArgumentsFor(AppConfig? app, IEngine engine, string where)
    {
        ArgumentNullException.ThrowIfNull(engine);
        CheckLaunchArgumentsFor(app, engine.Name, engine.LaunchArgumentValues, where);
    }

    /// <summary><see cref="CheckLaunchArgumentsFor(AppConfig?, IEngine, string)"/> for an engine handle, which takes the object form when it declares <see cref="EngineHandle.LaunchArgumentValues"/>.</summary>
    public static void CheckLaunchArgumentsFor(AppConfig? app, EngineHandle engine, string where)
    {
        ArgumentNullException.ThrowIfNull(engine);
        CheckLaunchArgumentsFor(app, engine.Name, engine.LaunchArgumentValues == true, where);
    }

    private static void CheckLaunchArgumentsFor(AppConfig? app, string engineName, bool takesObject, string where)
    {
        ArgumentNullException.ThrowIfNull(where);
        if (app is null)
        {
            return;
        }

        var launchArguments = LaunchArgumentRules.CheckLaunchArguments(
            where + ".launchArguments",
            app.LaunchArguments,
            app.LaunchArgumentValues
        );
        if (launchArguments?.Values is not null && !takesObject)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + ".launchArguments is an object, and engine \""
                    + engineName
                    + "\" takes only a list of strings: write the arguments as a list, or use an engine version that supports the object syntax"
            );
        }
    }

    /// <summary>
    /// Refuses <c>app.permissions: 'all'</c> on an engine that does not grant it: an engine that predates <c>'all'</c> would read the string as a map
    /// of names. Upstream's check in <c>checkTargetApp</c>.
    /// </summary>
    public static void CheckAllPermissions(bool allPermissions, IEngine engine, string where)
    {
        ArgumentNullException.ThrowIfNull(engine);
        CheckAllPermissions(allPermissions, engine.Name, engine.GrantsAllPermissions, where);
    }

    /// <summary><see cref="CheckAllPermissions(bool, IEngine, string)"/> for an engine handle, which grants <c>'all'</c> when it declares <see cref="EngineHandle.GrantsAllPermissions"/>.</summary>
    public static void CheckAllPermissions(bool allPermissions, EngineHandle engine, string where)
    {
        ArgumentNullException.ThrowIfNull(engine);
        CheckAllPermissions(allPermissions, engine.Name, engine.GrantsAllPermissions == true, where);
    }

    private static void CheckAllPermissions(bool allPermissions, string engineName, bool grants, string where)
    {
        if (allPermissions && !grants)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + ".permissions is 'all', which engine \""
                    + engineName
                    + "\" does not take (it declares no grantsAllPermissions)"
            );
        }
    }

    /// <summary>Whether a link names a refused scheme (see <see cref="Urls.IsRefusedLinkScheme"/>); one that does not parse is refused too. Upstream's <c>refusesLink</c>.</summary>
    public static bool RefusesLink(string link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return !Uri.TryCreate(link, UriKind.Absolute, out var url) || Urls.IsRefusedLinkScheme(url.Scheme + ":");
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex LinkScheme();

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
        var portRequest =
            declaredBase is null || !Urls.RequestsFreePort(declaredBase)
                ? null
                : new PortRequest(new Uri(declaredBase.Href).Host, port);
        var baseUrl =
            declaredBase is not null && portRequest?.Port is { } assigned
                ? Urls.WithPort(declaredBase, assigned)
                : declaredBase;
        int? appPort = baseUrl is null ? null : Urls.PortOf(baseUrl);
        // A file has no host: no site to read frames on, no origin, and nothing to poll.
        var file = baseUrl is not null && Urls.IsFileBase(baseUrl);

        if (app.Command is null && portRequest is not null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                where
                    + " app.url asks for a free port (port 0), but nothing starts on it: add app.command to start the app there on "
                    + PortToken
            );
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
                where
                    + " app.command.reuseExisting cannot find an app already running on a free port: port 0 is a new port every run; give the app a fixed port, or drop reuseExisting"
            );
        }

        var readyUrl = app.ReadyUrl is null
            ? (file ? null : baseUrl?.Href)
            : HttpUrl(ExpandPort(app.ReadyUrl, appPort, where + " app.readyUrl"), where + " app.readyUrl");
        if (command is not null && readyUrl is null)
        {
            throw new ConfigurationException(
                "APP_URL_REQUIRED",
                file
                    ? where
                        + " declares app.command beside a file app.url, and a file answers no readiness poll: declare app.readyUrl, the http(s) URL the command serves once it is ready"
                    : where + " declares app.command without a URL to poll: declare app.url or app.readyUrl beside it"
            );
        }

        var hostname = baseUrl is null || file ? null : new Uri(baseUrl.Href).Host;
        return new ResolvedApp(
            baseUrl,
            portRequest,
            hostname is null ? null : Urls.SiteOf(hostname),
            app.Environment ?? (hostname is not null && !Urls.IsImplicitTestHost(hostname) ? "production" : "test"),
            app.Identity
                ?? (
                    declaredBase is null ? null
                    : file ? declaredBase.Href
                    : declaredBase.Origin + declaredBase.BasePath
                )
                ?? app.BundleId
                ?? app.AppPath,
            app.BundleId,
            app.AppPath,
            app.LinkBase,
            LaunchArgumentRules.CheckLaunchArguments(
                where + " app.launchArguments",
                app.LaunchArguments,
                app.LaunchArgumentValues
            ),
            app.Permissions,
            app.AllPermissions,
            command,
            command is null ? null : readyUrl
        );
    }

    /// <summary>
    /// The target's app as the engine reads it at config load (upstream's <c>EngineAppDeclaration</c>): the declaration, and the site its URL
    /// names. The site is a URL's registrable domain, as <see cref="Urls.SiteOf"/> reads it, and null for an app without a URL.
    /// The launch arguments are checked here, so a target built in code is held to what <c>e2e.config.json</c> is.
    /// </summary>
    public static EngineAppDeclaration EngineAppDeclarationOf(string targetName, AppConfig app)
    {
        ArgumentNullException.ThrowIfNull(targetName);
        ArgumentNullException.ThrowIfNull(app);
        var site =
            app.Url is { } url && !Urls.NamesFile(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? Urls.SiteOf(uri.Host)
                : null;
        var (launchArguments, launchArgumentValues) = LaunchArgumentRules.EngineLaunchArguments(
            LaunchArgumentRules.CheckLaunchArguments(
                "target \"" + targetName + "\" app.launchArguments",
                app.LaunchArguments,
                app.LaunchArgumentValues
            )
        );
        return new EngineAppDeclaration
        {
            Url = app.Url,
            Site = site,
            BundleId = app.BundleId,
            AppPath = app.AppPath,
            LaunchArguments = launchArguments,
            LaunchArgumentValues = launchArgumentValues,
            Permissions = app.Permissions,
            AllPermissions = app.AllPermissions,
        };
    }

    /// <summary>
    /// What an engine's doctor receives about the target's app: its origin and site policy, and what a device launches (upstream's
    /// <c>engineAppInfo</c>). A free port the run has not assigned yet (<c>e2e doctor</c> assigns none) leaves the origin out rather than
    /// report it on port <c>0</c>.
    /// </summary>
    public static EngineAppInfo EngineAppInfoOf(ResolvedApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var unassigned = app.PortRequest is { Port: null };
        var (launchArguments, launchArgumentValues) = LaunchArgumentRules.EngineLaunchArguments(app.LaunchArguments);
        return new EngineAppInfo
        {
            // A file has no origin to scope a credential to.
            Origin = unassigned || app.Base is null || Urls.IsFileBase(app.Base) ? null : app.Base.Origin,
            Site = app.Site,
            BundleId = app.BundleId,
            AppPath = app.AppPath,
            LaunchArguments = launchArguments,
            LaunchArgumentValues = launchArgumentValues,
            Permissions = app.Permissions,
            AllPermissions = app.AllPermissions,
        };
    }

    /// <summary>
    /// A target's app as it enters the config digest (upstream's <c>digestTargetApp</c>): the declaration, with the command's env values
    /// reduced to their names. The run's port never enters it, so a worker handed the port reads the same digest.
    /// </summary>
    public static JsonObject DigestTargetApp(AppConfig app)
    {
        // A key the app leaves out stays out, as upstream's obj() drops an undefined one.
        var digest = new JsonObject();
        void Add(string key, JsonNode? value)
        {
            if (value is not null)
            {
                digest[key] = value;
            }
        }

        Add("url", app.Url);
        Add("bundleId", app.BundleId);
        Add("appPath", app.AppPath);
        Add("linkBase", app.LinkBase);
        Add("identity", app.Identity);
        Add("environment", app.Environment);
        Add(
            "launchArguments",
            app.LaunchArguments is not null
                    ? new JsonArray([.. app.LaunchArguments.Select(argument => (JsonNode?)JsonValue.Create(argument))])
                : app.LaunchArgumentValues is null ? null
                : new JsonObject(
                    app.LaunchArgumentValues.Where(pair => pair.Value is not null)
                        .Select(pair =>
                            KeyValuePair.Create(
                                pair.Key,
                                (JsonNode?)(
                                    pair.Value!.Value.Kind switch
                                    {
                                        LaunchArgumentValueKind.String => JsonValue.Create(pair.Value.Value.Text),
                                        LaunchArgumentValueKind.Number => JsonValue.Create(pair.Value.Value.Number),
                                        LaunchArgumentValueKind.Boolean => JsonValue.Create(pair.Value.Value.Flag),
                                        _ => new JsonArray([
                                            .. pair.Value.Value.Items!.Select(item =>
                                                (JsonNode?)JsonValue.Create(item)
                                            ),
                                        ]),
                                    }
                                )
                            )
                        )
                )
        );
        Add(
            "permissions",
            app.AllPermissions ? JsonValue.Create("all")
                : app.Permissions is null ? null
                : new JsonObject(
                    app.Permissions.Select(pair =>
                        KeyValuePair.Create(
                            pair.Key,
                            (JsonNode?)JsonValue.Create(pair.Value.ToString().ToLowerInvariant())
                        )
                    )
                )
        );
        Add("command", app.Command is null ? null : Command.DigestCommand(app.Command));
        Add("readyUrl", app.ReadyUrl);
        return digest;
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
            : throw new ConfigurationException(
                "INVALID_CONFIG",
                label + " uses " + PortToken + ", but the target declares no url to take the port from"
            );
    }

    /// <summary>The command with <c>{port}</c> expanded in every <c>args</c> entry and <c>env</c> value.</summary>
    private static CommandConfig ExpandCommandPort(CommandConfig command, string label, int? port) =>
        new()
        {
            Executable = command.Executable,
            Args = command.Args?.Select(arg => ExpandPort(arg, port, label + ".args")).ToList().AsReadOnly(),
            Cwd = command.Cwd,
            Env = command.Env is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    command.Env.ToDictionary(
                        pair => pair.Key,
                        pair => ExpandPort(pair.Value, port, label + ".env." + pair.Key),
                        StringComparer.Ordinal
                    )
                ),
            InheritEnv = command.InheritEnv,
            StartupTimeout = command.StartupTimeout,
            ShutdownTimeout = command.ShutdownTimeout,
            Log = command.Log,
            ReuseExisting = command.ReuseExisting,
        };
}
