// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('e2e init')</c> of <c>tests/unit/cli-init.test.ts</c>. The port has no package.json step, no
/// install, no examples, and no <c>continueWith</c>, so the <c>it</c>s that exist to check those are not ported:
/// "matches imports and dependencies to engine=$engine, ai=$ai" (package.json), "writes one target and one example file
/// per engine when both are selected" (the port runs one target), "writes a $gateway subscription model and names the
/// sign-in as the next step" (the model is a provider id and there is no package.json; the sign-in step is checked in
/// <see cref="Partial_Writes_a_subscription_model_and_names_the_sign_in_as_the_next_step"/>), "adds only the missing script
/// to package.json beside an existing config", "writes the selected dependencies when installation is declined", "with
/// continueWith" (three <c>it</c>s), the package.json <c>type</c> <c>it.each</c> (three), "adds missing dependencies without
/// changing existing fields, ranges, or formatting", "appends to a hand-ordered devDependencies block", the package.json
/// <c>it.each</c> at line 629, "uses pnpm when selected by the %s", "keeps the scaffold and returns a failure when
/// installation fails", and "is idempotent with both engines and adds no engine-less example beside theirs".
/// </summary>
public sealed partial class E2eInitTests : CliInitTestBase
{
    private static readonly string[] BothMcpFiles = [".mcp.json", ".cursor/mcp.json"];

    [GeneratedRegex(@"\A---\r?\nname: e2e-dotnet\r?\n")]
    private static partial Regex SkillFrontmatter();

