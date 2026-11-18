// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('detectMobileBuilds')</c>.</summary>
public sealed class DetectMobileBuildsTests : InitCiTestBase
{
    [Fact]
    public void Builds_Expo_with_CNG_and_skips_prebuild_when_android_is_committed()
    {
        Assert.Equal(
            (new IosBuild.Expo(), new AndroidBuild.Expo(true)),
            Ci.DetectMobileBuilds(Dir, name => name == "expo")
        );
        Write("android/gradlew", "");
        Assert.Equal(new AndroidBuild.Expo(false), Ci.DetectMobileBuilds(Dir, name => name == "expo").Android);
    }

    [Fact]
    public void Builds_bare_React_Native_from_its_workspace_and_Gradle_wrapper_else_falls_back_to_a_step_to_replace()
    {
        static bool Rn(string name) => name == "react-native";
        Assert.Equal((new IosBuild.Custom(), new AndroidBuild.Custom()), Ci.DetectMobileBuilds(Dir, Rn));
        Directory.CreateDirectory(Path_("ios/MyApp.xcworkspace"));
        Directory.CreateDirectory(Path_("ios/Pods.xcworkspace"));
        Write("android/gradlew", "");
        Write("Gemfile", "");
        Assert.Equal(
            (new IosBuild.ReactNative("MyApp", true), new AndroidBuild.ReactNative()),
            Ci.DetectMobileBuilds(Dir, Rn)
        );
    }

    [Fact]
    public void Writes_a_step_to_replace_for_a_native_app()
    {
        Assert.Equal((new IosBuild.Custom(), new AndroidBuild.Custom()), Ci.DetectMobileBuilds(Dir, _ => false));
    }

    [Fact]
    public void Reads_no_workspace_from_an_ios_that_is_a_file()
    {
        Write("ios", "");
        Assert.Equal(new IosBuild.Custom(), Ci.DetectMobileBuilds(Dir, name => name == "react-native").Ios);
    }

    [Fact]
    // .NET-only: the app is the nearest folder above the test project with a package.json, and its dependencies come from it.
    public void Finds_the_app_above_the_test_project_and_reads_its_dependencies()
    {
        Write("app/package.json", """{ "dependencies": { "expo": "^54.0.0" }, "devDependencies": { "jest": "1" } }""");
        Directory.CreateDirectory(Path_("app/tests/MyApp.E2E"));
        var app = Ci.FindAppDir(Path_("app/tests/MyApp.E2E"), Dir);
        Assert.Equal(Path_("app"), app);
        Assert.True(Ci.DependsOn(app!, "expo"));
        Assert.True(Ci.DependsOn(app!, "jest"));
        Assert.False(Ci.DependsOn(app!, "react-native"));
        Assert.Null(Ci.FindAppDir(Path_("tests"), Dir));
    }

    [Fact]
    // .NET-only: the lockfile names the package manager.
    public void Reads_the_package_manager_from_the_lockfile()
    {
        Assert.Equal(("npm", false), Ci.DetectManager(Dir));
        Write("pnpm-lock.yaml", "");
        Assert.Equal(("pnpm", false), Ci.DetectManager(Dir));
        File.Delete(Path_("pnpm-lock.yaml"));
        Write("yarn.lock", "");
        Write(".yarnrc.yml", "");
        Assert.Equal(("yarn", true), Ci.DetectManager(Dir));
    }

    [Fact]
    // .NET-only: a workspace app keeps its lockfile at the workspace root; a target name YAML would read as a boolean is quoted.
    public void Reads_the_lockfile_of_the_workspace_root_and_quotes_a_target_name_YAML_reads_as_a_boolean()
    {
        Write("pnpm-lock.yaml", "");
        Directory.CreateDirectory(Path_("apps/mobile"));
        Assert.Equal(("pnpm", false), Ci.DetectManager(Path_("apps/mobile"), Dir));
        Assert.Equal(("npm", false), Ci.DetectManager(Path_("apps/mobile")));
        var workflow = Ci.RenderGithubWorkflow(
            new WorkflowContext("a.csproj", "10.0", [], new CiJobs(new CiWebJob(["null"]), null, null))
        );
        Assert.Contains("E2E_TARGET: \"null\"", workflow, StringComparison.Ordinal);
    }
}
