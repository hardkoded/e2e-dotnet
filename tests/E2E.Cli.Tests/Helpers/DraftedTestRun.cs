// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Security;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// Upstream's <c>runProject</c> for a pasted draft: a test class built from the lines of <c>test_lines</c> in a project of its own
/// (beside the project's <c>e2e.config.json</c>), run with <c>dotnet test</c>.
/// </summary>
internal static class DraftedTestRun
{
    /// <summary>Builds and runs one NUnit test whose body is <paramref name="body"/>, then <paramref name="check"/>. The result holds the exit code and the output.</summary>
    public static async Task<(int Code, string Output)> RunAsync(string projectDirectory, IEnumerable<string> body, string check)
    {
        var repo = RepoRoot();
        // The draft sits outside the repository, so it takes the repository's SDK pin along: another SDK would rebuild src/ with its own outputs.
        File.Copy(Path.Combine(repo, "global.json"), Path.Combine(projectDirectory, "global.json"), overwrite: true);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Draft.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="NUnit" Version="5.0.0" />
                <PackageReference Include="NUnit3TestAdapter" Version="6.3.0" />
              </ItemGroup>
              <ItemGroup>
                <Using Include="NUnit.Framework" />
                <ProjectReference Include="{SecurityElement.Escape(Path.Combine(repo, "src", "E2E", "E2E.csproj"))}" />
                <ProjectReference Include="{SecurityElement.Escape(Path.Combine(repo, "src", "E2E.NUnit", "E2E.NUnit.csproj"))}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "DraftTests.cs"),
            $$"""
            using E2E;
            using E2E.NUnit;

            public sealed class DraftTests : E2ETest
            {
                [Test]
                public async Task Replays_the_drafted_lines()
                {
            {{string.Join('\n', body.Append(check).Select(line => "        " + line))}}
                }
            }
            """);

        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = projectDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--nologo");
        // The configuration this test runs in, so the referenced projects' built outputs are reused instead of built again.
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(typeof(DraftedTestRun).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug");
        start.ArgumentList.Add("-nodeReuse:false");
        start.Environment["UseSharedCompilation"] = "false";
        // Upstream's fixtureEnv: the run is the caller's environment, outside CI.
        start.Environment["CI"] = "";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var limit = TimeSpan.FromMinutes(5);
        using var timeout = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            // A build server or a browser the test host left behind keeps the pipes open after dotnet test exits, so the reads are bounded too.
            await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // A killed build must not leave its servers holding the pipes and the project directory.
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet test in {projectDirectory} did not finish in {limit.TotalMinutes} min");
        }

        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "E2E.slnx")))
        {
            root = root.Parent ?? throw new InvalidOperationException("no E2E.slnx above " + AppContext.BaseDirectory);
        }

        return root.FullName;
    }
}
