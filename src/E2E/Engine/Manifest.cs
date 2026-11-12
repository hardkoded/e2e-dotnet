// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;

namespace E2E.Engine;

/// <summary>Upstream's <c>keyboard</c> hooks of an engine (<c>engine/index.ts</c>): typing, pressing, and dismissing.</summary>
public sealed record EngineKeyboardSpec
{
    /// <summary>Types text into the focused field; <c>replace</c> clears the field first.</summary>
    public Func<string, bool, OperationContext, Task>? Type { get; init; }

    /// <summary>Presses one key, in the contract's key grammar.</summary>
    public Func<string, OperationContext, Task>? Press { get; init; }

    /// <summary>Dismisses the on-screen keyboard.</summary>
    public Func<OperationContext, Task>? Dismiss { get; init; }
}

/// <summary>Upstream's <c>session</c> hooks of an engine: opening, navigating back, resizing, restarting, and resetting the app.</summary>
public sealed record EngineSessionSpec
{
    public Func<string, OperationContext, Task>? Open { get; init; }

    public Func<OperationContext, Task>? Back { get; init; }

    public Func<ViewportSize, OperationContext, Task>? Resize { get; init; }

    public Func<OperationContext, Task>? Restart { get; init; }

    public Func<OperationContext, Task>? Reset { get; init; }
}

/// <summary>Upstream's <c>state</c> hooks of an engine: capturing and restoring the app's state.</summary>
public sealed record EngineStateSpec
{
    public Func<OperationContext, Task<EngineState>>? Capture { get; init; }

    public Func<EngineState, OperationContext, Task>? Restore { get; init; }
}

/// <summary>Upstream's <c>artifacts</c> hooks of an engine: the screenshot, and video started and stopped together.</summary>
public sealed record EngineArtifactsSpec
{
    public Func<string?, OperationContext, Task<string>>? Screenshot { get; init; }

    public Func<OperationContext, Task>? StartVideo { get; init; }

    public Func<OperationContext, Task<IReadOnlyList<VideoSegment>>>? StopVideo { get; init; }
}

/// <summary>
/// Upstream's <c>Engine</c> (<c>engine/index.ts</c>): what an engine author declares, handed to <see cref="EngineManifest.DefineEngine"/>.
/// Every member is optional; <see cref="EngineManifest.DefineEngine"/> validates the combination and computes the capability set.
/// </summary>
public sealed record EngineSpec
{
    /// <summary>The engine's name, shown in reports and in error messages.</summary>
    public string? Name { get; init; }

    /// <summary>The engine's version, recorded in reports and used to key saved sessions.</summary>
    public string? Version { get; init; }

    /// <summary>The engine contract version the engine was written against; this runner supports <see cref="EngineManifest.SpiVersion"/>.</summary>
    public int? SpiVersion { get; init; }

    /// <summary>The platform the engine drives, when it has one.</summary>
    public string? Platform { get; init; }

    /// <summary>The most workers the engine serves per target.</summary>
    public int? Workers { get; init; }

    /// <summary>Takes one fresh observation of the surface under test.</summary>
    public Func<OperationContext, ObserveOptions?, Task<EngineSnapshot>>? Observe { get; init; }

    /// <summary>Resolves a query against the surface, returning the nodes it matches.</summary>
    public Func<LocatorExpression, OperationContext, Task<IReadOnlyList<EngineNode>>>? Locate { get; init; }

    /// <summary>Performs one locator action on a node of the observation or of a resolution.</summary>
    public Func<NodeRef, LocatorAction, OperationContext, Task>? Perform { get; init; }

    /// <summary>The action kinds <see cref="Perform"/> honors, each once; required with <see cref="Perform"/>.</summary>
    public IReadOnlyList<string>? Actions { get; init; }

    /// <summary>Whether the tap actions accept modifier keys; only with <see cref="Perform"/>.</summary>
    public bool? TapModifiers { get; init; }

    /// <summary>Performs one pointer action at a viewport point, with no node behind it.</summary>
    public Func<Point, PointerAction, OperationContext, Task>? PerformAt { get; init; }

    /// <summary>The pointer action kinds <see cref="PerformAt"/> honors, each once; required with <see cref="PerformAt"/>.</summary>
    public IReadOnlyList<string>? PointerActions { get; init; }

