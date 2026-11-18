// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('with a CI workflow')</c> of <c>tests/unit/cli-init.test.ts</c>. The port has no workspace and
/// reads its JSON config without running it, so these <c>it</c>s are not ported: "installs a workspace member at the
/// workspace root with the workspace manager", and "loads an existing config only after the changes are confirmed and the
/// dependencies installed". "Writes the iOS and Android jobs alone for a mobile-only project" runs on the host, which picks the platform.
/// </summary>
public sealed class WithACiWorkflowTests : CliInitTestBase
{
    private const string Workflow = ".github/workflows/e2e.yml";

    private const string EngineProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
          <ItemGroup><PackageReference Include="E2E" Version="*" /><PackageReference Include="E2E.NUnit" Version="*" /></ItemGroup>
        </Project>
        """;

    private static readonly string HostPlatform = OperatingSystem.IsMacOS() ? "ios" : "android";

    [Fact]
    // Partial: the workflow runs dotnet test where upstream runs e2e run.
    public async Task Partial_Writes_the_GitHub_Actions_workflow_with_ci_and_says_what_CI_still_needs()
    {
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(InitResult.Scaffolded, outcome.Result);
        Assert.Equal("github-actions", outcome.Ci);
        Assert.Contains(
            $"run: dotnet test -c Release --no-build {ProjectFile()}",
            Read(Workflow),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "AI_GATEWAY_API_KEY: ${{ secrets.AI_GATEWAY_API_KEY }}",
            Read(Workflow),
            StringComparison.Ordinal
        );
        Assert.Contains($"Created {Workflow} (GitHub Actions)", Output, StringComparison.Ordinal);
        Assert.Contains("add it with gh secret set AI_GATEWAY_API_KEY", Output, StringComparison.Ordinal);
        // The new config sets no app.command, so nothing would start the app on the runner.
        Assert.Contains(
            "Nothing starts the app in CI: set app.command in e2e.config.json",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Writes_none_under_yes_alone_and_none_over_an_existing_workflow()
    {
        Assert.Equal("none", (await RunInitAsync()).Ci);
        Assert.False(Exists(Workflow));

        Directory.CreateDirectory(Path_(".github/workflows"));
        await File.WriteAllTextAsync(Path_(Workflow), "name: mine\n");
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.Equal(InitResult.AlreadyInitialized, outcome.Result);
        Assert.Equal("name: mine\n", Read(Workflow));
        Assert.Contains($"Exists, not touching: {Workflow}", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asks_with_GitHub_Actions_preselected_in_a_GitHub_repository_and_not_again_once_the_workflow_exists()
    {
        Prompts.Engines = ["web"];
        Directory.CreateDirectory(Path_(".github"));
        Prompts.NextSelect("none");
        Prompts.NextSelect("github-actions");
        Prompts.NextConfirm(true, false);
        var outcome = await RunInitAsync(new InitOptions());
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal("github-actions", outcome.Ci);
        var question = Prompts.SelectCalls[^1];
        Assert.Equal("Add a CI pipeline that runs the tests?", question.Message);
        Assert.Equal("github-actions", question.InitialValue);
        Assert.Equal(["github-actions", "gitlab", "none"], question.Options.Select(option => option.Value));
        Assert.Contains($"Create {Workflow}", Output, StringComparison.Ordinal);
        // No model was chosen, so the workflow reads no key.
        Assert.DoesNotContain("env:", Read(Workflow), StringComparison.Ordinal);

        var asked = Prompts.SelectCalls.Count;
        var second = await RunInitAsync(new InitOptions());
        Assert.Equal(InitResult.AlreadyInitialized, second.Result);
        Assert.Null(second.Ci);
        Assert.Equal(asked, Prompts.SelectCalls.Count);
    }

    [Fact]
    public async Task Preselects_none_outside_a_GitHub_repository()
    {
        Prompts.Engines = ["web"];
        Prompts.NextConfirm(false);
        await RunInitAsync(new InitOptions());
        Assert.Equal("none", Prompts.SelectCalls[^1].InitialValue);
    }

    [Fact]
    // Partial: the platform is the host's (iOS on macOS, Android elsewhere), and the job runs dotnet test.
    public async Task Partial_Writes_the_phone_job_alone_for_a_mobile_only_project_on_the_host()
    {
        var label = HostPlatform == "ios" ? "iOS" : "Android";
        var outcome = await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["mobile"],
                Ci = "github-actions",
            }
        );
        Assert.Equal("github-actions", outcome.Ci);
        var workflow = Read(Workflow);
        Assert.Equal(
            [HostPlatform],
            Regex.Matches(workflow, @"^  (web|ios|android):$", RegexOptions.Multiline).Select(m => m.Groups[1].Value)
        );
        Assert.Contains($"dotnet test -c Release --no-build {ProjectFile()}", workflow, StringComparison.Ordinal);
        // Not a project with a build, so the build is the user's to write.
        Assert.Contains($"The {label} job's build step is a placeholder that fails", Output, StringComparison.Ordinal);
        // app.command starts a web app; a phone job installs its build.
        Assert.DoesNotContain("Nothing starts the app in CI", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the scaffold writes one target, so the config with both comes from an existing one.
    public async Task Partial_Runs_the_web_and_phone_targets_in_their_own_jobs_when_the_config_has_both()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(
            Path_("e2e.config.json"),
            """{ "targets": [{ "platform": "web" }, { "platform": "ios" }] }"""
        );
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        var workflow = Read(Workflow);
        Assert.Equal(
            ["web", "ios"],
            Regex.Matches(workflow, @"^  (web|ios|android):$", RegexOptions.Multiline).Select(m => m.Groups[1].Value)
        );
        // Each job names the one target it runs.
        Assert.Contains("E2E_TARGET: web\n", workflow, StringComparison.Ordinal);
        Assert.Contains("E2E_TARGET: ios\n", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("runs several targets", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the host picks the phone platform, where upstream mocks it; the app is a package.json above the test project.
    public async Task Partial_Builds_an_Expo_app_in_the_phone_jobs()
    {
        await File.WriteAllTextAsync(Path_("package.json"), """{ "dependencies": { "expo": "*" } }""");
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["mobile"],
                Ci = "github-actions",
            }
        );
        var workflow = Read(Workflow);
        Assert.Contains(
            OperatingSystem.IsMacOS()
                ? "npx expo run:ios --configuration Release"
                : "npx expo prebuild --platform android --no-install",
            workflow,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("placeholder", Output, StringComparison.Ordinal);
    }

    [Fact]
    // .NET-only: upstream's --reporter list,junit is the junit logger, from the JunitXml.TestLogger package init adds.
    public async Task Writes_a_JUnit_report_with_the_junit_logger_and_adds_its_package()
    {
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.Contains(
            "--logger \"junit;LogFilePath=${{ github.workspace }}/.e2e/junit.xml\"",
            Read(Workflow),
            StringComparison.Ordinal
        );
        Assert.Contains("JunitXml.TestLogger", Read(ProjectFile()), StringComparison.Ordinal);
    }

    [Fact]
    // .NET-only: a TUnit project runs on Microsoft.Testing.Platform, which has no VSTest loggers.
    public async Task Writes_no_JUnit_report_for_a_TUnit_project()
    {
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Ci = "github-actions",
                Framework = "tunit",
            }
        );
        Assert.DoesNotContain("junit", Read(Workflow), StringComparison.Ordinal);
        Assert.DoesNotContain("JunitXml.TestLogger", Read(ProjectFile()), StringComparison.Ordinal);
        Assert.Contains("The pipeline writes no JUnit report", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the host's phone platform, and no E2E_DEVICE target setting to check beyond the variable.
    public async Task Partial_Tells_GitLab_users_the_phone_jobs_need_self_managed_runners_and_a_device_from_E2E_DEVICE()
    {
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["mobile"],
                Ci = "gitlab",
            }
        );
        var pipeline = Read(".gitlab-ci.yml");
        Assert.Contains(".e2e-mobile:", pipeline, StringComparison.Ordinal);
        Assert.Contains($"e2e-{HostPlatform}:", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("e2e-web:", pipeline, StringComparison.Ordinal);
        Assert.Contains(
            "GitLab's hosted runners can't run a simulator or an emulator",
            Output,
            StringComparison.Ordinal
        );
        Assert.Contains("name it in E2E_DEVICE", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the project lists E2E packages in the .csproj, and the scaffold asks nothing without an engine.
    public async Task Partial_Asks_nothing_for_a_new_config_with_no_engine_whatever_the_project_still_lists()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        Prompts.Engines = [];
        Prompts.NextConfirm(true, false);
        var outcome = await RunInitAsync(
            new InitOptions
            {
                Gateway = "none",
                Skills = [],
                Mcp = [],
            }
        );
        Assert.Equal(0, outcome.ExitCode);
        Assert.Null(outcome.Ci);
        Assert.DoesNotContain(Prompts.SelectCalls, call => call.Message == "Add a CI pipeline that runs the tests?");
    }

    [Fact]
    // Partial: the port has no peer dependencies; a PackageReference to E2E stands for the declared engine.
    public async Task Partial_Counts_an_engine_an_existing_project_declares()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(Path_("e2e.config.json"), """{ "targets": [{ "platform": "web" }] }""");
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.Contains(
            $"run: dotnet test -c Release --no-build {ProjectFile()}",
            Read(Workflow),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Writes_no_pipeline_for_a_project_with_neither_engine()
    {
        var outcome = await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = [],
                Ci = "github-actions",
            }
        );
        Assert.Equal(0, outcome.ExitCode);
        Assert.Null(outcome.Ci);
        Assert.False(Exists(".github"));
        Assert.Contains(
            "Ignoring --ci: init writes a pipeline for web, iOS, and Android targets, and the project has none",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: there is no .nvmrc to read, so the check is the folder in the project path and the artifact paths.
    public async Task Partial_Writes_the_workflow_at_the_repository_root_for_a_project_in_a_subfolder()
    {
        var project = Path_("e2e");
        Directory.CreateDirectory(Path_(".git"));
        Directory.CreateDirectory(project);
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" }, project);
        Assert.Equal(0, outcome.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(project, ".github")));
        var name = Init.ProjectNameOf(project);
        Assert.Contains($"dotnet build -c Release e2e/{name}.csproj", Read(Workflow), StringComparison.Ordinal);
        Assert.Contains(
            $"pwsh e2e/bin/Release/net10.0/playwright.ps1 install --with-deps chromium",
            Read(Workflow),
            StringComparison.Ordinal
        );
        Assert.Contains("Created ../.github/workflows/e2e.yml (GitHub Actions)", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_key_and_the_start_command_from_an_existing_config()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(
            Path_("e2e.config.json"),
            """
            {
              "agents": { "default": { "provider": "openrouter", "model": "openai/gpt-6-luna-fast" } },
              "targets": [{ "platform": "web", "app": { "url": "http://localhost:3100", "command": { "executable": "npm", "args": ["run", "dev"] } } }]
            }
            """
        );
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" })).ExitCode);
        Assert.Contains(
            "OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}",
            Read(Workflow),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("Nothing starts the app in CI", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_gitlab_ci_yml_with_ci_gitlab_and_names_the_CI_CD_variable_to_add()
    {
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Ci = "gitlab" });
        Assert.Equal("gitlab", outcome.Ci);
        Assert.Contains(
            $"    - dotnet test -c Release --no-build {ProjectFile()}",
            Read(".gitlab-ci.yml"),
            StringComparison.Ordinal
        );
        Assert.Contains("Created .gitlab-ci.yml (GitLab CI/CD)", Output, StringComparison.Ordinal);
        Assert.Contains(
            "glab variable set AI_GATEWAY_API_KEY --masked --hidden --raw",
            Output,
            StringComparison.Ordinal
        );
        // A second run finds the job in the pipeline and asks nothing more.
        Assert.Equal(
            InitResult.AlreadyInitialized,
            (await RunInitAsync(new InitOptions { Yes = true, Ci = "gitlab" })).Result
        );
        Assert.Contains("Exists, not touching: .gitlab-ci.yml", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_an_existing_gitlab_ci_yml_alone_and_writes_a_file_for_it_to_include()
    {
        await File.WriteAllTextAsync(Path_(".gitlab-ci.yml"), "build:\n  script: [make]\n");
        await RunInitAsync(new InitOptions { Yes = true, Ci = "gitlab" });
        Assert.Equal("build:\n  script: [make]\n", Read(".gitlab-ci.yml"));
        Assert.Matches(@"(?m)^e2e-web:\n", Read(".gitlab/e2e.gitlab-ci.yml"));
        Assert.Contains(
            ".gitlab-ci.yml exists, not touching it; add this to it to run the e2e jobs:",
            Output,
            StringComparison.Ordinal
        );
        Assert.Matches(@"include:\n\S*\s+- local: \.gitlab/e2e\.gitlab-ci\.yml", Output);
    }

    [Fact]
    public async Task Preselects_GitLab_in_a_repository_with_a_GitLab_remote()
    {
        Prompts.Engines = ["web"];
        Directory.CreateDirectory(Path_(".git"));
        await File.WriteAllTextAsync(
            Path_(".git/config"),
            "[remote \"origin\"]\n\turl = https://gitlab.com/acme/app.git\n"
        );
        Prompts.NextConfirm(false);
        await RunInitAsync(new InitOptions());
        Assert.Equal("gitlab", Prompts.SelectCalls[^1].InitialValue);
    }

    [Fact]
    public async Task Reads_the_target_names_and_platforms_from_an_existing_config()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(
            Path_("e2e.config.json"),
            """{ "targets": [{ "name": "chromium", "platform": "web" }, { "name": "iphone", "platform": "ios" }, { "name": "pixel", "platform": "android" }] }"""
        );
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        var workflow = Read(Workflow);
        Assert.Equal(
            ["web", "ios", "android"],
            Regex.Matches(workflow, @"^  (web|ios|android):$", RegexOptions.Multiline).Select(m => m.Groups[1].Value)
        );
    }

    [Fact]
    // Partial: the port reads its JSON config without running it, so the config that fails is one that is not JSON.
    public async Task Partial_Runs_the_tests_in_one_web_job_when_an_existing_web_config_does_not_read_and_says_why()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{ not json");
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.Equal(
            ["web"],
            Regex
                .Matches(Read(Workflow), @"^  (web|ios|android):$", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
        );
        Assert.Contains("The pipeline runs the tests in one web job", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("No web, iOS, or Android target", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writes_no_pipeline_when_an_existing_mobile_config_does_not_read()
    {
        await File.WriteAllTextAsync(
            Path_(ProjectFile()),
            EngineProject.Replace("E2E.NUnit", "E2E.Mobile.NUnit", StringComparison.Ordinal)
        );
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{ not json");
        await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" });
        Assert.False(Exists(Workflow));
        Assert.Contains("No pipeline: the mobile jobs need the config's targets", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Warns_that_a_model_served_on_this_machine_is_out_of_the_runners_reach()
    {
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["web"],
                Gateway = "openai-compatible",
                Ci = "gitlab",
            }
        );
        Assert.Contains(
            "The model endpoint is on this machine, so agent steps fail in CI",
            Output,
            StringComparison.Ordinal
        );
        File.Delete(Path_("e2e.config.json"));
        File.Delete(Path_(".gitlab-ci.yml"));
        ClearOutput();
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["web"],
                Gateway = "openai-compatible",
                Endpoint = "https://llm.example/v1",
                Ci = "gitlab",
            }
        );
        Assert.DoesNotContain("The model endpoint is on this machine", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_loopback_endpoint_from_an_existing_config_too()
    {
        await File.WriteAllTextAsync(Path_(ProjectFile()), EngineProject);
        await File.WriteAllTextAsync(
            Path_("e2e.config.json"),
            """
            {
              "agents": { "default": { "provider": "openai-compatible", "model": "gpt-6-luna", "baseUrl": "http://127.0.0.1:11434/v1" } },
              "targets": [{ "platform": "web" }]
            }
            """
        );
        await RunInitAsync(new InitOptions { Yes = true, Ci = "gitlab" });
        Assert.Contains(
            "The model endpoint is on this machine, so agent steps fail in CI",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Writes_nothing_through_a_symlinked_github_folder()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            Link(Path_(".github"), elsewhere, directory: true);
            Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" })).ExitCode);
            Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
            Assert.Contains(
                $"Symlink, not touching: .github -> {Real(elsewhere)} (.github/workflows/e2e.yml)",
                Output,
                StringComparison.Ordinal
            );
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Warns_that_a_subscription_gateway_has_no_key_to_give_CI()
    {
        await RunInitAsync(
            new InitOptions
            {
                Yes = true,
                Engines = ["web"],
                Gateway = "chatgpt",
                Ci = "github-actions",
            }
        );
        Assert.DoesNotContain("env:", Read(Workflow), StringComparison.Ordinal);
        Assert.Contains(
            "ChatGPT Plus/Pro subscription signs in on this machine only, so agent steps fail in CI",
            Output,
            StringComparison.Ordinal
        );
    }
}