    [Fact]
    // Partial: the port writes e2e.config.json, no package.json, no example test, and prints dotnet test.
    public async Task Partial_Adds_the_runner_Playwright_and_AI_with_yes_and_does_not_install_or_prompt()
    {
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        var config = Json("e2e.config.json");
        Assert.Equal("gateway", (string?)config["agents"]!["default"]!["provider"]);
        Assert.Equal("openai/gpt-6-luna-fast", (string?)config["agents"]!["default"]!["model"]);
        Assert.Equal("You are a thorough QA agent. Verify every outcome.", (string?)config["agents"]!["default"]!["system"]);
        Assert.Equal("web", (string?)config["targets"]![0]!["platform"]);
        Assert.Equal("http://localhost:3000", (string?)config["targets"]![0]!["app"]!["url"]);
        // The config loads: init never writes one the port rejects.
        _ = E2EConfig.Parse(Read("e2e.config.json"), Dir);
        var server = JsonNode.Parse("""{ "mcpServers": { "e2e": { "command": "e2e", "args": ["mcp"] } } }""")!;
        Assert.True(JsonNode.DeepEquals(server, Json(".mcp.json")));
        Assert.True(JsonNode.DeepEquals(server, Json(".cursor/mcp.json")));
        Assert.Contains(".e2e/results/", Read(".gitignore"), StringComparison.Ordinal);
        Assert.Contains(".e2e/cache/", Read(".gitignore"), StringComparison.Ordinal);
        Assert.Contains(".e2e/report.json", Read(".gitignore"), StringComparison.Ordinal);
        Assert.Matches(SkillFrontmatter(), Read(".agents/skills/e2e-dotnet/SKILL.md"));
        Assert.Contains("# Setting up e2e for .NET", Read(".claude/skills/e2e-dotnet/references/setup.md"), StringComparison.Ordinal);
        Assert.Contains("Next: dotnet test", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the outcome has no example and install fields, and no ready flag.
    public async Task Partial_Hands_back_what_yes_chose_then_that_a_second_run_had_nothing_to_do()
    {
        Assert.Equal(
            new InitRunOutcome(0, InitResult.Scaffolded, true, false, "web", "vercel", true, true),
            await RunInitAsync());
        var second = await RunInitAsync();
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(InitResult.AlreadyInitialized, second.Result);
        Assert.True(second.ExistingConfig);
        Assert.Null(second.Engine);
        Assert.Null(second.Gateway);
    }

    [Fact]
    // Partial: the port has no package.json to fail on, so the second run that cannot start is a skill-less installation.
    public async Task Partial_Hands_back_a_run_that_could_not_start()
    {
        Assert.Equal(InitResult.NotInteractive, (await RunInitAsync(new InitOptions { Interactive = false })).Result);
        Assert.Equal(2, (await RunInitAsync(new InitOptions { Interactive = false })).ExitCode);
        var outcome = await RunInitAsync(new InitOptions { Yes = true, ReadSkillFiles = () => [] });
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(InitResult.InvalidProject, outcome.Result);
    }

    [Theory]
    [InlineData("chatgpt", "openai", "chatgpt", "gpt-6-luna")]
    [InlineData("copilot", "github-copilot", "copilot", "claude-sonnet-5")]
    [InlineData("opencode-console", "opencode-console", "opencode-console", "deepseek-v4.1-flash")]
    [InlineData("grok", "spacexai", "grok", "grok-4")]
    // Partial: the model is a provider id in e2e.config.json, with no import line and no SDK packages.
    public async Task Partial_Writes_a_subscription_model_and_names_the_sign_in_as_the_next_step(string gateway, string login, string provider, string model)
    {
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Engines = ["web"], Gateway = gateway })).ExitCode);
        var agent = Json("e2e.config.json")["agents"]!["default"]!;
        Assert.Equal(provider, (string?)agent["provider"]);
        Assert.Equal(model, (string?)agent["model"]);
        Assert.Contains("e2e login " + login + ", then", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the engine is mobile on Android here, there is no package.json to install, and no install flag.
    public async Task Partial_Takes_every_answer_from_flags_under_yes_and_writes_only_the_chosen_locations()
    {
        var outcome = await RunInitAsync(new InitOptions
        {
            Yes = true,
            Engines = ["mobile"],
            Gateway = "openrouter",
            Skills = [".agents/skills"],
            Mcp = [".mcp.json"],
        });
        Assert.Equal(new InitRunOutcome(0, InitResult.Scaffolded, true, false, "mobile", "openrouter", true, true), outcome);
        var config = Json("e2e.config.json");
        Assert.Equal("mobile", Engines.GetEnginePresets().First(preset => preset.Id == "mobile").Id);
        Assert.Contains((string?)config["targets"]![0]!["platform"], new[] { "ios", "android" });
        Assert.Equal("openrouter", (string?)config["agents"]!["default"]!["provider"]);
        Assert.True(Exists(".agents/skills/e2e-dotnet/SKILL.md"));
        Assert.False(Exists(".claude"));
        Assert.True(Exists(".mcp.json"));
        Assert.False(Exists(".cursor"));
    }

    [Fact]
    // Partial: there is no package.json, so only the model, skill, and MCP config are checked.
    public async Task Partial_Writes_no_model_skill_or_MCP_config_when_each_flag_says_none()
    {
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Engines = [], Gateway = "none", Skills = [], Mcp = [] });
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal("none", outcome.Engine);
        Assert.Equal("none", outcome.Gateway);
        Assert.False(outcome.Skill);
        Assert.False(outcome.Mcp);
        Assert.Null(Json("e2e.config.json")["agents"]);
        Assert.Equal([".gitignore", "e2e.config.json"], Directory.EnumerateFileSystemEntries(Dir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Contains("Skipped the agent skill", Output, StringComparison.Ordinal);
        Assert.Contains("Skipped the MCP server", Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" https://llm.example/v1 ", "https://llm.example/v1")]
    [InlineData(null, "http://127.0.0.1:11434/v1")]
    public async Task Writes_the_endpoint_of_an_OpenAI_compatible_gateway_under_yes_else_the_local_placeholder(string? endpoint, string written)
    {
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Gateway = "openai-compatible", Endpoint = endpoint })).ExitCode);
        Assert.Equal(written, (string?)Json("e2e.config.json")["agents"]!["default"]!["baseUrl"]);
        _ = E2EConfig.Parse(Read("e2e.config.json"), Dir);
    }

    [Fact]
    public async Task Warns_that_engine_and_gateway_flags_do_not_apply_to_an_existing_config()
    {
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{}\n");
        var outcome = await RunInitAsync(new InitOptions { Yes = true, Engines = ["mobile"], Gateway = "openrouter" });
        Assert.Equal(0, outcome.ExitCode);
        Assert.Null(outcome.Engine);
        Assert.Null(outcome.Gateway);
        Assert.Equal("{}\n", Read("e2e.config.json"));
        Assert.Contains("Ignoring --engine, --gateway: e2e.config.json exists, and these apply to a new config only", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Installs_the_skill_exactly_where_skills_says_on_a_re_run_leaving_earlier_locations_untouched()
    {
        WriteCopy(".agents/skills", "stale");
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Skills = [".claude/skills"], Mcp = [] })).ExitCode);
        Assert.Equal(BundledSkillMd(), Read(".claude/skills/e2e-dotnet/SKILL.md"));
        Assert.False(IsLink(Path_(".claude/skills/e2e-dotnet")));
        Assert.Equal("stale", Read(".agents/skills/e2e-dotnet/SKILL.md"));
    }

    [Fact]
    // Partial: the flags have no --install, so they answer every question but that one.
    public async Task Partial_Refuses_to_run_without_a_terminal_and_without_yes_even_when_flags_answer_every_question()
    {
        var outcome = await RunInitAsync(new InitOptions { Interactive = false, Engines = ["web"], Gateway = "none", Skills = [], Mcp = [] });
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(InitResult.NotInteractive, outcome.Result);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Dir));
        Assert.Empty(Prompts.ConfirmCalls);
        Assert.Contains("needs an interactive terminal", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the port has no installation, so the defaults the message names are the engine and the gateway.
    public async Task Partial_Refuses_to_prompt_without_a_terminal_and_names_yes()
    {
        Assert.Equal(2, (await RunInitAsync(new InitOptions { Interactive = false })).ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Dir));
        Assert.Empty(Prompts.SelectCalls);
        Assert.Empty(Prompts.ConfirmCalls);
        Assert.Contains("needs an interactive terminal", Output, StringComparison.Ordinal);
        Assert.Contains("pass --yes to accept the defaults (the web engine, the Vercel AI Gateway) or --yes with flags such as --engine for other answers", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validates_the_endpoint_the_way_config_resolution_will()
    {
        Prompts.Engines = ["web"];
        Prompts.NextSelect("openai-compatible");
        Prompts.NextConfirm(true);
        Assert.Equal(0, (await RunInitAsync(new InitOptions())).ExitCode);
        var validate = Prompts.TextCalls[0].Validate;
        Assert.Contains("Enter the base URL", validate(null), StringComparison.Ordinal);
        Assert.Contains("Enter the base URL", validate("   "), StringComparison.Ordinal);
        Assert.Contains("Not a URL", validate("not a url"), StringComparison.Ordinal);
        Assert.Contains("HTTPS", validate("http://llm.example/v1"), StringComparison.Ordinal);
        Assert.Null(validate("http://localhost:11434/v1"));
        Assert.Null(validate("https://llm.example/v1"));
    }

    [Fact]
    // Partial: the port has no install question, so it checks neither the installation nor the package.json.
    public async Task Partial_Accepts_interactive_selections_and_installs_once_after_writing_all_files()
    {
        Prompts.Engines = ["web"];
        Prompts.NextSelect("vercel");
        Prompts.NextConfirm(true);
        Assert.Equal(0, (await RunInitAsync(new InitOptions())).ExitCode);
        var gateway = Assert.Single(Prompts.SelectCalls);
        Assert.Contains("Which model gateway", gateway.Message, StringComparison.Ordinal);
        Assert.Equal("vercel", gateway.InitialValue);
        Assert.Equal(new ConfirmCall("Apply these changes?", true), Assert.Single(Prompts.ConfirmCalls));
        Assert.Equal("gateway", (string?)Json("e2e.config.json")["agents"]!["default"]!["provider"]);
        Assert.Contains("Next: dotnet test", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the port has no install question and no AI SDK in its gateway question.
    public async Task Partial_Offers_every_gateway_and_none_and_asks_an_OpenAI_compatible_endpoint_for_its_URL()
    {
        Prompts.Engines = ["web"];
        Prompts.NextSelect("openai-compatible");
        Prompts.NextText(" http://127.0.0.1:11434/v1 ");
        Prompts.NextConfirm(true);
        Assert.Equal(0, (await RunInitAsync(new InitOptions())).ExitCode);
        var gateway = Assert.Single(Prompts.SelectCalls);
        Assert.Contains("Which model gateway", gateway.Message, StringComparison.Ordinal);
        Assert.Equal("vercel", gateway.InitialValue);
        Assert.Equal(
            ["vercel", "openrouter", "openai-compatible", "chatgpt", "copilot", "opencode-console", "grok", "none"],
            gateway.Options.Select(option => option.Value));
        Assert.Equal("Vercel AI Gateway", gateway.Options[0].Label);
        Assert.Equal("http://127.0.0.1:11434/v1", Assert.Single(Prompts.TextCalls).Placeholder);
        var agent = Json("e2e.config.json")["agents"]!["default"]!;
        Assert.Equal("openai-compatible", (string?)agent["provider"]);
        Assert.Equal("http://127.0.0.1:11434/v1", (string?)agent["baseUrl"]);
        Assert.Equal("gpt-6-luna", (string?)agent["model"]);
    }

    [Fact]
    // Partial: the port has no install flag, so the flags leave the gateway the only select question and the apply the only confirm.
    public async Task Partial_Asks_only_the_questions_no_flag_answered()
    {
        Prompts.NextSelect("none");
        Prompts.NextConfirm(true);
        var outcome = await RunInitAsync(new InitOptions { Engines = ["web"], Skills = [".claude/skills"], Mcp = [] });
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal("web", outcome.Engine);
        Assert.Equal("none", outcome.Gateway);
        Assert.Contains("Which model gateway", Assert.Single(Prompts.SelectCalls).Message, StringComparison.Ordinal);
        Assert.Empty(Prompts.MultiSelectCalls);
        Assert.Equal("Apply these changes?", Assert.Single(Prompts.ConfirmCalls).Message);
        Assert.True(File.Exists(Path_(".claude/skills/e2e-dotnet/SKILL.md")));
        Assert.False(Exists(".agents"));
    }

    [Fact]
    // Partial: the port has no install question and runs one target, so the third run chooses mobile alone.
    public async Task Partial_Hands_back_the_prompted_choices_a_cancellation_with_what_was_chosen_so_far_and_a_failed_install()
    {
        Prompts.Engines = ["mobile"];
        Prompts.NextSelect("none");
        Prompts.Skills = [".agents/skills"];
        Prompts.Mcp = null;
        Assert.Equal(
            new InitRunOutcome(0, InitResult.Cancelled, false, false, "mobile", "none", true, false),
            await RunInitAsync(new InitOptions()));

        Prompts.Engines = ["mobile"];
        Prompts.Mcp = [".mcp.json"];
        Prompts.NextSelect("none");
        Prompts.NextConfirm(true);
        var outcome = await RunInitAsync(new InitOptions());
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(InitResult.Scaffolded, outcome.Result);
        Assert.Equal("mobile", outcome.Engine);
    }

    [Fact]
    // .NET-only: the port runs one target, so the engines question refuses two before anything is written.
    public async Task Refuses_two_engines_from_the_prompt_before_writing_anything()
    {
        Prompts.Engines = ["web", "mobile"];
        var outcome = await RunInitAsync(new InitOptions());
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(InitResult.InvalidProject, outcome.Result);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Dir));
        Assert.Contains("the .NET port runs one target; choose one engine", Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("engine")]
    [InlineData("gateway")]
    [InlineData("endpoint")]
    [InlineData("skill")]
    [InlineData("mcp")]
    [InlineData("files")]
    // Partial: the port has no install stage.
    public async Task Partial_Leaves_the_directory_untouched_when_cancelling_at_stage(string stage)
    {
        Prompts.Engines = stage == "engine" ? null : ["web"];
        Prompts.Skills = stage == "skill" ? null : [".agents/skills"];
        Prompts.Mcp = stage == "mcp" ? null : [".mcp.json"];
        Prompts.NextSelect(stage == "gateway" ? null : "openai-compatible");
        Prompts.NextText(stage == "endpoint" ? null : "http://127.0.0.1:11434/v1");
        Prompts.NextConfirm(stage != "files", null);
        Assert.Equal(0, (await RunInitAsync(new InitOptions())).ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Dir));
    }

    [Fact]
    public async Task Does_not_create_the_directory_when_cancelling()
    {
        var target = Path_("later");
        Prompts.Engines = null;
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Directory = "later" }, target)).ExitCode);
        Assert.False(Directory.Exists(target));
    }

    [Theory]
    [InlineData(true, "ios", "Settings")]
    [InlineData(false, "android", "com.android.settings")]
    // Partial: the preset is checked by its target entry, since the port writes no example test.
    public void Partial_Defaults_agent_device_to_the_platform_of_the_host(bool darwin, string platform, string app)
    {
        var target = Engines.GetEnginePresets(darwin).First(preset => preset.Id == "mobile").Target();
        Assert.Equal(platform, (string?)target["platform"]);
        Assert.Equal(app, (string?)target["app"]!["bundleId"]);
        Assert.Equal(["web", "mobile"], Engines.GetEnginePresets(darwin).Select(preset => preset.Id));
    }

    [Fact]
    // Partial: the port has no package.json, so the existing config and the engine/gateway packages are not checked.
    public async Task Partial_Never_overwrites_an_existing_e2e_config_json()
    {
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{ \"custom\": true }\n");
        await RunInitAsync();
        Assert.Equal("{ \"custom\": true }\n", Read("e2e.config.json"));
        Assert.Contains("Exists, not touching: e2e.config.json", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the port ignores .e2e/results, cache, report.json, and ai-trace files, not upstream's list.
    public async Task Partial_Reconciles_new_gitignore_entries_without_replacing_existing_source_files()
    {
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{ \"custom\": true }\n");
        var older = ".e2e/artifacts/\n.e2e/cache/\n";
        await File.WriteAllTextAsync(Path_(".gitignore"), older);
        await RunInitAsync();
        Assert.Equal("{ \"custom\": true }\n", Read("e2e.config.json"));
        Assert.Equal(older + ".e2e/results/\n.e2e/report.json\n.e2e/ai-trace-*.json\n", Read(".gitignore"));
    }

    [Fact]
    // Partial: the locations come from flags, since the port has no prompt to answer.
    public async Task Partial_Installs_the_skill_where_selected_then_refreshes_only_those_copies()
    {
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Skills = [".claude/skills"] })).ExitCode);
        Assert.Contains("e2e", Read(".mcp.json"), StringComparison.Ordinal);
        Assert.False(Exists(".agents/skills"));
        Assert.Contains("Created .claude/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
        var reference = Path_(".claude/skills/e2e-dotnet/references/setup.md");
        var shipped = Read(".claude/skills/e2e-dotnet/references/setup.md");

        await File.WriteAllTextAsync(reference, "stale\n");
        ClearOutput();
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Equal(shipped, Read(".claude/skills/e2e-dotnet/references/setup.md"));
        Assert.False(Exists(".agents/skills"));
        Assert.Contains("Updated .claude/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repairs_a_damaged_copy_without_asking_and_leaves_other_locations_alone()
    {
        Directory.CreateDirectory(Path_(".agents/skills/e2e-dotnet/references"));
        await File.WriteAllTextAsync(Path_(".agents/skills/e2e-dotnet/references/setup.md"), "stale\n");
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Matches(SkillFrontmatter(), Read(".agents/skills/e2e-dotnet/SKILL.md"));
        Assert.Contains("# Setting up e2e for .NET", Read(".agents/skills/e2e-dotnet/references/setup.md"), StringComparison.Ordinal);
        Assert.False(Exists(".claude"));
        Assert.Contains("Updated .agents/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: run under --yes, so the plan summary and its confirmation are not printed.
    public async Task Partial_Links_claude_skills_to_the_copy_in_agents_skills_when_both_are_chosen_and_a_second_run_has_nothing_to_do()
    {
        if (!Symlinks)
        {
            return;
        }

        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        var link = Path_(".claude/skills/e2e-dotnet");
        Assert.True(IsLink(link));
        Assert.Equal("../../.agents/skills/e2e-dotnet", new FileInfo(link).LinkTarget!.Replace('\\', '/'));
        Assert.Equal(Real(Path_(".agents/skills/e2e-dotnet")), Real(link));
        Assert.Equal(Read(".claude/skills/e2e-dotnet/SKILL.md"), Read(".agents/skills/e2e-dotnet/SKILL.md"));
        Assert.Contains("Created .agents/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
        Assert.Contains("Linked .claude/skills/e2e-dotnet -> ../../.agents/skills/e2e-dotnet", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("(replaced", Output, StringComparison.Ordinal);

        ClearOutput();
        Assert.Equal(InitResult.AlreadyInitialized, (await RunInitAsync()).Result);
        Assert.True(IsLink(link));
        Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: run under --yes, so the plan summary and its confirmation are not printed.
    public async Task Partial_Replaces_an_earlier_copy_in_claude_skills_with_the_link_when_it_holds_only_shipped_files()
    {
        if (!Symlinks)
        {
            return;
        }

        WriteCopy(".agents/skills");
        WriteCopy(".claude/skills", "stale\n");
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        var link = Path_(".claude/skills/e2e-dotnet");
        Assert.True(IsLink(link));
        Assert.Equal("../../.agents/skills/e2e-dotnet", new FileInfo(link).LinkTarget!.Replace('\\', '/'));
        Assert.Matches(SkillFrontmatter(), Read(".claude/skills/e2e-dotnet/SKILL.md"));
        Assert.Contains("Linked .claude/skills/e2e-dotnet -> ../../.agents/skills/e2e-dotnet (replaced the copy)", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Updated .agents/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_an_earlier_copy_in_claude_skills_that_holds_other_files_and_refreshes_it()
    {
        WriteCopy(".agents/skills");
        WriteCopy(".claude/skills", "stale\n");
        await File.WriteAllTextAsync(Path_(".claude/skills/e2e-dotnet/notes.md"), "mine\n");
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.True(Directory.Exists(Path_(".claude/skills/e2e-dotnet")));
        Assert.False(IsLink(Path_(".claude/skills/e2e-dotnet")));
        Assert.Equal("mine\n", Read(".claude/skills/e2e-dotnet/notes.md"));
        Assert.Matches(SkillFrontmatter(), Read(".claude/skills/e2e-dotnet/SKILL.md"));
        Assert.Contains("Updated .claude/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_an_earlier_copy_in_claude_skills_whose_SKILL_md_is_a_symlink_and_leaves_that_link_alone()
    {
        if (!Symlinks)
        {
            return;
        }

        WriteCopy(".agents/skills");
        WriteCopy(".claude/skills");
        var mine = Path_("mine.md");
        await File.WriteAllTextAsync(mine, "mine\n");
        var linked = Path_(".claude/skills/e2e-dotnet/SKILL.md");
        File.Delete(linked);
        Link(linked, Path.Combine("..", "..", "..", "mine.md"), directory: false);
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet/ (.claude/skills/e2e-dotnet/SKILL.md -> " + Real(mine) + ")", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path_(".claude/skills/e2e-dotnet")));
        Assert.True(IsLink(linked));
        Assert.Equal("mine\n", Read("mine.md"));
    }

    [Fact]
    public async Task Needs_no_link_when_claude_skills_already_leads_to_agents_skills()
    {
        if (!Symlinks)
        {
            return;
        }

        Directory.CreateDirectory(Path_(".agents/skills"));
        Directory.CreateDirectory(Path_(".claude"));
        Link(Path_(".claude/skills"), Path.Combine("..", ".agents", "skills"), directory: true);
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Matches(SkillFrontmatter(), Read(".agents/skills/e2e-dotnet/SKILL.md"));
        Assert.Equal(["e2e-dotnet"], Directory.EnumerateFileSystemEntries(Path_(".agents/skills")).Select(Path.GetFileName));
        Assert.True(Directory.Exists(Path_(".agents/skills/e2e-dotnet")));
        Assert.False(IsLink(Path_(".agents/skills/e2e-dotnet")));
        Assert.True(IsLink(Path_(".claude/skills")));
        Assert.Contains("Created .agents/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("not touching", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_before_any_prompt_or_write_when_the_package_lacks_its_skill_files()
    {
        // The missing skill is injected through InitOptions.ReadSkillFiles, in place of mocking readSkillFiles.
        var outcome = await RunInitAsync(new InitOptions { ReadSkillFiles = () => [] });
        Assert.Equal(2, outcome.ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Dir));
        Assert.Empty(Prompts.SelectCalls);
        Assert.Empty(Prompts.MultiSelectCalls);
        Assert.Contains("reinstall e2e", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the port has no install question and prints its own commands for the skill and the MCP server.
    public async Task Partial_Offers_the_skill_to_an_initialized_project_and_points_at_e2e_guide_when_declined()
    {
        await File.WriteAllTextAsync(Path_("e2e.config.json"), "{ \"custom\": true }\n");
        Prompts.Engines = ["web"];
        Prompts.Skills = [];
        Prompts.Mcp = [];
        Prompts.NextConfirm(true);
        Assert.Equal(0, (await RunInitAsync(new InitOptions())).ExitCode);
        Assert.Empty(Prompts.SelectCalls);
        Assert.Equal(2, Prompts.MultiSelectCalls.Count);
        Assert.False(Exists(".agents"));
        Assert.False(Exists(".claude"));
        Assert.False(Exists(".mcp.json"));
        Assert.Contains("e2e guide", Output, StringComparison.Ordinal);
        Assert.Contains("claude mcp add --scope project e2e -- e2e mcp", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_idempotent()
    {
        await RunInitAsync(new InitOptions { Yes = true, Engines = ["web"], Gateway = "vercel" });
        string[] files = ["e2e.config.json", ".gitignore", ".agents/skills/e2e-dotnet/SKILL.md"];
        var before = files.Select(Read).ToList();
        ClearOutput();
        await RunInitAsync();
        Assert.Equal(before, files.Select(Read));
        Assert.Contains("Nothing to create; project already initialized", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the port's .gitignore entries are its own, and the file has no node_modules line.
    public async Task Partial_Preserves_existing_ignores_and_appends_only_missing_entries()
    {
        await File.WriteAllTextAsync(Path_(".gitignore"), "bin/\r\n.e2e/results/");
        await RunInitAsync();
        Assert.StartsWith("bin/\r\n.e2e/results/\n", Read(".gitignore"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(Read(".gitignore"), @"\.e2e/results/"));
        Assert.Contains(".e2e/cache/", Read(".gitignore"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Skips_a_copy_with_a_directory_where_a_file_goes_or_a_file_where_a_directory_goes_naming_each()
    {
        Directory.CreateDirectory(Path_(".agents/skills/e2e-dotnet/SKILL.md"));
        await File.WriteAllTextAsync(Path_(".agents/skills/e2e-dotnet/references"), "not a directory\n");
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Contains("Broken, not touching: .agents/skills/e2e-dotnet/ (SKILL.md is a directory, references is a file)", Output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path_(".agents/skills/e2e-dotnet/SKILL.md")));
        Assert.Equal("not a directory\n", Read(".agents/skills/e2e-dotnet/references"));
        Assert.False(Exists(".claude"));
        Assert.NotNull(Json("e2e.config.json")["targets"]);
        Assert.NotNull(Json(".mcp.json")["mcpServers"]!["e2e"]);
    }

    [Fact]
    public void Plans_a_location_outside_the_project_as_links_even_with_no_symlink_on_the_way()
    {
        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var location = Path.GetRelativePath(Dir, Path.Combine(elsewhere, "skills")).Replace('\\', '/');
            var bundled = E2E.Cli.Skill.ReadFiles();
            var installs = AgentSkill.PlanSkillInstall(Dir, [location], bundled);
            var install = Assert.Single(installs);
            Assert.Equal(location + "/e2e-dotnet", install.Relative);
            Assert.False(install.Existing);
            Assert.Empty(install.Obstacles);
            Assert.Equal(
                bundled.Select(file => new SkillLink(location + "/e2e-dotnet/" + file.Relative, Path.Combine(Real(elsewhere), "skills", "e2e-dotnet", file.Relative.Replace('/', Path.DirectorySeparatorChar)))),
                install.Links);
            Assert.False(Directory.Exists(Path.Combine(elsewhere, "skills")));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    // Partial: the directory is created and named, and the next steps cd into it; the next step is dotnet test, not npm.
    public async Task Partial_Creates_a_named_directory_and_starts_the_next_steps_with_cd_into_it()
    {
        var target = Path.Combine(Dir, "apps", "web");
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Directory = "apps/web" }, target)).ExitCode);
        Assert.True(File.Exists(Path.Combine(target, "e2e.config.json")));
        Assert.Contains("e2e init apps/web", Output, StringComparison.Ordinal);
        Assert.Contains("Next: cd apps/web, then dotnet test", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quotes_a_directory_the_shell_would_otherwise_split_for_the_platform_it_runs_on()
    {
        var posix = Path.Combine(Dir, "apps", "my web");
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Directory = "apps/my web", Windows = false }, posix)).ExitCode);
        Assert.Contains("Next: cd 'apps/my web', then dotnet test", Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(posix, "e2e.config.json")));

        ClearOutput();
        var windows = Path.Combine(Dir, "my app");
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Directory = "my app", Windows = true }, windows)).ExitCode);
        Assert.Contains("Next: cd \"my app\", then dotnet test", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_directory_argument_that_names_a_file()
    {
        await File.WriteAllTextAsync(Path_("notes.txt"), "");
        Assert.Equal(2, (await RunInitAsync(new InitOptions { Yes = true, Directory = "notes.txt" }, Path_("notes.txt"))).ExitCode);
        Assert.Contains("notes.txt is a file, not a directory", Output, StringComparison.Ordinal);
    }
}