    /// <summary>Input to the focused field; needs <see cref="Observe"/>.</summary>
    public EngineKeyboardSpec? Keyboard { get; init; }

    /// <summary>The app's session hooks.</summary>
    public EngineSessionSpec? Session { get; init; }

    /// <summary>The app's state hooks.</summary>
    public EngineStateSpec? State { get; init; }

    /// <summary>The app's artifact hooks.</summary>
    public EngineArtifactsSpec? Artifacts { get; init; }

    /// <summary>The secrets the engine's options hold, as handles; each name is kept once.</summary>
    public IReadOnlyList<Secret>? Secrets { get; init; }

    /// <summary>Checks the app a target declares against what the engine can drive, at config load.</summary>
    public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp { get; init; }

    /// <summary>Prepares the engine for a run.</summary>
    public Func<EnginePrepareInfo, Task>? Prepare { get; init; }

    /// <summary>Finishes a run.</summary>
    public Func<EngineFinishInfo, Task>? Finish { get; init; }

    /// <summary>Reports the engine's readiness checks.</summary>
    public Func<EngineDoctorInfo, Task<IReadOnlyList<EngineDoctorCheck>>>? Doctor { get; init; }

    /// <summary>Initializes a worker.</summary>
    public Func<EngineInitInfo, Task>? Init { get; init; }

    /// <summary>Starts an attempt on the engine.</summary>
    public Func<OperationContext, Task>? StartAttempt { get; init; }

    /// <summary>Settles an attempt, rethrowing a failure no step awaited.</summary>
    public Func<EngineCleanupContext, Task>? SettleAttempt { get; init; }

    /// <summary>Ends an attempt's isolation, within the cleanup budget.</summary>
    public Func<EngineCleanupContext, Task>? EndAttempt { get; init; }

    /// <summary>Disposes the engine's worker.</summary>
    public Func<EngineCleanupContext, Task>? Dispose { get; init; }

    /// <summary>
    /// Fixture factories, by name. A name is lower-camel and not one a universal fixture uses (<c>agent</c>, <c>app</c>, <c>screen</c>,
    /// <c>platform</c>, <c>session</c>).
    /// </summary>
    public IReadOnlyDictionary<string, Delegate>? Fixtures { get; init; }
}

/// <summary>
/// Upstream's engine handle (<c>defineEngine</c>'s result, <c>engine/manifest.ts</c>): the validated engine with its capability set. A handle
/// is only built by <see cref="EngineManifest.DefineEngine"/>.
/// </summary>
public sealed class EngineHandle
{
    internal EngineHandle(EngineSpec spec, IReadOnlySet<string> capabilities)
    {
        Spec = spec;
        Capabilities = capabilities;
    }

    /// <summary>The spec this handle was built from, which every hook is called on.</summary>
    internal EngineSpec Spec { get; }

    public string Name => Spec.Name!;

    public string Version => Spec.Version!;

    public int SpiVersion => Spec.SpiVersion!.Value;

    public string? Platform => Spec.Platform;

    public int? Workers => Spec.Workers;

    /// <summary>The capabilities the spec declares: <c>observation</c>, <c>actions</c>, <c>location</c>, <c>pointer</c>, <c>keyboard</c>, <c>state</c>, <c>artifacts</c>, and each fixture name.</summary>
    public IReadOnlySet<string> Capabilities { get; }

    /// <summary>The action kinds <see cref="Perform"/> honors, or null without the actions capability.</summary>
    public IReadOnlyList<string>? Actions => Spec.Actions;

    /// <summary>Whether the tap actions take modifier keys; null when the spec does not declare it.</summary>
    public bool? TapModifiers => Spec.TapModifiers == true ? true : null;

    /// <summary>The pointer action kinds <see cref="PerformAt"/> honors, or null without the pointer capability.</summary>
    public IReadOnlyList<string>? PointerActions => Spec.PointerActions;

    public Func<OperationContext, ObserveOptions?, Task<EngineSnapshot>>? Observe => Spec.Observe;

    public Func<LocatorExpression, OperationContext, Task<IReadOnlyList<EngineNode>>>? Locate => Spec.Locate;

    public Func<NodeRef, LocatorAction, OperationContext, Task>? Perform => Spec.Perform;

    public Func<Point, PointerAction, OperationContext, Task>? PerformAt => Spec.PerformAt;

