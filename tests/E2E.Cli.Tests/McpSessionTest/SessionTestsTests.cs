// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Cli.Mcp;

namespace E2E.Cli.Tests.McpSessionTest;

/// <summary>
/// .NET-only: upstream's <c>mcp-run-test.test.ts</c> runs a test through <c>open_session {test}</c> with its own runner. These check how
/// the tool finds the test framework's runner in the configured <c>testAssembly</c>. Running a test is covered by
/// <c>SessionTestRunnerTests</c> in E2E.NUnit.Tests. <c>SelectOne</c> is the host half of upstream's <c>selectStandaloneTest</c> refusals.
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

    [Fact]
    public void Refuses_a_name_that_selects_no_test()
    {
        var error = Assert.Throws<ConfigurationException>(() => SessionTests.SelectOne(new FakeRunner(), typeof(SessionTestsTests).Assembly, "Shop.Nothing", "tests.dll"));

        Assert.Equal("NO_TESTS", error.Code);
        Assert.Equal("no test named \"Shop.Nothing\" in tests.dll; use the full name, Namespace.Class.Method, or file:line", error.Message);
    }

    [Fact]
    public void Refuses_a_name_that_selects_several_tests_and_names_the_first_ten()
    {
        var runner = new FakeRunner(Enumerable.Range(1, 12).Select(number => new SessionTestInfo("Shop.Others.Test" + number, null)).ToArray());

        var error = Assert.Throws<ConfigurationException>(() => SessionTests.SelectOne(runner, typeof(SessionTestsTests).Assembly, "Shop.Others", "tests.dll"));

        Assert.Equal("TEST_AMBIGUOUS", error.Code);
        Assert.Equal(
            "\"Shop.Others\" selects 12 tests, and a session runs one; name it by its full name: "
            + string.Join(", ", Enumerable.Range(1, 10).Select(number => "\"Shop.Others.Test" + number + "\"")) + ", and 2 more",
            error.Message);
    }

    [Fact]
    public void Refuses_a_test_a_session_cannot_run_alone()
    {
        var runner = new FakeRunner(new SessionTestInfo("Shop.Others.SignedIn", "test \"Shop.Others.SignedIn\" starts from session \"member\""));

        var error = Assert.Throws<ConfigurationException>(() => SessionTests.SelectOne(runner, typeof(SessionTestsTests).Assembly, "Shop.Others.SignedIn", "tests.dll"));

        Assert.Equal("SESSION_TEST_UNSUPPORTED", error.Code);
        Assert.StartsWith("test \"Shop.Others.SignedIn\" starts from session \"member\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_one_test()
    {
        SessionTests.SelectOne(new FakeRunner(new SessionTestInfo("Shop.Others.First", null)), typeof(SessionTestsTests).Assembly, "Shop.Others.First", "tests.dll");
    }

    private sealed class FakeRunner(params SessionTestInfo[] selected) : ISessionTestRunner
    {
        public IReadOnlyList<SessionTestInfo> Select(Assembly tests, string name) => selected;

        public IReadOnlyList<SessionTestInfo> List(Assembly tests) => selected;

        public Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken, E2EConfig? host = null) => throw new NotSupportedException();
    }
}
