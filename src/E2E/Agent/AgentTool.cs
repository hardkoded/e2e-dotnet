// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// A project tool the agent offers to the model beside its own: upstream's <c>defineTool</c>. It declares whether it
/// changes the application and which platforms it is offered on; the agent owns dispatch and accounting. The tool's
/// name is its key in <see cref="AgentOptions.Tools"/>.
/// </summary>
public sealed class AgentTool
{
    private readonly Func<JsonObject, AgentToolContext, CancellationToken, Task<string>> _run;

    /// <param name="description">What the model is told the tool does.</param>
    /// <param name="inputSchema">The JSON schema of the tool's input.</param>
    /// <param name="mutates">Whether running the tool can change the application. A mutating tool takes an action slot and ends replay of the step.</param>
    /// <param name="executeAsync">Runs the tool with its input as the model wrote it, and answers what the model reads.</param>
    /// <param name="platforms">The engine platforms the tool is offered on. Null means every platform.</param>
    public AgentTool(
        string description,
        JsonObject inputSchema,
        bool mutates,
        Func<JsonObject, CancellationToken, Task<string>> executeAsync,
        IReadOnlyList<string>? platforms = null)
        : this(description, inputSchema, mutates, Ignoring(executeAsync), platforms)
    {
    }

    /// <summary>
    /// A tool that reaches the harness through <see cref="AgentToolContext"/>: upstream's <c>getToolContext</c>. It can look at
    /// the screen (a read-only tool only) and keep what it saw as a screenshot of the step.
    /// </summary>
    /// <param name="description">What the model is told the tool does.</param>
    /// <param name="inputSchema">The JSON schema of the tool's input.</param>
    /// <param name="mutates">Whether running the tool can change the application. A mutating tool takes an action slot and ends replay of the step.</param>
    /// <param name="executeAsync">Runs the tool with its input as the model wrote it and the context of the step, and answers what the model reads.</param>
    /// <param name="platforms">The engine platforms the tool is offered on. Null means every platform.</param>
    public AgentTool(
        string description,
        JsonObject inputSchema,
        bool mutates,
        Func<JsonObject, AgentToolContext, CancellationToken, Task<string>> executeAsync,
        IReadOnlyList<string>? platforms = null)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(inputSchema);
        ArgumentNullException.ThrowIfNull(executeAsync);
        if (platforms is not null && (platforms.Count == 0 || platforms.Any(string.IsNullOrEmpty)))
        {
            throw new TestException("INVALID_ARGUMENT", "platforms must be a non-empty list of platform names when present");
        }

        Description = description;
        InputSchema = inputSchema;
        Mutates = mutates;
        _run = executeAsync;
        Platforms = platforms?.ToArray();
    }

    public string Description { get; }

    public JsonObject InputSchema { get; }

    public bool Mutates { get; }

    /// <summary>True for a tool the harness adds itself, such as the finding tool of <see cref="Explore"/>, which may take a name no project tool can.</summary>
    internal bool IsHarness { get; init; }

    /// <summary>The platforms the tool is offered on. Null means every platform.</summary>
    public IReadOnlyList<string>? Platforms { get; }

    /// <summary>Runs the tool with no step around it. A tool that reads its <see cref="AgentToolContext"/> fails there.</summary>
    public Func<JsonObject, CancellationToken, Task<string>> ExecuteAsync => (input, cancellationToken) => _run(input, AgentToolContext.Unavailable, cancellationToken);

    /// <summary>True when the tool is offered on <paramref name="platform"/>.</summary>
    public bool AppliesTo(string platform) => Platforms is null || Platforms.Contains(platform, StringComparer.Ordinal);

    internal Task<string> RunAsync(JsonObject input, AgentToolContext context, CancellationToken cancellationToken) => _run(input, context, cancellationToken);

    private static Func<JsonObject, AgentToolContext, CancellationToken, Task<string>> Ignoring(Func<JsonObject, CancellationToken, Task<string>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        return (input, _, cancellationToken) => executeAsync(input, cancellationToken);
    }
}

/// <summary>
/// What a project tool reaches of the step it runs in (upstream's <c>ToolContext</c>): a look at the screen, and a screenshot kept
/// as evidence of the step. A tool that changes the app may not look: it observes in a separate call.
/// </summary>
public sealed class AgentToolContext
{
    private readonly StepExecutorContext? _context;
    private readonly bool _mutates;

    /// <param name="context">The context of the step the tool runs in.</param>
    /// <param name="mutates">Whether the tool changes the app. Such a tool cannot look at the screen.</param>
    public AgentToolContext(StepExecutorContext context, bool mutates)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _mutates = mutates;
    }

    private AgentToolContext()
    {
    }

    /// <summary>The context of a tool run outside a step: every call fails.</summary>
    internal static AgentToolContext Unavailable { get; } = new();

    /// <summary>Upstream's <c>observe</c>. A mutating tool is refused with <c>POLICY_DENIED</c>.</summary>
    public Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_mutates)
        {
            throw new AgentException("POLICY_DENIED", "only read-only tools may request observations; observe in a separate tool call", blocked: true);
        }

        return Context.ObserveAsync(options, cancellationToken);
    }

    /// <summary>Upstream's <c>attachScreenshot</c>: keeps pixels the tool saw as a screenshot of the step, and returns where it landed.</summary>
    public Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default) =>
        Context.AttachScreenshotAsync(pixels, label, cancellationToken);

    private StepExecutorContext Context =>
        _context ?? throw new TestException("INVALID_ARGUMENT", "this tool needs the observation context the built-in agent supplies");
}