    public EngineKeyboardSpec? Keyboard => Spec.Keyboard;

    public EngineSessionSpec? Session => Spec.Session;

    public EngineStateSpec? State => Spec.State;

    public EngineArtifactsSpec? Artifacts => Spec.Artifacts;

    /// <summary>The secrets the engine holds, each name once; null when the spec declares none.</summary>
    public IReadOnlyList<Secret>? Secrets => Spec.Secrets;

    public IReadOnlyDictionary<string, Delegate>? Fixtures => Spec.Fixtures;

    public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp => Spec.ValidateApp;

    public Func<EnginePrepareInfo, Task>? Prepare => Spec.Prepare;

    public Func<EngineFinishInfo, Task>? Finish => Spec.Finish;

    public Func<EngineDoctorInfo, Task<IReadOnlyList<EngineDoctorCheck>>>? Doctor => Spec.Doctor;

    public Func<EngineInitInfo, Task>? Init => Spec.Init;

    public Func<OperationContext, Task>? StartAttempt => Spec.StartAttempt;

    public Func<EngineCleanupContext, Task>? SettleAttempt => Spec.SettleAttempt;

    public Func<EngineCleanupContext, Task>? EndAttempt => Spec.EndAttempt;

    public Func<EngineCleanupContext, Task>? Dispose => Spec.Dispose;
}

/// <summary>
/// Upstream's <c>defineEngine</c> and <c>isEngineHandle</c> (<c>engine/manifest.ts</c>). The engine manifest: <see cref="DefineEngine"/> validates an
/// engine at config load, synchronously and loudly, and computes its capability set. A combination the contract does not allow is
/// <c>INVALID_CONFIG</c>, never a silent demotion to a lower tier. .NET-only holder name: C# has no free functions.
/// </summary>
public static partial class EngineManifest
{
    /// <summary>The engine contract version this runner supports (upstream's <c>ENGINE_SPI_VERSION</c>).</summary>
    public const int SpiVersion = 1;

    /// <summary>Fixture names an engine may not shadow: the universal fixtures.</summary>
    private static readonly HashSet<string> ReservedFixtures = ["agent", "app", "screen", "platform", "session"];

    /// <summary>A fixture name is lower-camel: a lower-case letter, then letters and digits.</summary>
    [GeneratedRegex("^[a-z][A-Za-z0-9]*$")]
    private static partial Regex FixtureNamePattern();

    /// <summary>
    /// Validates <paramref name="spec"/> and computes the engine's capability set. Throws <c>INVALID_CONFIG</c> for a declared combination the
    /// contract does not allow, naming the engine.
    /// </summary>
    public static EngineHandle DefineEngine(EngineSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            throw new ConfigurationException("INVALID_CONFIG", "engine.name must be a non-empty string");
        }

        var name = spec.Name;
        if (string.IsNullOrWhiteSpace(spec.Version))
        {
            throw Invalid(name, "version must be a non-empty string; it is recorded in reports and keys saved sessions");
        }

        if (spec.SpiVersion != SpiVersion)
        {
            throw Invalid(name, $"declares spiVersion {spec.SpiVersion?.ToString() ?? "undefined"}; this runner supports {SpiVersion}");
        }

        if (spec.Platform is not null && string.IsNullOrWhiteSpace(spec.Platform))
        {
            throw Invalid(name, "platform must be a non-empty string when declared");
        }

        if (spec.Workers is { } workers && workers < 1)
        {
            throw Invalid(name, "workers must be a positive safe integer: the most workers the engine serves per target");
        }

        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        if (spec.Observe is not null)
        {
            capabilities.Add("observation");
        }

        if (spec.Perform is not null)
        {
            if (!capabilities.Contains("observation"))
            {
                throw Invalid(name, "declares perform without observe: action targets are observation refs");
            }

            if (spec.Actions is null)
            {
                throw Invalid(name, "declares perform without actions: list the action kinds the surface honors");
            }

            capabilities.Add("actions");
        }
        else if (spec.Actions is not null)
        {
            throw Invalid(name, "declares actions without perform");
        }

        if (spec.Locate is not null)
        {
            if (!capabilities.Contains("observation"))
            {
                throw Invalid(name, "declares locate without observe: located nodes share the observation id space");
            }

            capabilities.Add("location");
        }

