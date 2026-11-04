// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;

namespace E2E.Cli.Tests.CliInit;

/// <summary>.NET-only: upstream has one runner. The package versions a new project pins must be the ones the samples build and test with.</summary>
public sealed class FrameworkPresetsTests
{
    private static string SampleProject(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var project = Path.Combine(directory.FullName, "samples", name, name + ".csproj");
            if (File.Exists(project))
            {
                return project;
            }
        }

        throw new InvalidOperationException("sample " + name + " was not found above " + AppContext.BaseDirectory);
    }

    [Theory]
    [InlineData("nunit", "E2E.Sample")]
    [InlineData("xunit", "E2E.XUnit.V3.Sample")]
    public void Pin_the_package_versions_of_the_matching_sample(string id, string sample)
    {
        var pinned = XDocument.Load(SampleProject(sample)).Descendants("PackageReference")
            .ToDictionary(reference => (string)reference.Attribute("Include")!, reference => (string)reference.Attribute("Version")!);
        Assert.All(Frameworks.Get(id).Dependencies, dependency => Assert.Equal(pinned[dependency.Key], dependency.Value));
    }

    [Fact]
    public void Detect_the_framework_from_the_packages_a_project_names()
    {
        Assert.Equal("nunit", Frameworks.Detect(["Microsoft.NET.Test.Sdk", "NUnit3TestAdapter"])?.Id);
        Assert.Equal("xunit", Frameworks.Detect(["xunit.v3.mtp-off"])?.Id);
        Assert.Null(Frameworks.Detect(["Microsoft.NET.Test.Sdk", "E2E"]));
    }
}
