// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Abstractions;
using Xunit.Sdk;

namespace E2E.Tests.Crosscheck;

/// <summary>
/// Runs the test that compares the recorded disagreements with <c>expected.txt</c> after the tests that record them. .NET-only:
/// vitest runs a file's tests in order, xUnit does not.
/// </summary>
public sealed class ExpectationsLastOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase =>
        testCases.OrderBy(test =>
            test.TestMethod.Method.Name == nameof(CrossCheckTests.Matches_the_expectations) ? 1 : 0
        );
}
