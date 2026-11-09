// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>The suite's base class: <see cref="E2ETest"/> against the benchmark app, which <see cref="BenchmarkSite"/> serves.</summary>
public abstract class BenchmarkTest : E2ETest
{
    protected override string? BaseUrl => BenchmarkSite.Url;
}
