// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>
/// Upstream's <c>describe('renderGithubWorkflow')</c>. The port has no workspace or Node
/// version file, so these <c>it</c>s are not ported: "sets up
/// every job for a workspace member ...", "points the cache at a lockfile below the root ...", the package manager
/// <c>it.each</c>, "sets up Bun ...", and "keys the caches on Bun's older bun.lockb ...".
/// </summary>
public sealed class RenderGithubWorkflowTests : InitCiTestBase
{
    [Fact]
    // The template is the port's own: a .NET project, dotnet test, and E2E_TARGET where upstream's has e2e run --target.
    public void Partial_Writes_the_template_unchanged_for_npm_at_the_repository_root_Expo_on_both_phones_and_the_model_key_from_secrets()
    {
        Assert.Equal(Ci.ReadTemplateText("github-actions.yml"), Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs }));
    }

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
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = PhoneJobs });
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
    public void Writes_a_build_step_that_fails_until_it_is_replaced_for_any_other_app_saying_where_the_build_goes()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = CustomJobs });
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

    [Fact]
    // Partial: upstream's run step names its targets with --target; the port sets E2E_TARGET for a job that runs one.
    public void Partial_Writes_only_the_phone_jobs_for_a_mobile_only_config_each_naming_its_target()
    {
        var workflow = Ci.RenderGithubWorkflow(
            Base with
            {
                Secrets = [],
                Jobs = PhoneJobs with { Ios = new CiPhoneJob<IosBuild>(["iphone"], new IosBuild.Expo()) },
            }
        );
        Assert.Contains("jobs:\n  ios:\n", workflow, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?m)^ +$", workflow);
        Assert.Contains("        env:\n          E2E_TARGET: iphone\n", workflow, StringComparison.Ordinal);
        Assert.Contains("          E2E_TARGET: android\n", workflow, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: upstream names several targets in one --target; E2E_TARGET takes one, so a job with several sets none.
    public void Partial_Runs_the_web_job_on_every_target_off_the_phones_when_a_config_has_both()
    {
        var one = Ci.RenderGithubWorkflow(Base with { Secrets = [], Jobs = AllJobs });
        Assert.Contains("E2E_TARGET: web\n", one, StringComparison.Ordinal);
        var several = Ci.RenderGithubWorkflow(
            Base with
            {
                Secrets = [],
                Jobs = AllJobs with { Web = new CiWebJob(["web", "api"]) },
            }
        );
        Assert.DoesNotContain("E2E_TARGET: web", several, StringComparison.Ordinal);
        Assert.DoesNotContain("E2E_TARGET: api", several, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: bare React Native builds in the app's folder; the install and the pods commands are the same as upstream's.
    public void Partial_Builds_a_bare_React_Native_app_with_its_workspace_through_Bundler_when_it_has_a_Gemfile()
    {
        var workflow = Ci.RenderGithubWorkflow(
            Base with
            {
                Jobs = new CiJobs(
                    null,
                    new CiPhoneJob<IosBuild>(["ios"], new IosBuild.ReactNative("MyApp", true)),
                    new CiPhoneJob<AndroidBuild>(["android"], new AndroidBuild.ReactNative())
                ),
            }
        );
        Assert.Contains(
            "      # Installs the pods, then builds the workspace for the simulator into build/.\n      - name: Build the app\n        run: |\n"
                + "          bundle install\n          (cd ios && bundle exec pod install)\n          xcodebuild -workspace ios/MyApp.xcworkspace -scheme MyApp -configuration Release -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath build\n",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "# Only the emulator's ABI: building all four takes about 25 minutes longer.\n      - name: Build the app\n        run: (cd android && ./gradlew :app:assembleRelease -PreactNativeArchitectures=x86_64)\n",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains("run: npm ci\n", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Skips_Expo_prebuild_when_android_is_already_there()
    {
        var workflow = Ci.RenderGithubWorkflow(
            Base with
            {
                Jobs = new CiJobs(null, null, new CiPhoneJob<AndroidBuild>(["android"], new AndroidBuild.Expo(false))),
            }
        );
        Assert.DoesNotContain("expo prebuild", workflow, StringComparison.Ordinal);
        Assert.Contains("run: (cd android && ./gradlew :app:assembleRelease", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "expo prebuild",
            Ci.RenderGithubWorkflow(Base with { Jobs = PhoneJobs }),
            StringComparison.Ordinal
        );
    }

    [Fact]
    // .NET-only: the app's folder is where the install and the build run, and where the APK and the .app are.
    public void Builds_an_app_in_a_folder_there_and_installs_its_output_from_there()
    {
        var workflow = Ci.RenderGithubWorkflow(
            Base with
            {
                Jobs = PhoneJobs,
                AppDir = "apps/mobile",
                Manager = "pnpm",
            }
        );
        Assert.Contains("working-directory: apps/mobile", workflow, StringComparison.Ordinal);
        Assert.Contains("pnpm exec expo run:ios", workflow, StringComparison.Ordinal);
        Assert.Contains("pnpm install --frozen-lockfile", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "adb install -r apps/mobile/android/app/build/outputs/apk/release/app-release.apk",
            workflow,
            StringComparison.Ordinal
        );
        Assert.Contains("find apps/mobile/build -maxdepth 5", workflow, StringComparison.Ordinal);
    }

    [Fact]
    // .NET-only: upstream's --reporter list,junit; the port's junit logger writes .e2e/junit.xml, which the report upload keeps.
    public void Writes_a_JUnit_report_with_the_junit_logger_and_keeps_it()
    {
        var workflow = Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs });
        Assert.Equal(
            3,
            System
                .Text.RegularExpressions.Regex.Matches(
                    workflow,
                    @"--logger ""junit;LogFilePath=\$\{\{ github\.workspace \}\}/\.e2e/junit\.xml"""
                )
                .Count
        );
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(workflow, @"\.e2e/junit\.xml\n").Count);
        var without = Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs, Junit = false });
        Assert.DoesNotContain("junit", without, StringComparison.Ordinal);
    }
}
