// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliInit;

/// <summary>.NET-only: the console prompts that stand for <c>@clack/prompts</c>. Upstream has no tests of its prompt library.</summary>
public sealed class ConsolePromptsTests
{
    private static readonly PromptOption[] Options =
    [
        new("web", "Web", "Playwright"),
        new("mobile", "Mobile", "agent-device"),
    ];

    private static ConsolePrompts Ask(string input, out StringWriter output)
    {
        output = new StringWriter();
        return new ConsolePrompts(new StringReader(input), output);
    }

    [Fact]
    public void Multi_select_takes_the_marked_options_on_an_empty_line_and_none_on_none()
    {
        Assert.Equal(["web"], Ask("\n", out _).MultiSelect("Which?", Options, ["web"]));
        Assert.Empty(Ask("none\n", out _).MultiSelect("Which?", Options, ["web"])!);
    }

    [Fact]
    public void Multi_select_reads_numbers_and_names_in_list_order_and_asks_again_for_an_unknown_one()
    {
        Assert.Equal(["web", "mobile"], Ask("mobile, 1\n", out _).MultiSelect("Which?", Options, []));
        var prompts = Ask("3\n2\n", out var output);
        Assert.Equal(["mobile"], prompts.MultiSelect("Which?", Options, []));
        Assert.Contains("Not one of the options: 3", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Select_takes_the_default_on_an_empty_line_and_a_number_or_a_name_otherwise()
    {
        Assert.Equal("web", Ask("\n", out _).Select("Which?", Options, "web"));
        Assert.Equal("mobile", Ask("2\n", out _).Select("Which?", Options, "web"));
        Assert.Equal("mobile", Ask("MOBILE\n", out _).Select("Which?", Options, "web"));
    }

    [Fact]
    public void Text_asks_again_while_the_validator_refuses_and_returns_the_line_it_accepts()
    {
        var prompts = Ask("nope\nyes\n", out var output);
        Assert.Equal("yes", prompts.Text("Say", "yes", value => value == "yes" ? null : "Say yes"));
        Assert.Contains("Say yes", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Confirm_takes_the_initial_answer_on_an_empty_line()
    {
        Assert.True(Ask("\n", out _).Confirm("Sure?"));
        Assert.False(Ask("\n", out _).Confirm("Sure?", initialValue: false));
        Assert.True(Ask("Y\n", out _).Confirm("Sure?", initialValue: false));
        Assert.False(Ask("no\n", out _).Confirm("Sure?"));
    }

    [Fact]
    public void Every_question_cancels_on_q_and_on_the_end_of_the_input()
    {
        Assert.Null(Ask("q\n", out _).MultiSelect("Which?", Options, []));
        Assert.Null(Ask("", out _).MultiSelect("Which?", Options, []));
        Assert.Null(Ask("q\n", out _).Select("Which?", Options, "web"));
        Assert.Null(Ask("", out _).Select("Which?", Options, "web"));
        Assert.Null(Ask("q\n", out _).Text("Say", "yes", _ => null));
        Assert.Null(Ask("", out _).Text("Say", "yes", _ => null));
        Assert.Null(Ask("q\n", out _).Confirm("Sure?"));
        Assert.Null(Ask("", out _).Confirm("Sure?"));
    }
}
