// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// <c>describe('filters')</c> of <c>config.test.ts</c>, through <c>ResolvedConfig.ResolveTestFilters</c> (upstream's <c>resolveTestFilters</c>).
/// Not ported: "refuses filters that are not a list" and "says to call a function in the list, naming it by its index and its name", since
/// the list and its items are typed (an <c>IReadOnlyList&lt;ITestFilter?&gt;</c> cannot hold a function or a non-list).
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

    [Fact]
    public void Leaves_the_config_digest_alone_when_a_filter_is_added_changed_or_removed_even_one_holding_a_cyclic_client()
    {
        var config = E2EConfig.Parse("{}", Path.Combine(Path.GetTempPath(), "e2e-config-root"));
        var plain = HostTestFilters.Resolve(config, []).ConfigDigest;
        Assert.NotEmpty(plain);
        Assert.Equal(plain, HostTestFilters.Resolve(config, [Filter("areas"), Filter("jev")]).ConfigDigest);
        Assert.Equal(plain, HostTestFilters.Resolve(config, [new NamedFilter("jev") { Client = new ResolveConfigTests.Cycle() }]).ConfigDigest);
    }

    /// <summary>A filter that leaves nothing out, with a key of its own (upstream's <c>{ name, filter: decide, model }</c>).</summary>
    private sealed class NamedFilter(string name) : ITestFilter
    {
        public string Name { get; } = name;

        public string? Model { get; init; }

        /// <summary>A client that points back at itself (upstream's <c>client</c>): JSON cannot clone it.</summary>
        public ResolveConfigTests.Cycle? Client { get; init; }

        public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) =>
            Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string>() });
    }
}
