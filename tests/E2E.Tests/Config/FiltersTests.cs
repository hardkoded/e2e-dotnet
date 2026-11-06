// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// <c>describe('filters')</c> of <c>config.test.ts</c>, through <c>ResolvedConfig.ResolveTestFilters</c> (upstream's <c>resolveTestFilters</c>).
/// Not ported: "refuses filters that are not a list" and "says to call a function in the list, naming it by its index and its name", since
/// the list and its items are typed (an <c>IReadOnlyList&lt;ITestFilter?&gt;</c> cannot hold a function or a non-list). Not ported: "leaves the
/// config digest alone when a filter is added, changed, or removed, even one holding a cyclic client", since the port has no config digest.
/// </summary>
public sealed class FiltersTests
{
    private static ITestFilter Filter(string name) => new NamedFilter(name);

    private static ConfigurationException Refused(params ITestFilter?[] filters) =>
        Assert.Throws<ConfigurationException>(() => ResolvedConfig.ResolveTestFilters(filters));

    [Fact]
    public void Resolves_the_list_in_declaration_order_empty_when_unset_and_allows_keys_of_their_own()
    {
        Assert.Empty(ResolvedConfig.ResolveTestFilters(null));
        Assert.Empty(ResolvedConfig.ResolveTestFilters([]));
        var areas = Filter("areas");
        var jev = new NamedFilter("jev") { Model = "typesafe-ai/jev" };
        var odd = Filter("v1.2_x-y");
        var filters = ResolvedConfig.ResolveTestFilters([jev, areas, odd]);
        Assert.Equal<ITestFilter>([jev, areas, odd], filters);
        Assert.Same(jev, filters[0]);
        Assert.Single(ResolvedConfig.ResolveTestFilters([Filter(new string('x', 256))]));
    }

    /// <summary>Partial: <see cref="ITestFilter"/> requires a name and a filter method, so only a null item and a null name reach the check.</summary>
    [Fact]
    public void Partial_Refuses_an_item_without_a_string_name_or_a_filter_function()
    {
        foreach (var value in new ITestFilter?[] { null, new NamedFilter(null!) })
        {
            var error = Refused(value);
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal("filters[0] must be a TestFilter: { name, filter(tests, context) }", error.Message);
        }
    }

    [Fact]
    public void Refuses_a_hole_in_the_list_as_an_item_that_is_not_a_TestFilter()
    {
        var gap = new ITestFilter?[3];
        gap[0] = Filter("areas");
        gap[2] = Filter("jev");
        var error = Assert.Throws<ConfigurationException>(() => ResolvedConfig.ResolveTestFilters(gap));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("filters[1] must be a TestFilter: { name, filter(tests, context) }", error.Message);
    }

    [Fact]
    public void Refuses_a_name_outside_the_target_name_rules()
    {
        foreach (var name in new[] { "..", ".", "x y", "areas!", "", "é", new string('x', 257) + "!" })
        {
            var error = Refused(Filter(name));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"invalid filter name \"{name}\": names are ASCII letters, numbers, \"_\", \"-\", or \".\", and cannot be only dots", error.Message);
        }
    }

    [Fact]
    public void Refuses_a_name_over_256_characters_saying_so()
    {
        var error = Refused(Filter(new string('x', 257)));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal($"filter name \"{new string('x', 31)}…\" is longer than 256 characters", error.Message);
    }

    [Fact]
    public void Refuses_two_filters_with_one_name()
    {
        var jev = Filter("jev");
        var error = Refused(jev, Filter("areas"), Filter("jev"));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("filters has two filters named \"jev\"; the report tells filters apart by name", error.Message);
        Assert.Contains("filters has two filters named \"jev\"", Refused(jev, jev).Message, StringComparison.Ordinal);
    }

    /// <summary>A filter that leaves nothing out, with a key of its own (upstream's <c>{ name, filter: decide, model }</c>).</summary>
    private sealed class NamedFilter(string name) : ITestFilter
    {
        public string Name { get; } = name;

        public string? Model { get; init; }

        public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) =>
            Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string>() });
    }
}
