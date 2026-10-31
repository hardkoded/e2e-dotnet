// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.Reporters;

[assembly: E2EReporter(typeof(RegisteredReporter))]

namespace E2E.Tests.Reporters;

/// <summary>.NET-only: <c>[assembly: E2EReporter]</c> is how a test assembly registers a reporter, where upstream lists reporter objects in its config.</summary>
public sealed class E2EReporterAttributeTests
{
    [Fact]
    public void Builds_the_reporters_the_assembly_registers()
    {
        var reporters = E2E.Reporters.Discover(typeof(RegisteredReporter).Assembly);

        Assert.Contains(reporters, reporter => reporter is RegisteredReporter { Name: "registered" });
    }
}

/// <summary>A reporter the test assembly registers.</summary>
public sealed class RegisteredReporter : Reporter
{
    public override string Name => "registered";
}
