// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;

namespace E2E.Cli.Tests.McpSessionTest;

/// <summary>
/// .NET-only: upstream's <c>mcp-run-test.test.ts</c> runs a test through <c>open_session {test}</c> with its own runner. These check how
/// the tool finds the test framework's runner in the configured <c>testAssembly</c>. Running a test is covered by
/// <c>SessionTestRunnerTests</c> in E2E.NUnit.Tests.
/// </summary>
public sealed class SessionTestsTests
{
    [Fact]
    public void Refuses_a_test_assembly_that_does_not_exist()
    {
        var error = Assert.Throws<ConfigurationException>(() => SessionTests.Load(Path.Combine(Path.GetTempPath(), "no-such-tests.dll")));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("build the test project first", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_test_assembly_whose_framework_package_cannot_run_a_session_test()
    {
        var error = Assert.Throws<ConfigurationException>(() => SessionTests.Load(typeof(SessionTestsTests).Assembly.Location));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("E2E.NUnit does", error.Message, StringComparison.Ordinal);
    }
}
