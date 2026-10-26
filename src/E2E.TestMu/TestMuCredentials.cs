// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.TestMu;

/// <summary>The TestMu AI username and access key (upstream's <c>credentials.ts</c>).</summary>
internal static class TestMuCredentials
{
    private const string LtUsername = "LT_USERNAME";
    private const string LtAccessKey = "LT_ACCESS_KEY";

    /// <summary>
    /// <c>LT_USERNAME</c> and <c>LT_ACCESS_KEY</c> (or the .NET-only <c>TESTMU_USERNAME</c> and <c>TESTMU_ACCESS_KEY</c> aliases) from the run's
    /// environment (upstream <c>testmuCredentials</c>); throws naming each one that is unset or blank.
    /// </summary>
    internal static (string Username, string AccessKey) Get(IReadOnlyDictionary<string, string?> env)
    {
        var username = EnvValue(env, LtUsername, "TESTMU_USERNAME");
        var accessKey = EnvValue(env, LtAccessKey, "TESTMU_ACCESS_KEY");
        if (username is null || accessKey is null)
        {
            var missing = new[] { username is null ? LtUsername : null, accessKey is null ? LtAccessKey : null }.OfType<string>().ToArray();
            throw new InvalidOperationException(
                $"{string.Join(" and ", missing)} {(missing.Length == 1 ? "is" : "are")} not set; set {LtUsername} and {LtAccessKey} (or TESTMU_USERNAME and TESTMU_ACCESS_KEY) to your TestMu AI username and access key in the environment the tests start in. Both are in the TestMu automation dashboard, under Access Key.");
        }

        return (username, accessKey);
    }

    /// <summary>A non-blank variable from the run's environment, trimmed, or <see langword="null"/> (upstream <c>envValue</c>).</summary>
    private static string? EnvValue(IReadOnlyDictionary<string, string?> env, string name, string alias)
    {
        foreach (var variable in new[] { name, alias })
        {
            if (env.GetValueOrDefault(variable)?.Trim() is { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }
}
