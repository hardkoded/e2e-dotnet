// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Reports;

namespace E2E.Tests.Reporters;

/// <summary>
/// .NET-only: <c>reporters</c> in <c>e2e.config.json</c> names reporter types, where upstream's config lists reporter objects built in code.
/// </summary>
public sealed class ConfigReportersTests
{
    private const string First = "E2E.Tests.Reporters.FirstReporter, E2E.Tests";

    private const string Second = "E2E.Tests.Reporters.SecondReporter, E2E.Tests";

    private static E2EConfig Parse(string reporters) => E2EConfig.Parse("{ \"reporters\": " + reporters + " }", Path.GetTempPath());

    private static string Invalid(string reporters) => Assert.Throws<ConfigurationException>(() => Parse(reporters)).Message;

    [Fact]
    public void A_config_with_no_reporters_key_has_no_reporters()
    {
        Assert.Empty(E2EConfig.Default(Path.GetTempPath()).Reporters);
    }

    [Fact]
    public void Builds_the_reporters_in_config_order_and_a_type_listed_twice_runs_twice()
    {
        var config = Parse($$"""["{{First}}", {"type": "{{Second}}"}, "{{First}}"]""");

        var reporters = E2E.Reporters.Create(config);

        Assert.Equal(["first", "second", "first"], reporters.Select(reporter => reporter.Name));
    }

    [Fact]
    public void Resolves_a_type_name_without_an_assembly_from_the_loaded_assemblies()
    {
        var config = Parse("""["E2E.Tests.Reporters.FirstReporter"]""");

        Assert.IsType<FirstReporter>(Assert.Single(E2E.Reporters.Create(config)));
    }

    [Fact]
    public void Hands_the_options_to_a_configurable_reporter()
    {
        var config = Parse($$$"""[{"type": "{{{Second}}}", "options": {"label": "ci"}}]""");

        var reporter = Assert.IsType<SecondReporter>(Assert.Single(E2E.Reporters.Create(config)));

        Assert.Equal("ci", reporter.Label);
    }

    [Fact]
    public void Does_not_call_configure_without_options()
    {
        var reporter = Assert.IsType<SecondReporter>(Assert.Single(E2E.Reporters.Create(Parse($"[\"{Second}\"]"))));

        Assert.Null(reporter.Label);
    }

    [Fact]
    public void A_reporter_that_cannot_be_built_is_skipped_and_the_others_still_run()
    {
        var config = Parse($$$"""["{{{First}}}", {"type": "{{{Second}}}", "options": {"bad": true}}, "{{{First}}}"]""");

        Assert.Equal(2, E2E.Reporters.Create(config).Count);
    }

    [Fact]
    public void Rejects_reporters_that_is_not_an_array()
    {
        Assert.Contains("reporters must be an array", Invalid("{}"));
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("[{\"type\": 1}]")]
    [InlineData("[\"\"]")]
    public void Rejects_an_entry_that_is_not_a_type_name_or_an_object_with_a_string_type(string reporters)
    {
        Assert.Contains("reporters[0] must be a reporter type name or an object with a string \"type\"", Invalid(reporters));
    }

    [Fact]
    public void Rejects_an_unknown_key_in_an_entry()
    {
        Assert.Contains("unknown reporters[0] key \"typ\"", Invalid($"[{{\"type\": \"{First}\", \"typ\": 1}}]"));
    }

    [Fact]
    public void Rejects_options_that_are_not_an_object()
    {
        Assert.Contains("reporters[0].options must be an object", Invalid($"[{{\"type\": \"{First}\", \"options\": 1}}]"));
    }

    [Fact]
    public void Rejects_a_type_name_that_does_not_resolve_and_hints_a_missing_assembly()
    {
        var message = Invalid("[\"Nope.Missing, Nope\"]");

        Assert.Contains("reporters[0] reporter type \"Nope.Missing, Nope\" was not found", message);
        Assert.Contains("assembly", message);
    }

    [Theory]
    [InlineData("E2E.Tests.Reporters.NotAReporter, E2E.Tests", "derives from E2E.Reports.Reporter")]
    [InlineData("E2E.Tests.Reporters.AbstractReporter, E2E.Tests", "non-abstract")]
    [InlineData("E2E.Tests.Reporters.ParameterReporter, E2E.Tests", "public constructor with no parameters")]
    public void Rejects_a_type_that_cannot_be_a_reporter(string type, string reason)
    {
        Assert.Contains(reason, Invalid($"[\"{type}\"]"));
    }

    [Fact]
    public void Rejects_options_for_a_reporter_that_is_not_configurable_without_echoing_them()
    {
        var message = Invalid($"[{{\"type\": \"{First}\", \"options\": {{\"key\": \"s3cret-value\"}}}}]");

        Assert.Contains("takes no options", message);
        Assert.DoesNotContain("s3cret-value", message);
    }

    [Fact]
    public void The_attribute_is_gone()
    {
        Assert.Null(typeof(Reporter).Assembly.GetType("E2E.Reports.E2EReporterAttribute"));
    }
}

/// <summary>A reporter a config lists.</summary>
public sealed class FirstReporter : Reporter
{
    public override string Name => "first";
}

/// <summary>A reporter that takes options and rejects the key <c>bad</c> without echoing its value.</summary>
public sealed class SecondReporter : Reporter, IConfigurableReporter
{
    public string? Label { get; private set; }

    public override string Name => "second";

    public void Configure(JsonElement options)
    {
        if (options.TryGetProperty("bad", out _))
        {
            throw new ArgumentException("option \"bad\" is not supported");
        }

        Label = options.TryGetProperty("label", out var label) ? label.GetString() : null;
    }
}

/// <summary>Not a reporter.</summary>
public sealed class NotAReporter;

/// <summary>A reporter type that cannot be built.</summary>
public abstract class AbstractReporter : Reporter;

/// <summary>A reporter with no parameterless constructor.</summary>
public sealed class ParameterReporter(string name) : Reporter
{
    public override string Name => name;
}
