// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>
/// Upstream's <c>describe('renderGithubWorkflow')</c>. The port writes the pipeline as text and has no package manager, Node
/// version, or Expo and React Native build, so these <c>it</c>s are not ported: "writes the template unchanged ..." (there is
/// no template file), "builds a bare React Native app with its workspace ...", "skips Expo prebuild when android/ is already
/// there", "sets up every job for a workspace member ...", "points the cache at a lockfile below the root ...", the
/// package manager <c>it.each</c>, "sets up Bun ...", "keys the caches on Bun's older bun.lockb ...", and "keeps a folder
/// name with a space as one value".
/// </summary>
public sealed class RenderGithubWorkflowTests : InitCiTestBase
{
    [Fact]
    // Partial: the job runs dotnet test, and the web job is the only one in a config with no phone target.
    public void Partial_Writes_only_the_web_job_when_the_config_has_no_phone_target()
    {
        var workflow = Ci.RenderGithubWorkflow(Base);
        Assert.Equal(["web"], Names(workflow, @"^  (web|ios|android):$"));
        Assert.Contains(
            "run: dotnet test -c Release --no-build MyApp.E2E/MyApp.E2E.csproj",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "pwsh MyApp.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium",
            workflow,
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: the phone jobs run the whole project, since the port picks a target in the fixture, not with a flag.
    public void Partial_Writes_only_the_phone_jobs_for_a_mobile_only_config()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = new CiJobs(false, true, true) });
        Assert.Equal(["ios", "android"], Names(workflow, @"^  (web|ios|android):$"));
        Assert.Contains(
            "adb install -r android/app/build/outputs/apk/release/app-release.apk",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains("E2E_DEVICE: emulator-5554", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("playwright.ps1", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Runs_the_web_job_beside_the_phone_jobs_when_a_config_has_both()
    {
        Assert.Equal(
            ["web", "ios", "android"],
            Names(Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs }), @"^  (web|ios|android):$")
        );
    }

    [Fact]
    // Partial: the port has no Expo or React Native build, so every phone build is the step that fails until it is replaced.
    public void Partial_Writes_a_build_step_that_fails_until_it_is_replaced_saying_where_the_build_goes()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = new CiJobs(false, true, true) });
        Assert.Contains(
            "# Replace this step with your app's Release build for the iOS simulator, with the .app under build/.",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "# Replace this step with your app's release build, an APK at android/app/build/outputs/apk/release/app-release.apk.",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains("exit 1", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_template_comments_and_pinned_tags()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs });
        Assert.Contains("# Written by `e2e init --ci github-actions`", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "# .e2e is a hidden directory, which upload-artifact skips when it expands a folder.",
            workflow,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Sets_no_env_block_when_the_run_reads_no_key()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Secrets = [] });
        Assert.DoesNotContain("env:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("AI_GATEWAY_API_KEY", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_secret_of_each_key_the_run_reads()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Secrets = ["OPENROUTER_API_KEY"], Jobs = AllJobs });
        Assert.Equal(
            3,
            System
                .Text.RegularExpressions.Regex.Matches(
                    workflow,
                    @"OPENROUTER_API_KEY: \$\{\{ secrets\.OPENROUTER_API_KEY \}\}"
                )
                .Count
        );
    }

    [Fact]
    // .NET-only: a project path with a space is one shell word.
    public void Keeps_a_folder_name_with_a_space_as_one_value()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { ProjectFile = "My App/My App.csproj" });
        Assert.Contains("dotnet build -c Release 'My App/My App.csproj'", workflow, StringComparison.Ordinal);
        Assert.Contains("pwsh 'My App/bin/Release/net10.0/playwright.ps1' install", workflow, StringComparison.Ordinal);
    }
}
