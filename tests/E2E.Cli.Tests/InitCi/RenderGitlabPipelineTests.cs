// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>
/// Upstream's <c>describe('renderGitlabPipeline')</c>. The port has no template file, package manager, workspace, or Expo
/// and React Native build, so "writes the template unchanged ...", "installs at the workspace root and runs in the project
/// folder", "changes into a project below the root before the install ...", the package manager <c>it.each</c>, and
/// "installs Bun into the node image ..." are not ported.
/// </summary>
public sealed class RenderGitlabPipelineTests : InitCiTestBase
{
    private static readonly string[] Jobs = [".e2e-mobile", "e2e-ios", "e2e-android"];

    [Fact]
    // Partial: the header names this repository, and the pipeline starts at the mobile base.
    public void Partial_Keeps_the_header_and_starts_with_no_blank_line_when_the_pipeline_has_no_web_job()
    {
        var pipeline = Ci.RenderGitlabPipeline(Base with { Jobs = new CiJobs(false, false, true) });
        Assert.StartsWith("# Written by `e2e init --ci gitlab`", pipeline, StringComparison.Ordinal);
        Assert.Contains(
            "# the default branch, one job per platform. See https://github.com/hardkoded/e2e-dotnet\n\n# GitLab's hosted runners",
            pipeline,
            StringComparison.Ordinal
        );
        Assert.Equal([".e2e-mobile", "e2e-android"], Names(pipeline, @"^(\S+):$"));
    }

    [Fact]
    public void Writes_only_the_web_job_without_the_mobile_base_or_its_comment_when_the_config_has_no_phone_target()
    {
        var pipeline = Ci.RenderGitlabPipeline(Base);
        Assert.Equal(["e2e-web"], Names(pipeline, @"^(\S+):$"));
        Assert.DoesNotContain("self-managed", pipeline, StringComparison.Ordinal);
        Assert.Contains(
            "    - dotnet test -c Release --no-build MyApp.E2E/MyApp.E2E.csproj\n",
            pipeline,
            StringComparison.Ordinal
        );
        Assert.Contains("image: mcr.microsoft.com/dotnet/sdk:10.0", pipeline, StringComparison.Ordinal);
        // CI/CD variables reach the job's environment on their own; the file names no key.
        Assert.DoesNotContain("AI_GATEWAY_API_KEY", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the build step is always the one that fails until it is replaced.
    public void Partial_Runs_the_phone_jobs_on_self_managed_runners_build_a_device_of_their_own_then_the_tests()
    {
        var pipeline = Ci.RenderGitlabPipeline(Base with { Jobs = new CiJobs(false, true, true) });
        Assert.Equal(Jobs, Names(pipeline, @"^(\S+):$"));
        Assert.Contains("  tags: [macos]", pipeline, StringComparison.Ordinal);
        Assert.Contains(
            "export E2E_DEVICE=$(xcrun simctl create \"e2e-$CI_JOB_ID\" \"iPhone 17 Pro\" com.apple.CoreSimulator.SimRuntime.iOS-26-5)",
            pipeline,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "    # Replace this step with your app's release build, an APK at android/app/build/outputs/apk/release/app-release.apk.\n    - echo",
            pipeline,
            StringComparison.Ordinal
        );
        Assert.Equal(
            2,
            System
                .Text.RegularExpressions.Regex.Matches(
                    pipeline,
                    "    - dotnet test -c Release --no-build MyApp.E2E/MyApp.E2E.csproj\n"
                )
                .Count
        );
    }

    [Fact]
    public void Renames_every_job_and_what_the_phone_jobs_extend_beside_a_pipeline_that_has_one_of_the_names()
    {
        var pipeline = Ci.RenderGitlabPipeline(Base with { Jobs = AllJobs, JobSuffix = "-run" });
        Assert.Equal(
            ["e2e-web-run", ".e2e-mobile-run", "e2e-ios-run", "e2e-android-run"],
            Names(pipeline, @"^(\S+):$")
        );
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pipeline, @"extends: \.e2e-mobile-run\n").Count);
        Assert.StartsWith("# Written by `e2e init --ci gitlab`", pipeline, StringComparison.Ordinal);
        Assert.Contains("    # GitLab caches only paths inside the project.\n", pipeline, StringComparison.Ordinal);
    }
}
