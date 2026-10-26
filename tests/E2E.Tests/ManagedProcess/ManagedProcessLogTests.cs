// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

public sealed class ManagedProcessLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-process-log-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ManagedProcessUnderTest Step(string log) =>
        new("app.command", AppProgram.Command(["chatter", "0"], log: log), _dir, Readiness.WaitForExit);

    [Fact]
    public async Task Captures_stdout_and_stderr_into_the_log_file_resolved_from_the_project_root()
    {
        await Step(Path.Combine("out", "app.log")).StartAsync();
        var content = await File.ReadAllTextAsync(Path.Combine(_dir, "out", "app.log"));
        Assert.Contains("to stdout" + Environment.NewLine, content, StringComparison.Ordinal);
        Assert.Contains("to stderr" + Environment.NewLine, content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Appends_across_runs_instead_of_truncating()
    {
        await Step("app.log").StartAsync();
        await Step("app.log").StartAsync();
        var lines = (await File.ReadAllLinesAsync(Path.Combine(_dir, "app.log")));
        Assert.Equal(2, lines.Count(line => line == "to stdout"));
        Assert.Equal(2, lines.Count(line => line == "to stderr"));
    }

    [Fact]
    public async Task Creates_missing_parent_directories()
    {
        await Step(Path.Combine(".e2e", "logs", "nested", "app.log")).StartAsync();
        Assert.True(File.Exists(Path.Combine(_dir, ".e2e", "logs", "nested", "app.log")));
    }

    [Fact]
    public async Task Fails_with_APP_UNREACHABLE_when_the_log_path_cannot_be_opened()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "app.log"));
        var failure = await Assert.ThrowsAsync<EngineException>(() => Step("app.log").StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Contains("could not open its log file", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discards_output_when_log_is_unset()
    {
        await new ManagedProcessUnderTest("app.command", AppProgram.Command(["chatter", "0"]), _dir, Readiness.WaitForExit).StartAsync();
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }
}
