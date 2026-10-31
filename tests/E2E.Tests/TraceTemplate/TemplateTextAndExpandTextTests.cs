// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// The port fills a text from the params (<see cref="CacheKeys.Detemplate"/>) and from a pointer-to-value map
/// (<see cref="CacheKeys.ExpandText"/>, upstream's <c>expandText</c>).
/// </summary>
public sealed class TemplateTextAndExpandTextTests
{
    private static Dictionary<string, object?> Params(string name, string full) => new()
    {
        ["name"] = Values.Unique(name),
        ["full"] = Values.Unique(full),
    };

    [Fact]
    public void Round_trips_claims_the_longer_value_first_and_never_templates_inside_a_placeholder()
    {
        var recorded = Params("name", "Ada name");
        var templated = CacheKeys.Template("tap \"Ada name\" then \"name\" in the name column", recorded);

        Assert.Equal("tap \"{{param:/full}}\" then \"{{param:/name}}\" in the {{param:/name}} column", templated);
        Assert.Equal("tap \"Ada name\" then \"name\" in the name column", CacheKeys.Detemplate(templated, recorded));
        Assert.Equal(
            "tap \"Grace Hopper\" then \"Grace\" in the Grace column",
            CacheKeys.Detemplate(templated, Params("Grace", "Grace Hopper")));

        // A value that is part of a slot ("ull" in "full") is not found inside the slot written a moment earlier.
        var inner = new Dictionary<string, object?> { ["full"] = Values.Unique("Ada"), ["x"] = Values.Unique("ull") };
        Assert.Equal("{{param:/full}}", CacheKeys.Template("Ada", inner));
    }

    [Fact]
    public void Refuses_to_expand_a_placeholder_the_current_call_did_not_mark()
    {
        Assert.Null(CacheKeys.ExpandText("tap \"{{param:/name}}\"", new Dictionary<string, string>()));
        Assert.Null(CacheKeys.ExpandText("tap \"{{param:/name}}\"", new Dictionary<string, string> { ["/title"] = "x" }));
        Assert.Equal("no placeholders", CacheKeys.ExpandText("no placeholders", new Dictionary<string, string>()));
    }
}
