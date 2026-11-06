// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>One <c>config.credentials</c> entry, resolved (upstream's <c>ResolvedCredential</c>, <c>config/resolve.ts</c>): an account whose password is the secret <c>{name}.password</c>.</summary>
/// <param name="Name">The entry's name in <c>credentials</c>.</param>
/// <param name="Username">The username from <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> or the config.</param>
/// <param name="Password">The password from <c>E2E_USER_&lt;NAME&gt;_PASSWORD</c> or the config, named <c>&lt;name&gt;.password</c>.</param>
public sealed record ResolvedCredential(string Name, string Username, Secret Password);

/// <summary>
/// The engine of a resolved target as selection reads it: its name, the capabilities it declares, and the secrets its options hold (the slice of
/// upstream's <c>Engine</c> that <c>select</c> and <c>checkEngineSecrets</c> use). .NET-only name.
/// </summary>
internal sealed record ResolvedEngine(string Name, IReadOnlySet<string> Capabilities, IReadOnlyList<Secret>? Secrets = null);

/// <summary>A target as selection reads it (upstream's <c>ResolvedTarget</c>, <c>config/resolve.ts</c>); null <see cref="Engine"/> grades no capability.</summary>
internal sealed record ResolvedTarget(string Name, string Platform, AppConfig App, ResolvedEngine? Engine = null);

/// <summary>
/// The resolved config as selection and the test filters read it (upstream's <c>ResolvedConfig</c>, <c>config/resolve.ts</c>). The port has
/// no resolver yet, so only these members exist.
/// </summary>
internal sealed record ResolvedConfig
{
    public required string ProjectRoot { get; init; }

    /// <summary>The loaded config file; null for a config object.</summary>
    public string? ConfigPath { get; init; }

    public bool Ci { get; init; }

    public required IReadOnlyList<ResolvedTarget> Targets { get; init; }

    /// <summary>The test filters, in order; empty when unset. A run with <c>--changed-since</c> applies them.</summary>
    public IReadOnlyList<ITestFilter> Filters { get; init; } = [];

    /// <summary>Every secret the run holds, by name: the <c>config.secrets</c> entries and every credential's password.</summary>
    public IReadOnlyList<(string Name, string Value)> AllSecrets { get; init; } = [];

    /// <summary>The longest a filter's name may be (upstream's <c>MAX_FILTER_NAME</c>, <c>config/resolve.ts</c>).</summary>
    internal const int MaxFilterName = 256;

    /// <summary>
    /// Every secret an engine's options hold (a <c>Secrets.Get</c> the config evaluated before any run existed) names a configured secret, so a
    /// typo fails at load with the nearest name, not mid-run (upstream's <c>checkEngineSecrets</c>, <c>config/resolve.ts</c>). A credential's
    /// password is not one: an engine option takes a <c>secrets</c> entry. Differences: the port's config has no engine to hold such a
    /// handle, so no resolver calls it yet.
    /// </summary>
    internal static void CheckEngineSecrets(IReadOnlyList<ResolvedTarget> targets, Secrets secrets, IReadOnlyDictionary<string, ResolvedCredential> credentials)
    {
        foreach (var target in targets)
        {
            foreach (var secret in target.Engine?.Secrets ?? [])
            {
                if (secrets.Names.Contains(secret.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                var credential = ConfigSecrets.CredentialNamed(secret.Name, credentials);
                var hint = credential is null
                    ? Suggestions.DidYouMean(secret.Name, [.. secrets.Names])
                    : "; credential " + LocatorExpect.Quote(credential.Name) + " is not a secrets entry, and an engine option takes one: declare the value under config.secrets";
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    "target " + LocatorExpect.Quote(target.Name) + " engine " + target.Engine!.Name + " uses secrets.get(" + LocatorExpect.Quote(secret.Name) + "), which is not configured; add it to config.secrets" + hint);
            }
        }
    }

    /// <summary>
    /// Resolves the test filters (upstream's <c>resolveTestFilters</c>, <c>config/resolve.ts</c>): the list in declaration order, empty when
    /// null. A filter's name is how the report and errors tell it apart, so each name follows the target name rules, is at most
    /// <see cref="MaxFilterName"/> characters, and is unique. A null item (upstream's hole, or an item that is no <c>TestFilter</c>) is
    /// refused. Differences: upstream's refusals of a value that is not a list, of a function in the list, and of an item without a
    /// <c>filter</c> function have no port, since the list and its items are typed.
    /// </summary>
    internal static IReadOnlyList<ITestFilter> ResolveTestFilters(IReadOnlyList<ITestFilter?>? raw)
    {
        if (raw is null)
        {
            return [];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var filters = new List<ITestFilter>(raw.Count);
        for (var index = 0; index < raw.Count; index++)
        {
            if (raw[index] is not { Name: { } name } value)
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filters[{index}] must be a TestFilter: {{ name, filter(tests, context) }}");
            }

            if (!E2EConfig.ValidName(name))
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"invalid filter name {LocatorExpect.Quote(name)}: names are ASCII letters, numbers, \"_\", \"-\", or \".\", and cannot be only dots");
            }

            if (name.Length > MaxFilterName)
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filter name {LocatorExpect.Quote(ReportFormat.Ellipsize(name, 32))} is longer than {MaxFilterName} characters");
            }

            if (!names.Add(name))
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filters has two filters named {LocatorExpect.Quote(name)}; the report tells filters apart by name");
            }

            filters.Add(value);
        }

        return filters;
    }
}
