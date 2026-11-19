// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;
using Xunit.v3;

namespace E2E.XUnit.V3;

/// <summary>
/// Puts the setup tests (<see cref="E2ESetupAttribute"/>) of a class before its other tests, which start the likeliest first, by the relevance
/// band the test filters gave them (upstream's <c>orderWorkPlans</c>), and otherwise keep xUnit's default order. .NET-only:
/// upstream's runner orders a setup test before its dependents itself, and xUnit orders the test methods. <see cref="E2ETest"/> applies it;
/// a class with its own <c>[TestMethodOrderer]</c> replaces it.
/// </summary>
public sealed class SetupTestMethodOrderer : ITestMethodOrderer
{
    /// <inheritdoc />
    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(
        IReadOnlyCollection<TTestMethod?> testMethods
    )
        where TTestMethod : ITestMethod
    {
        ArgumentNullException.ThrowIfNull(testMethods);
        var setup = DefaultTestMethodOrderer
            .Instance.OrderTestMethods(testMethods.Where(method => IsSetup(method)).ToList())
            .ToList();
        // A stable sort, as upstream's `toSorted` is: LINQ's OrderBy keeps the order of equal keys.
        setup.AddRange(
            DefaultTestMethodOrderer
                .Instance.OrderTestMethods(testMethods.Where(method => !IsSetup(method)).ToList())
                .OrderByDescending(method => BandOf(method))
        );
        return setup;
    }

    private static int BandOf(ITestMethod? testMethod) =>
        testMethod is IXunitTestMethod { Method: var method, TestClass.Class: var type }
            ? Units.RelevanceBand(RunSelection.RelevanceOf(type, method.Name))
            : 0;

    private static bool IsSetup(ITestMethod? testMethod) =>
        testMethod is IXunitTestMethod { Method: var method } && SessionAttributes.Saves(method) is not null;
}
