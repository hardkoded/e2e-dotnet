// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitExamples;

/// <summary>Upstream's <c>describe('example presets')</c> of <c>tests/unit/init-examples.test.ts</c>.</summary>
public sealed class ExamplePresetsTests
{
    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "samples")) && File.Exists(Path.Combine(directory.FullName, "E2E.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("the repository root was not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    // Partial: the port keeps a list of the samples that stand alone (the others need a device or a build), so it checks each listed one exists with a project and a config, not that the list covers every folder; and a sample has no e2e project folder, test script, or build step.
    public void Partial_List_every_standalone_app_in_samples_each_with_its_e2e_project_and_the_script_the_Next_line_names()
    {
        var samples = Path.Combine(RepoRoot(), "samples");
        Assert.Equal(Examples.All.Count, Examples.All.Select(example => example.Id).Distinct().Count());
        foreach (var example in Examples.All)
        {
            var project = Path.Combine(samples, example.Id);
            Assert.True(File.Exists(Path.Combine(project, E2EConfig.FileName)), example.Id);
            Assert.Single(Directory.GetFiles(project, "*.csproj"));
        }
    }

    [Fact]
    // Partial: the id is the folder name in any case; there is no with- prefix to leave out.
    public void Accept_the_folder_name_with_or_without_with()
    {
        Assert.Equal("TodoMvc", Examples.FindExample("todomvc")?.Id);
        Assert.Equal("E2E.XUnit.V3.Sample", Examples.FindExample("E2E.XUnit.V3.Sample")?.Id);
        Assert.Null(Examples.FindExample("expo"));
    }
}