        if (spec.PerformAt is not null)
        {
            if (!capabilities.Contains("observation"))
            {
                throw Invalid(name, "declares performAt without observe: a point is read off the observation pixels");
            }

            if (spec.PointerActions is null)
            {
                throw Invalid(name, "declares performAt without pointerActions: list the pointer action kinds the surface honors");
            }

            capabilities.Add("pointer");
        }
        else if (spec.PointerActions is not null)
        {
            throw Invalid(name, "declares pointerActions without performAt");
        }

        if (spec.TapModifiers == true && !capabilities.Contains("actions"))
        {
            throw Invalid(name, "declares tapModifiers without perform: modifiers ride the tap actions perform receives");
        }

        if (spec.Keyboard is not null && !capabilities.Contains("observation"))
        {
            throw Invalid(name, "declares keyboard without observe: the focused field is read off the observation");
        }

        // Declared kind lists are copied and checked, so the handle keeps the list the engine declared and nothing else.
        var actions = spec.Actions is null ? null : DeclaredKinds(name, "actions", spec.Actions, EngineActionKinds.Locator);
        var pointerActions = spec.PointerActions is null ? null : DeclaredKinds(name, "pointerActions", spec.PointerActions, EngineActionKinds.Pointer);
        var declared = spec with { Actions = actions, PointerActions = pointerActions };

        if (spec.Fixtures is not null)
        {
            foreach (var fixture in spec.Fixtures.Keys)
            {
                if (!FixtureNamePattern().IsMatch(fixture))
                {
                    throw Invalid(name, $"fixture name \"{fixture}\" must be a lower-camel identifier");
                }

                if (ReservedFixtures.Contains(fixture))
                {
                    throw Invalid(name, $"fixture name \"{fixture}\" shadows a universal fixture");
                }

                capabilities.Add(fixture);
            }
        }

        if (spec.Keyboard is { } keyboard)
        {
            if (keyboard.Type is null)
            {
                throw Invalid(name, "keyboard.type must be a function");
            }

            if (keyboard.Press is null)
            {
                throw Invalid(name, "keyboard.press must be a function");
            }

            capabilities.Add("keyboard");
        }

        if (spec.State is { } state)
        {
            if (state.Capture is null)
            {
                throw Invalid(name, "state.capture must be a function");
            }

            if (state.Restore is null)
            {
                throw Invalid(name, "state.restore must be a function");
            }

            capabilities.Add("state");
        }

        if (spec.Artifacts is { } artifacts)
        {
            if (artifacts.Screenshot is null)
            {
                throw Invalid(name, "artifacts.screenshot must be a function");
            }

            if ((artifacts.StartVideo is null) != (artifacts.StopVideo is null))
            {
                throw Invalid(name, "artifacts.startVideo and stopVideo must be declared together");
            }

            capabilities.Add("artifacts");
        }

        if (spec.Secrets is not null)
        {
            // Each name is kept once, so the handle lists every secret the engine holds exactly once.
            declared = declared with { Secrets = [.. spec.Secrets.DistinctBy(secret => secret.Name)] };
        }

        return new EngineHandle(declared, capabilities);
    }

    /// <summary>True when <paramref name="value"/> is an engine handle built by <see cref="DefineEngine"/>.</summary>
    public static bool IsEngineHandle(object? value) => value is EngineHandle;

    /// <summary>Validates a declared kind list (<c>actions</c>, <c>pointerActions</c>): a non-empty list of known kinds, each once.</summary>
    private static IReadOnlyList<string> DeclaredKinds(string name, string member, IReadOnlyList<string> value, IReadOnlyList<string> known)
    {
        if (value.Count == 0)
        {
            throw Invalid(name, $"{member} must be a non-empty array of action kinds ({string.Join(", ", known)})");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in value)
        {
            if (!known.Contains(kind))
            {
                throw Invalid(name, $"{member} names unknown kind \"{kind}\"; expected one of {string.Join(", ", known)}");
            }

            if (!seen.Add(kind))
            {
                throw Invalid(name, $"{member} lists \"{kind}\" twice");
            }
        }

        return value.ToArray();
    }

    private static ConfigurationException Invalid(string name, string detail) => new("INVALID_CONFIG", $"engine \"{name}\": {detail}");
}
