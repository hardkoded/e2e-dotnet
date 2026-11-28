// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

// Every test shares one temporary directory (an environment variable is process-wide), and `init` sweeps it, so the tests run one at a time.
// Upstream's vitest runs the tests of a file one at a time, each file in a process of its own with its own TMPDIR.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace E2E.Desktop.Tests;

/// <summary>
/// Each test run gets its own temporary directory. <c>init</c> sweeps every <c>e2e-desktop-worker-*</c> directory under the temporary directory (and
/// every <c>e2e-agent-desktop-*</c> one the agent-desktop engine left), and kills the processes their launch records name: in a shared one, a run
/// of the suite in another checkout would sweep the directories a test plants and expects to find, and a developer's own leftovers from a real
/// desktop run would be swept by the tests. Port of upstream's <c>tests/helpers/private-tmpdir.ts</c>, which sets <c>TMPDIR</c> for each test file.
/// </summary>
internal static class PrivateTmpDir
{
    [ModuleInitializer]
    internal static void Use()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-desktop-tests-").FullName;
        Environment.SetEnvironmentVariable("TMPDIR", directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // The next run's temporary directory is its own.
            }
        };
    }
}
