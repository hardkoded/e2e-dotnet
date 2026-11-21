// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.ChromiumSandbox;

/// <summary><see cref="BrowserConnection.DefaultChromiumSandbox"/>: on where the sandbox starts out of the box, off on Linux.</summary>
public sealed class DefaultChromiumSandboxTests
{
    [Fact]
    public void Is_on_where_the_sandbox_starts_out_of_the_box_and_off_on_Linux()
    {
        Assert.True(BrowserConnection.DefaultChromiumSandbox(System.Runtime.InteropServices.OSPlatform.OSX));
        Assert.True(BrowserConnection.DefaultChromiumSandbox(System.Runtime.InteropServices.OSPlatform.Windows));
        Assert.False(BrowserConnection.DefaultChromiumSandbox(System.Runtime.InteropServices.OSPlatform.Linux));
    }
}
