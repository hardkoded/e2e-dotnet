// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli;

namespace E2E.Cli.Tests.InitCi;

/// <summary>
/// Upstream's <c>describe('project detection')</c>. The port has no Node version file, so "reads the Node major from %j" and
/// "reads the Node version file nearest the project, never above the repository" are not ported; the SDK version of the
/// project file is read instead, by <see cref="Partial_Reads_the_SDK_version_from_the_target_framework"/>.
/// </summary>
public sealed class ProjectDetectionTests : InitCiTestBase
{
    private const string StageNote = "The e2e jobs run in the test stage; add test to the stages in .gitlab-ci.yml";

    private static CiService Gitlab => Ci.GetCiService("gitlab");

    private static CiService Github => Ci.GetCiService("github-actions");

    [Fact]
    public void Finds_the_repository_above_the_project_or_falls_back_to_the_init_directory()
    {
        var project = Path_("apps/web");
        Directory.CreateDirectory(project);
        Assert.Equal(project, Ci.FindRepositoryRoot(project, project));
        Directory.CreateDirectory(Path_(".git"));
        Assert.Equal(Dir, Ci.FindRepositoryRoot(project, project));
        // A worktree or submodule has a .git file.
        Write("apps/.git", "gitdir: elsewhere\n");
        Assert.Equal(Path_("apps"), Ci.FindRepositoryRoot(project, project));
    }

    [Fact]
    public void Tells_GitHub_from_GitLab_by_the_remote_a_github_folder_or_a_gitlab_ci_yml()
    {
        Directory.CreateDirectory(Path_(".git"));
        Assert.Null(Ci.DetectCiService(Dir));
        Write(".git/config", "[remote \"origin\"]\n\turl = git@gitlab.com:acme/app.git\n");
        Assert.Equal("gitlab", Ci.DetectCiService(Dir));
        Write(".git/config", "[remote \"origin\"]\n\turl = https://github.com/acme/app.git\n");
        Assert.Equal("github-actions", Ci.DetectCiService(Dir));
        Directory.Delete(Path_(".git"), recursive: true);
        Directory.CreateDirectory(Path_(".github"));
        Assert.Equal("github-actions", Ci.DetectCiService(Dir));
        // A GitLab pipeline file wins over a .github folder that holds only templates.
        Write(".gitlab-ci.yml", "build:\n  script: [make]\n");
        Assert.Equal("gitlab", Ci.DetectCiService(Dir));
    }

    [Fact]
    // Partial: the pipeline that runs the tests runs dotnet test, where upstream's runs npx e2e run.
    public void Partial_Writes_gitlab_ci_yml_or_a_file_beside_an_existing_one_and_counts_a_pipeline_that_runs_the_tests_as_set_up()
    {
        Assert.Equal(new CiFile(".gitlab-ci.yml", false), Gitlab.Locate(Dir));
        Write(".gitlab-ci.yml", "build:\n  script: [make]\n");
        Assert.Equal(
            new CiFile(".gitlab/e2e.gitlab-ci.yml", false, "include:\n  - local: .gitlab/e2e.gitlab-ci.yml"),
            Gitlab.Locate(Dir)
        );
        Write(".gitlab-ci.yml", "e2e:\n  script: [dotnet test]\n");
        Assert.Equal(new CiFile(".gitlab-ci.yml", true), Gitlab.Locate(Dir));
    }

    [Fact]
    public void Says_when_an_existing_pipeline_lists_stages_without_test_where_the_included_job_runs()
    {
        Write(".gitlab-ci.yml", "stages: [build, deploy]\nbuild:\n  script: [make]\n");
        Assert.Equal(StageNote, Gitlab.Locate(Dir).Note);
        Write(".gitlab-ci.yml", "stages:\n  - build\n  - test\nbuild:\n  script: [make]\n");
        Assert.Null(Gitlab.Locate(Dir).Note);
        Write(".gitlab-ci.yml", "build:\n  script: [make]\n");
        Assert.Null(Gitlab.Locate(Dir).Note);
        // A stage whose name only contains test is another stage.
        Write(".gitlab-ci.yml", "stages: [build, unit-test]\n");
        Assert.Equal(StageNote, Gitlab.Locate(Dir).Note);
        Write(".gitlab-ci.yml", "stages:\n  - 'build'\n  - \"test\" # unit and e2e\nvariables: {}\n");
        Assert.Null(Gitlab.Locate(Dir).Note);
    }

