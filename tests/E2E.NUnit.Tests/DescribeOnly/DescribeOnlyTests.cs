// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.DescribeOnly;

/// <summary>Upstream's <c>test('outside', ...)</c> at the top of <c>describe-only.test.ts</c>'s file.</summary>
[Explicit("Discovered by DescribeOnlyTests, never run")]
public sealed class OutsideFixture : E2ETest
{
    [Test]
    public void Outside() { }
}

/// <summary>Upstream's <c>describe.only('checkout', ...)</c>, with its nested <c>describe('refunds', ...)</c>.</summary>
[Explicit("Discovered by DescribeOnlyTests, never run")]
[Only]
public sealed class CheckoutFixture : E2ETest
{
    [Test]
    public void Pays() { }

    [Explicit("Discovered by DescribeOnlyTests, never run")]
    public sealed class Refunds : E2ETest
    {
        [Test]
        public void Refunds_a_card() { }
    }
}

/// <summary>Upstream's <c>describe('profile', { only: true }, ...)</c>.</summary>
[Explicit("Discovered by DescribeOnlyTests, never run")]
[Only]
public sealed class ProfileFixture : E2ETest
{
    [Test]
    public void Renames() { }
}

/// <summary>Upstream's <c>describe('settings', ...)</c> with a <c>test.only</c> beside a plain test.</summary>
[Explicit("Discovered by DescribeOnlyTests, never run")]
public sealed class SettingsFixture : E2ETest
{
    [Test]
    public void Sibling() { }

    [Test]
    [Only]
    public void Focused_elsewhere() { }
}

/// <summary>Upstream's CI case: a <c>describe.only</c> beside an unfocused test.</summary>
[Explicit("Discovered by DescribeOnlyTests, never run")]
[Only]
public sealed class CiCheckoutFixture : E2ETest
{
    [Test]
    public void Pays() { }
}

/// <summary>
/// Upstream's <c>describe-only.test.ts</c>, <c>describe('describe.only')</c>. Both tests are partial: upstream runs a project with the <c>e2e</c> runner and checks the
/// report's passed results and the exit code (2 for <c>ONLY_IN_CI</c>), and the port has no runner to spawn, so these check discovery and <c>HostTestFilters.ExcludeAsync</c>.
/// </summary>
public sealed class DescribeOnlyTests
{
    private static readonly Type[] Focus =
    [
        typeof(OutsideFixture),
        typeof(CheckoutFixture),
        typeof(CheckoutFixture.Refunds),
        typeof(ProfileFixture),
        typeof(SettingsFixture),
    ];

    [Test]
    public async Task Partial_runs_the_focused_groups_and_a_test_only_beside_them_and_nothing_else()
    {
        var config = E2EConfig.Parse("{}", AppContext.BaseDirectory, _ => null);
        var discovered = E2E.NUnit.RunSelection.Discover(Focus, typeof(DescribeOnlyTests).Assembly);

        var selection = await HostTestFilters.ExcludeAsync(
            config,
            [],
            null,
            discovered,
            _ => { },
            CancellationToken.None
        );

        var ran = discovered
            .Where(test => !selection.Excluded.ContainsKey(test.Key))
            .Select(test => $"{test.TypeName}.{test.Title}")
            .ToList();
        Assert.That(
            ran,
            Is.EquivalentTo(
                new[]
                {
                    "CheckoutFixture.Pays",
                    "CheckoutFixture+Refunds.Refunds_a_card",
                    "ProfileFixture.Renames",
                    "SettingsFixture.Focused_elsewhere",
                }
            )
        );
        Assert.That(selection.Excluded.Values, Is.All.EqualTo("not focused by .only"));
        Assert.That(
            selection.Excluded.Keys,
            Is.EquivalentTo(
                new[] { $"{typeof(OutsideFixture).FullName}.Outside", $"{typeof(SettingsFixture).FullName}.Sibling" }
            )
        );
    }

    [Test]
    public async Task Partial_is_refused_in_CI_with_ONLY_IN_CI()
    {
        var config = E2EConfig.Parse("{}", AppContext.BaseDirectory, name => name == "CI" ? "1" : null);
        var discovered = E2E.NUnit.RunSelection.Discover(
            [typeof(CiCheckoutFixture), typeof(OutsideFixture)],
            typeof(DescribeOnlyTests).Assembly
        );

        var error = await Assert.ThrowsAsync<ConfigurationException>(() =>
            HostTestFilters.ExcludeAsync(config, [], null, discovered, _ => { }, CancellationToken.None)
        );

        Assert.That(error!.Code, Is.EqualTo("ONLY_IN_CI"));
    }
}
