// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/process.rs</c>.</summary>
public sealed class ProcessTests
{
    /// <summary>A <c>KERN_PROCARGS2</c> buffer as the kernel lays it out.</summary>
    private static byte[] Buffer(string path, string[] argv, string[] environment)
    {
        var output = new List<byte>(BitConverter.GetBytes(argv.Length));
        output.AddRange(Encoding.UTF8.GetBytes(path));
        output.AddRange(new byte[] { 0, 0, 0, 0 });
        foreach (var entry in argv.Concat(environment))
        {
            output.AddRange(Encoding.UTF8.GetBytes(entry));
            output.Add(0);
        }

        output.AddRange(new byte[] { 0, 0 });
        return [.. output];
    }

    [Fact]
    public void Keeps_an_empty_argument_and_the_environment_after_it()
    {
        var parsed = ProcessInfo.ParseProcArgs(
            Buffer("/bin/app", ["/bin/app", "-Greeting", "", "x"], ["E2E_DESKTOP_LAUNCH=tok", "HOME=/u"])
        );
        var (arguments, environment) = parsed!.Value;
        Assert.Equal(["-Greeting", "", "x"], arguments);
        Assert.Equal(["E2E_DESKTOP_LAUNCH=tok", "HOME=/u"], environment);
    }

    [Fact]
    public void Reads_a_hidden_environment_as_none_and_keeps_the_arguments()
    {
        var (arguments, environment) = ProcessInfo
            .ParseProcArgs(Buffer("/System/App", ["/System/App", "-E2EDesktopLaunch=tok"], []))!
            .Value;
        Assert.Equal(["-E2EDesktopLaunch=tok"], arguments);
        Assert.Null(environment);
    }

    [Fact]
    public void Reads_an_environment_that_holds_only_the_launch_token()
    {
        var (_, environment) = ProcessInfo
            .ParseProcArgs(Buffer("/bin/helper", ["/bin/helper"], ["E2E_DESKTOP_LAUNCH=tok"]))!
            .Value;
        Assert.Equal(["E2E_DESKTOP_LAUNCH=tok"], environment);
    }

    [Fact]
    public void Refuses_a_buffer_cut_short()
    {
        Assert.Null(ProcessInfo.ParseProcArgs([1, 0]));
        var cut = Buffer("/bin/app", ["/bin/app", "a", "b"], []);
        Assert.Null(ProcessInfo.ParseProcArgs(cut[..14]));
    }
}
