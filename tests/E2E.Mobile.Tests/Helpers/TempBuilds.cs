// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.Tests;

/// <summary>
/// A project root on disk for the tests that install a build. The harness's own root, <c>/project</c>, is not on disk
/// (upstream's engine.test.ts mocks <c>existsSync</c> for it), so these tests boot the harness with this directory.
/// </summary>
internal sealed class TempBuilds : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("e2e-builds").FullName;

    /// <summary>A build under the root, at a path relative to it.</summary>
    public string Create(string relative)
    {
        Directory.CreateDirectory(Path.Combine(Root, relative));
        return relative;
    }

    public string Full(string relative) => Path.Combine(Root, relative);

    public void Dispose() => Directory.Delete(Root, true);
}
