// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Acp.Tests;

/// <summary>
/// Tests that change the process's working directory or its environment variables run one at a time, since both are shared by the whole
/// process. .NET-only test infrastructure: a Vitest file runs in its own worker, so upstream's tests never meet this.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkingDirectoryCollection
{
    public const string Name = "Working directory";
}