    [Theory]
    [InlineData("items at the indentation of the key", "stages:\n- build\n- test\n")]
    [InlineData("a comment between the items", "stages:\n  - build\n  # checks\n  - test\n")]
    [InlineData("a blank line between the items", "stages:\n  - build\n\n  - test\n")]
    [InlineData("a flow list over several lines", "stages: [\n  build,\n  test\n]\n")]
    [InlineData("a spec header above it", "spec:\n  inputs: {}\n---\nstages:\n  - build\n  - test\n")]
    [InlineData("an alias to an anchored list", ".stages: &stages\n  - build\n  - test\nstages: *stages\n")]
    public void Reads_the_test_stage_from_a_stages_list_written_with(string layout, string pipeline)
    {
        Assert.NotEmpty(layout);
        Write(".gitlab-ci.yml", pipeline + "build:\n  stage: build\n  script: [make]\n");
        Assert.Null(Gitlab.Locate(Dir).Note);
        Write(
            ".gitlab-ci.yml",
            pipeline.Replace("test", "deploy", StringComparison.Ordinal) + "build:\n  stage: build\n  script: [make]\n"
        );
        Assert.Equal(StageNote, Gitlab.Locate(Dir).Note);
    }

    [Fact]
    // Partial: the existing jobs are named like the port's, and the cypress and detox commands are not dotnet's.
    public void Partial_Renames_the_jobs_beside_a_pipeline_that_already_has_a_job_of_one_of_their_names()
    {
        Write(".gitlab-ci.yml", "e2e:\n  script: [npx cypress run]\n");
        Assert.Null(Gitlab.Locate(Dir).JobSuffix);
        Write(".gitlab-ci.yml", "e2e-ios:\n  script: [npx detox test]\n");
        var file = Gitlab.Locate(Dir);
        Assert.Equal(".gitlab/e2e.gitlab-ci.yml", file.File);
        Assert.Equal("-run", file.JobSuffix);
    }

    [Fact]
    public void Partial_Counts_any_GitHub_workflow_that_runs_the_tests_as_set_up()
    {
        Assert.Equal(new CiFile(".github/workflows/e2e.yml", false), Github.Locate(Dir));
        Write(".github/workflows/lint.yml", "jobs: {}\n");
        Assert.False(Github.Locate(Dir).Existing);
        Write(".github/workflows/ci.yaml", "jobs:\n  test:\n    steps:\n      - run: dotnet test\n");
        Assert.Equal(new CiFile(".github/workflows/ci.yaml", true), Github.Locate(Dir));
    }

    [Theory]
    [InlineData("      - run: dotnet test --filter Category=E2E\n")]
    [InlineData("      - run: |\n          adb install -r app.apk\n          (cd site && dotnet test)\n")]
    [InlineData(
        "      - uses: reactivecircus/android-emulator-runner@v2\n        with:\n          script: dotnet test -c Release\n"
    )]
    public void Partial_Counts_a_GitHub_workflow_with_the_command_as_set_up(string steps)
    {
        Write(".github/workflows/ci.yml", "jobs:\n  test:\n    steps:\n" + steps);
        Assert.Equal(new CiFile(".github/workflows/ci.yml", true), Github.Locate(Dir));
    }

    [Theory]
    [InlineData("      - run: dotnet build # our old dotnet test runner\n")]
    [InlineData("      # replaces dotnet test\n      - run: npx playwright test\n")]
    [InlineData("      - name: Cypress dotnet test\n        run: npx cypress run\n")]
    [InlineData("      - run: |\n          # dotnet test\n          npm run test:dotnet test\n")]
    [InlineData("      - run: dotnet testing\n")]
    public void Partial_Writes_its_own_workflow_beside_one_that_only_mentions_the_command(string steps)
    {
        Write(".github/workflows/ci.yml", "jobs:\n  test:\n    steps:\n" + steps);
        Assert.Equal(new CiFile(".github/workflows/e2e.yml", false), Github.Locate(Dir));
    }

    [Fact]
    public void Partial_Counts_a_GitLab_pipeline_as_set_up_by_its_scripts_not_by_its_comments_or_job_names()
    {
        Write(
            ".gitlab-ci.yml",
            "dotnet test:\n  # dotnet test\n  script:\n    - npx cypress run # the old dotnet test runner\n"
        );
        var other = Gitlab.Locate(Dir);
        Assert.Equal(".gitlab/e2e.gitlab-ci.yml", other.File);
        Assert.False(other.Existing);
        Write(
            ".gitlab-ci.yml",
            "test:\n  before_script:\n    - dotnet restore\n  script:\n    - dotnet test -c Release\n"
        );
        Assert.Equal(new CiFile(".gitlab-ci.yml", true), Gitlab.Locate(Dir));
    }

    [Theory]
    [InlineData("<TargetFramework>net10.0</TargetFramework>", "10.0")]
    [InlineData("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", "8.0")]
    [InlineData("<TargetFramework>net11.0</TargetFramework>", "11.0")]
    [InlineData("<TargetFramework>netstandard2.0</TargetFramework>", "10.0")]
    public void Partial_Reads_the_SDK_version_from_the_target_framework(string project, string version)
    {
        Assert.Equal(version, Ci.DotnetVersionOf(project));
    }
}
