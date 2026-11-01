// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceTemplate;

/// <summary>Upstream's <c>templateText</c> and <c>expandText</c>: <see cref="CacheKeys.TemplateText"/> and <see cref="CacheKeys.ExpandText"/>.</summary>
public sealed class TemplateTextAndExpandTextTests
{
    private static List<ParamTemplate> Templates(string name, string full) => [new("/name", name), new("/full", full)];

    private static Dictionary<string, string> Filled(IEnumerable<ParamTemplate> templates) => templates.ToDictionary(template => template.Pointer, template => template.Value);

    [Fact]
    public void Round_trips_claims_the_longer_value_first_and_never_templates_inside_a_placeholder()
    {
        var recorded = Templates("name", "Ada name");
        var templated = CacheKeys.TemplateText("tap \"Ada name\" then \"name\" in the name column", recorded);

        Assert.Equal("tap \"{{param:/full}}\" then \"{{param:/name}}\" in the {{param:/name}} column", templated);
        Assert.Equal("tap \"Ada name\" then \"name\" in the name column", CacheKeys.ExpandText(templated, Filled(recorded)));
        Assert.Equal(
            "tap \"Grace Hopper\" then \"Grace\" in the Grace column",
            CacheKeys.ExpandText(templated, Filled(Templates("Grace", "Grace Hopper"))));

        // A value that is part of a slot ("ull" in "full") is not found inside the slot written a moment earlier.
        Assert.Equal("{{param:/full}}", CacheKeys.TemplateText("Ada", [new("/full", "Ada"), new("/x", "ull")]));
    }

    [Fact]
    public void Refuses_to_expand_a_placeholder_the_current_call_did_not_mark()
    {
        Assert.Null(CacheKeys.ExpandText("tap \"{{param:/name}}\"", new Dictionary<string, string>()));
        Assert.Null(CacheKeys.ExpandText("tap \"{{param:/name}}\"", new Dictionary<string, string> { ["/title"] = "x" }));
        Assert.Equal("no placeholders", CacheKeys.ExpandText("no placeholders", new Dictionary<string, string>()));
    }
}
