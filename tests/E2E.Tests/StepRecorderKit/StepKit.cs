// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Progress = global::E2E.StepProgress;

namespace E2E.Tests.StepRecorderKit;

/// <summary>A gate for controlling completion order without relying on timers (the <c>gate()</c> and <c>deferred()</c> helpers of the step tests).</summary>
internal sealed class Gate
{
    private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Task => _source.Task;

    public void Open() => _source.TrySetResult();

    public void Fail(Exception cause) => _source.TrySetException(cause);
}

internal static class StepKit
{
    /// <summary>
    /// A recorder and what it published, as the step tests build one. The project root is the directory above the one holding the test
    /// file, the way upstream's tests use their <c>tests</c> directory as a stand-in for a project root: the runner's own frames lie
    /// outside it, as they do in a real project.
    /// </summary>
    public static (StepRecorder Steps, List<Progress> Heard) Recorder([CallerFilePath] string testFile = "")
    {
        var heard = new List<Progress>();
        var steps = new StepRecorder("attempt", new StepRecorderOptions { ProjectRoot = Path.GetDirectoryName(Path.GetDirectoryName(testFile)), OnProgress = heard.Add });
        return (steps, heard);
    }

    /// <summary>A recorder that publishes into a list, with no project root.</summary>
    public static (StepRecorder Steps, List<Progress> Heard) UnrootedRecorder()
    {
        var heard = new List<Progress>();
        return (new StepRecorder("attempt", new StepRecorderOptions { OnProgress = heard.Add }), heard);
    }
}
