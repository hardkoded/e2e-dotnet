// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine.Protocol;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// .NET-only: upstream has no <c>it</c> for the PATH search of <c>spawnTransport</c>, where Node's <c>spawn</c> lets the kernel skip an entry the
/// current user cannot execute. The port asks <c>access(X_OK)</c> for the same answer.
/// </summary>
public sealed class SpawnTransportPathTests
{
    [Fact]
    public void Skips_a_PATH_entry_the_current_user_cannot_execute()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            // Root may run any file with an execute bit, so no mode separates it from the owner.
            return;
        }

        var root = Directory.CreateTempSubdirectory("e2e-spawn-path-").FullName;
        try
        {
            var first = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            var second = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
            // Only the other-execute bit: the owner is the current user, and the kernel refuses it.
            File.WriteAllText(Path.Combine(first, "tool"), "#!/bin/sh\n");
            File.SetUnixFileMode(
                Path.Combine(first, "tool"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherExecute
            );
            File.WriteAllText(Path.Combine(second, "tool"), "#!/bin/sh\n");
            File.SetUnixFileMode(
                Path.Combine(second, "tool"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );

            var resolved = SpawnTransport.ResolveCommand(
                "tool",
                root,
                new Dictionary<string, string?> { ["PATH"] = first + Path.PathSeparator + second }
            );

            Assert.Equal(Path.Combine(second, "tool"), resolved);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
