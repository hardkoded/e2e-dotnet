// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;
using Xunit.v3;

namespace E2E.XUnit.V3;

/// <summary>
/// Starts the test classes with the tests the test filters rated likeliest to fail first (.NET-only: upstream's runner orders its work plans
/// itself, and xUnit owns the order of the collections). A class ranks by the highest relevance band of its tests, as a file does in upstream's
/// <c>orderWorkPlans</c>; classes in one band keep xUnit's order. It orders only xUnit's default collection of one class. Register it for the
/// assembly: <c>[assembly: TestCollectionOrderer(typeof(E2E.XUnit.V3.RelevanceTestCollectionOrderer))]</c>.
/// </summary>
public sealed class RelevanceTestCollectionOrderer : ITestCollectionOrderer
{
    private const string DefaultCollectionPrefix = "Test collection for ";

    /// <inheritdoc />
    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(
        IReadOnlyCollection<TTestCollection> testCollections
    )
        where TTestCollection : ITestCollection
    {
        ArgumentNullException.ThrowIfNull(testCollections);
        return [.. testCollections.OrderByDescending(collection => BandOf(collection))];
    }

    private static int BandOf(ITestCollection collection) =>
        collection.TestCollectionDisplayName.StartsWith(DefaultCollectionPrefix, StringComparison.Ordinal)
            ? RunSelection.BandOfClass(collection.TestCollectionDisplayName[DefaultCollectionPrefix.Length..])
            : 0;
}
