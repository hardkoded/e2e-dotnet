// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace E2E;

/// <summary>
/// One named agent, as upstream <c>agents.&lt;name&gt;</c>. Each entry starts from the built-in
/// defaults; no agent inherits another's values. A call picks one with its <c>Agent</c> option.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// The model behind <c>act</c>. There is no implicit default. A custom <see cref="Executor"/> that brought a model of its
    /// own supplies it; naming a different one here is <c>INVALID_CONFIG</c>.
    /// </summary>
    public IAgentModel? Model { get; init; }

    /// <summary>
    /// The model that judges <c>assert</c>, <c>waitFor</c>, and <c>extract</c>. Defaults to
    /// <see cref="Model"/>. A judge of its own separates the model that grades a flow from the one that drove it.
    /// </summary>
    public IAgentModel? Judge { get; init; }

    /// <summary>
    /// The custom brain <c>act</c> and <c>assert</c> dispatch to, upstream's <c>executor</c>. Without one, the built-in agent
    /// acts. An executor replaces the built-in agent's prompt and tools, so it cannot be combined with <see cref="System"/> or
    /// <see cref="Tools"/>. It cannot be written in <c>e2e.config.json</c>: an executor is code.
    /// </summary>
    public IStepExecutor? Executor { get; init; }

    /// <summary>
    /// How the acting agent should work, appended to the built-in execution rules. Only the act loop
    /// reads it; the judges never see it.
    /// </summary>
    public string? System { get; init; }

    /// <summary>
    /// What the app calls things, told to every model call this agent makes, act turns and judgments
    /// alike. At most 16384 UTF-8 bytes.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>Committed actions per <c>act</c>, 1 through 100. Null leaves it unset: <see cref="E2EDefaults.MaxSteps"/> applies, and <c>Explore</c> sets its own.</summary>
    public int? MaxSteps { get; init; }

    /// <summary>Model requests per agent call, 1 through 100. Null leaves it unset: <see cref="E2EDefaults.MaxModelCalls"/> applies, and <c>Explore</c> sets its own.</summary>
    public int? MaxModelCalls { get; init; }

    /// <summary>Deadline of one <c>assert</c>, <c>waitFor</c>, or <c>extract</c>.</summary>
    public TimeSpan JudgmentTimeout { get; init; } = E2EDefaults.JudgmentTimeout;

    /// <summary>
    /// Provider options every model call carries, keyed by provider. <see cref="OpenAiCompatibleModel"/>
    /// adds the fields under its <see cref="OpenAiCompatibleModelOptions.Provider"/> key to the request body as given.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>
    /// Project tools offered to the model beside the agent's own, by name. A tool is offered only on the platforms it
    /// declares, and a name the agent already uses is refused.
    /// </summary>
    public IReadOnlyDictionary<string, AgentTool>? Tools { get; init; }
}

/// <summary>An agent with its settings checked and its judge resolved.</summary>
internal sealed class ResolvedAgent
{
    /// <summary>The most bytes <see cref="AgentOptions.Context"/> may hold.</summary>
    public const int MaxContextBytes = 16_384;

    /// <summary>The largest <c>maxSteps</c> and <c>maxModelCalls</c> an agent may configure.</summary>
    public const int MaxBudget = 100;

    public required string Name { get; init; }

    public IAgentModel? Model { get; init; }

    public IAgentModel? Judge { get; init; }

    public string? System { get; init; }

    public string? Context { get; init; }

    /// <summary>The custom brain, or null for the built-in agent.</summary>
    public IStepExecutor? Executor { get; init; }

    public int MaxSteps { get; init; } = E2EDefaults.MaxSteps;

    public int MaxModelCalls { get; init; } = E2EDefaults.MaxModelCalls;

    public TimeSpan JudgmentTimeout { get; init; } = E2EDefaults.JudgmentTimeout;

    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    public IReadOnlyDictionary<string, AgentTool>? Tools { get; init; }

    /// <summary>
    /// This agent with the test's <c>agentContext</c> appended to its own <see cref="Context"/>, one per line. A blank part is
    /// left out. The 16 KiB limit applies to the agent's own context, as upstream's <c>joinAgentContext</c> runs after it.
    /// </summary>
    public ResolvedAgent WithTestContext(string? testContext)
    {
        var parts = new[] { Context, testContext }.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return parts.Length == 0 ? this : new ResolvedAgent
        {
            Name = Name,
            Model = Model,
            Judge = Judge,
            System = System,
            Context = string.Join('\n', parts),
            Executor = Executor,
            MaxSteps = MaxSteps,
            MaxModelCalls = MaxModelCalls,
            JudgmentTimeout = JudgmentTimeout,
            ProviderOptions = ProviderOptions,
            Tools = Tools,
        };
    }

