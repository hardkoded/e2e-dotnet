// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.BrowserStack;

/// <summary>The BrowserStack username and access key (upstream's <c>BrowserStackCredentials</c>, <c>credentials.ts</c>).</summary>
internal sealed record BrowserStackCredentials(string Username, string AccessKey)
{
    private const string UsernameVariable = "BROWSERSTACK_USERNAME";
    private const string AccessKeyVariable = "BROWSERSTACK_ACCESS_KEY";

    /// <summary>The access key is a secret: a record would print it. (.NET-only: a TypeScript object prints nothing by itself.)</summary>
    public override string ToString() => $"BrowserStackCredentials {{ Username = {Username}, AccessKey = ***** }}";

    /// <summary>
    /// <c>BROWSERSTACK_USERNAME</c> and <c>BROWSERSTACK_ACCESS_KEY</c> from the run's environment (upstream <c>browserstackCredentials</c>);
    /// throws naming each one that is unset or blank.
    /// </summary>
    public static BrowserStackCredentials FromEnvironment(IReadOnlyDictionary<string, string?> env)
    {
        var username = EnvValue(env, UsernameVariable);
        var accessKey = EnvValue(env, AccessKeyVariable);
        if (username is null || accessKey is null)
        {
            var missing = new[] { username is null ? UsernameVariable : null, accessKey is null ? AccessKeyVariable : null }.OfType<string>().ToArray();
            throw new InvalidOperationException(
                $"{string.Join(" and ", missing)} {(missing.Length == 1 ? "is" : "are")} not set; set {UsernameVariable} and {AccessKeyVariable} to your BrowserStack username and access key in the environment the tests start in");
        }

        return new BrowserStackCredentials(username, accessKey);
    }

    /// <summary>A non-blank variable from the run's environment, trimmed, or <see langword="null"/> (upstream <c>envValue</c>).</summary>
    private static string? EnvValue(IReadOnlyDictionary<string, string?> env, string name) =>
        env.GetValueOrDefault(name)?.Trim() is { Length: > 0 } value ? value : null;
}
