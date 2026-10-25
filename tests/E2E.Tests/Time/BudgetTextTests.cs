// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Time;

/// <summary>Upstream's <c>describe('budgetText')</c> (<c>tests/unit/time.test.ts</c>).</summary>
public sealed class BudgetTextTests
{
    [Fact]
    public void Names_a_budget_exactly_in_the_largest_unit_that_divides_it()
    {
        long[] budgets = [50, 1000, 1499, 1500, 30_000, 60_000, 90_500, 300_000];
        Assert.Equal(["50ms", "1s", "1499ms", "1500ms", "30s", "1m", "90500ms", "5m"], budgets.Select(Internal.Time.BudgetText));
    }
}