    /// <summary>Checks one agent's settings. Throws <see cref="ConfigurationException"/> with <c>INVALID_CONFIG</c>.</summary>
    public static ResolvedAgent From(string name, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var label = "agents." + name;
        Check(options.MaxSteps ?? E2EDefaults.MaxSteps, label + ".maxSteps");
        Check(options.MaxModelCalls ?? E2EDefaults.MaxModelCalls, label + ".maxModelCalls");
        if (options.JudgmentTimeout <= TimeSpan.Zero)
        {
            throw new ConfigurationException("INVALID_CONFIG", label + ".judgmentTimeout must be a positive integer of milliseconds");
        }

        CheckContext(options.Context, label + ".context");
        CheckProviderOptions(options.ProviderOptions, label);
        CheckTools(options.Tools, label);
        var executor = CheckExecutor(options, label);
        var model = Canonical(options.Model, executor?.Model, label, "model");
        return new ResolvedAgent
        {
            Name = name,
            Model = model,
            Judge = Canonical(options.Judge, executor?.Judge, label, "judge") ?? model,
            Executor = executor,
            System = options.System,
            Context = options.Context,
            MaxSteps = options.MaxSteps ?? E2EDefaults.MaxSteps,
            MaxModelCalls = options.MaxModelCalls ?? E2EDefaults.MaxModelCalls,
            JudgmentTimeout = options.JudgmentTimeout,
            ProviderOptions = options.ProviderOptions,
            Tools = options.Tools,
        };
    }

    // A custom brain replaces the built-in agent, so the options only the built-in agent reads are refused beside it.
    private static IStepExecutor? CheckExecutor(AgentOptions options, string label)
    {
        if (options.Executor is not { } executor)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(executor.Name))
        {
            throw new ConfigurationException("INVALID_CONFIG", label + ".executor must be a StepExecutor: an IStepExecutor with a name, such as ToolLoop.CreateToolLoopExecutor(...)");
        }

        foreach (var (key, set, brings) in new[] { ("system", options.System is not null, "prompt"), ("tools", options.Tools is not null, "tools") })
        {
            if (set)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    label + "." + key + " is an option of the built-in agent, and " + label + ".executor replaces it: a custom executor brings its own " + brings + "; drop " + key + " or drop executor");
            }
        }

        return executor;
    }

    // One model per slot: the model a custom executor brought is it, and without one the entry's own. An entry naming another
    // model than the executor's is refused: two configured models for one slot would split the run silently.
    private static IAgentModel? Canonical(IAgentModel? configured, IAgentModel? brought, string label, string slot)
    {
        if (brought is null)
        {
            return configured;
        }

        if (configured is not null && !ReferenceEquals(configured, brought) && !string.Equals(configured.Name, brought.Name, StringComparison.Ordinal))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                label + "." + slot + " (" + configured.Name + ") and the executor's own " + slot + " (" + brought.Name + ") differ; configure the " + slot + " in one place");
        }

        return brought;
    }

    /// <summary>
    /// The failure for an agent that needs a model and has none: <paramref name="needs"/> names what needs it, and the message
    /// names the entry to write, under that agent's own key.
    /// </summary>
    internal static AgentException MissingModelError(string name, string needs = "the agent fixture requires a model") =>
        new("MODEL_UNAVAILABLE", needs + ", and agents." + name + " has none: set Model on it to an IAgentModel, or model and provider in e2e.config.json");

    internal static void Check(int value, string label)
    {
        if (value is < 1 or > MaxBudget)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                label + " must be an integer from 1 to " + MaxBudget.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal static void CheckContext(string? context, string label)
    {
        if (context is null)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetByteCount(context);
        if (bytes > MaxContextBytes)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                label + " is " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes; the maximum is " + MaxContextBytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal static void CheckTools(IReadOnlyDictionary<string, AgentTool>? tools, string label)
    {
        if (tools is null)
        {
            return;
        }

        foreach (var (name, tool) in tools)
        {
            if (AgentTools.IsReserved(name))
            {
                throw new ConfigurationException("INVALID_CONFIG", label + ".tools." + name + ": the " + name + " tool name is reserved for the agent's own tools");
            }

            if (!tool.IsHarness && AgentTools.HarnessToolNames.TryGetValue(name, out var surface))
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    label + ".tools." + name + ": the " + name + " tool name is reserved for the tool the harness adds in " + surface + "; rename it");
            }
        }
    }

    internal static void CheckProviderOptions(IReadOnlyDictionary<string, JsonElement>? providerOptions, string label)
    {
        if (providerOptions is null)
        {
            return;
        }

        foreach (var pair in providerOptions)
        {
            if (pair.Value.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigurationException("INVALID_CONFIG", label + ".providerOptions." + pair.Key + " must be an object of provider options");
            }
        }
    }
}
