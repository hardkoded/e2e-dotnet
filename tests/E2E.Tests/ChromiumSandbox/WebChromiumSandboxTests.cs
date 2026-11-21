// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ChromiumSandbox;

/// <summary>The <c>ChromiumSandbox</c> option of <see cref="WebEngineOptions"/>. Upstream's "rejects a value that is not a boolean as INVALID_CONFIG" has no port: the option is a <c>bool?</c>, so the compiler refuses one.</summary>
public sealed class WebChromiumSandboxTests
{
    [Fact]
    public void Accepts_true_and_false()
    {
        _ = new WebEngine(new WebEngineOptions { ChromiumSandbox = true });
        _ = new WebEngine(new WebEngineOptions { ChromiumSandbox = false });
    }
}
