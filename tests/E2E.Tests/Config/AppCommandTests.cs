// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary><c>describe('app.command')</c> of <c>config.test.ts</c>.</summary>
public sealed class AppCommandTests
{
    private const string AppUrl = "http://localhost:3000";

    [Fact]
    public void Accepts_reuseExisting_on_a_fixed_port_and_checks_its_shape()
    {
        Assert.True(ResolveConfigTests.Resolved($$"""{ "url": "{{AppUrl}}", "command": { "executable": "pnpm", "args": ["dev"], "reuseExisting": true } }""").Command!.ReuseExisting);
        var error = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App($$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "reuseExisting": "yes" } }"""));
        Assert.Contains("target \"web\" app.command.reuseExisting must be a boolean", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: JSON has no NaN or infinity, and a number over what a .NET timer waits is refused with a message of its own.</summary>
    [Fact]
    public void Partial_Rejects_non_positive_integer_timeouts()
    {
        foreach (var value in new[] { "0", "-1", "1.5", "\"1000\"", "true" })
        {
            var startup = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App($$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "startupTimeout": {{value}} } }"""));
            Assert.Contains("app.command.startupTimeout must be a positive safe integer", startup.Message, StringComparison.Ordinal);
            var shutdown = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App($$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "shutdownTimeout": {{value}} } }"""));
            Assert.Contains("app.command.shutdownTimeout must be a positive safe integer", shutdown.Message, StringComparison.Ordinal);
        }

        var command = ResolveConfigTests.Resolved($$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "startupTimeout": 5000, "shutdownTimeout": 500 } }""").Command!;
        Assert.Equal(TimeSpan.FromMilliseconds(5000), command.StartupTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(500), command.ShutdownTimeout);
        var tooLong = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App($$"""{ "url": "{{AppUrl}}", "command": { "executable": "x", "startupTimeout": 9007199254740991 } }"""));
        Assert.Contains("must be at most 2147483647 milliseconds", tooLong.Message, StringComparison.Ordinal);
    }
}
