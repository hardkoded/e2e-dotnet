// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

public sealed class ManagedProcessEnvTests : IDisposable
{
    private static readonly string[] Variables =
        ["E2E_TEST_DATABASE_URL", "E2E_TEST_STRIPE_KEY", "E2E_TEST_STRIPE_HOOK", "E2E_TEST_MODEL_KEY", "E2E_TEST_DATABASE_URL_RO"];

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-process-env-").FullName;

    public ManagedProcessEnvTests()
    {
        Environment.SetEnvironmentVariable("E2E_TEST_DATABASE_URL", "postgres://from-host");
        Environment.SetEnvironmentVariable("E2E_TEST_STRIPE_KEY", "sk_host");
        Environment.SetEnvironmentVariable("E2E_TEST_STRIPE_HOOK", "wh_host");
        Environment.SetEnvironmentVariable("E2E_TEST_MODEL_KEY", "model-key");
        Environment.SetEnvironmentVariable("E2E_TEST_DATABASE_URL_RO", "postgres://replica");
    }

    public void Dispose()
    {
        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Runs a command that writes its own environment to <c>env.txt</c> in the project root, and reads it back.</summary>
    private async Task<Dictionary<string, string>> EnvOf(IReadOnlyList<string>? inheritEnv = null, IReadOnlyDictionary<string, string>? env = null, IReadOnlyDictionary<string, string?>? runEnv = null)
    {
        await new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["write-env", "env.txt"], cwd: _dir, env: env, inheritEnv: inheritEnv),
            _dir,
            Readiness.WaitForExit,
            runEnv is null ? null : new ManagedProcessHooks { Env = runEnv }).StartAsync();
        return (await File.ReadAllLinesAsync(Path.Combine(_dir, "env.txt")))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Passes_only_the_default_set_without_inheritEnv()
    {
        var env = await EnvOf();
        Assert.DoesNotContain(env.Keys, key => key.StartsWith("E2E_TEST_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Copies_listed_variables_leaves_the_rest_out_and_lets_command_env_win()
    {
        var env = await EnvOf(["E2E_TEST_DATABASE_URL", "E2E_TEST_STRIPE_*"], new Dictionary<string, string> { ["E2E_TEST_STRIPE_HOOK"] = "wh_config" });
        Assert.Equal("postgres://from-host", env["E2E_TEST_DATABASE_URL"]);
        Assert.Equal("sk_host", env["E2E_TEST_STRIPE_KEY"]);
        Assert.Equal("wh_config", env["E2E_TEST_STRIPE_HOOK"]);
        Assert.DoesNotContain("E2E_TEST_MODEL_KEY", env.Keys);
        Assert.DoesNotContain("E2E_TEST_DATABASE_URL_RO", env.Keys);
    }

    [Fact]
    public async Task Copies_from_the_environment_the_run_resolves_against_not_the_process_environment_when_one_is_passed()
    {
        var env = await EnvOf(["E2E_TEST_DATABASE_URL", "E2E_TEST_STRIPE_*"], runEnv: new Dictionary<string, string?> { ["E2E_TEST_DATABASE_URL"] = "postgres://per-task", ["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT") });
        Assert.Equal("postgres://per-task", env["E2E_TEST_DATABASE_URL"]);
        Assert.DoesNotContain("E2E_TEST_STRIPE_KEY", env.Keys);
    }

    /// <summary>
    /// Partial: it checks the environment <c>ManagedProcess.ChildEnv</c> builds with Windows name matching, where upstream fakes the platform
    /// and reads the environment the child saw. The child's own environment follows the host's rules.
    /// </summary>
    [Fact]
    public void Partial_Matches_and_replaces_names_case_insensitively_on_Windows()
    {
        var (env, _) = ManagedProcessUnderTest.ChildEnv(
            new CommandConfig
            {
                Executable = "x",
                InheritEnv = ["E2E_TEST_DATABASE_URL", "E2E_TEST_STRIPE_*"],
                Env = new Dictionary<string, string> { ["e2e_test_stripe_key"] = "sk_config" },
            },
            new Dictionary<string, string?> { ["E2e_Test_Database_Url"] = "postgres://host", ["E2E_Test_Stripe_Key"] = "sk_host" },
            caseInsensitive: true);
        Assert.Equal(
            [("E2e_Test_Database_Url", "postgres://host"), ("e2e_test_stripe_key", "sk_config")],
            env.Where(pair => pair.Key.StartsWith("E2E_TEST_", StringComparison.OrdinalIgnoreCase)).Select(pair => (pair.Key, pair.Value)).ToArray());
    }
}
