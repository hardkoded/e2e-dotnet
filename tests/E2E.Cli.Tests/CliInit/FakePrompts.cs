// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliInit;

/// <summary>A <see cref="IInitPrompts.Select"/> question as it was asked.</summary>
internal sealed record SelectCall(string Message, IReadOnlyList<PromptOption> Options, string InitialValue);

/// <summary>A <see cref="IInitPrompts.Text"/> question as it was asked.</summary>
internal sealed record TextCall(string Message, string Placeholder, Func<string?, string?> Validate);

/// <summary>A <see cref="IInitPrompts.Confirm"/> question as it was asked.</summary>
internal sealed record ConfirmCall(string Message, bool InitialValue);

/// <summary>
/// .NET-only test double standing for upstream's <c>vi.mock('@clack/prompts')</c> and its <c>beforeEach</c> in
/// <c>cli-init.test.ts</c>: what each multiselect question answers (<see cref="Engines"/>, <see cref="Skills"/>,
/// <see cref="Mcp"/>, a <see langword="null"/> being the cancel symbol), the answers queued for the next select, text, and
/// confirm questions (<c>mockResolvedValueOnce</c>), the defaults after them, and every question asked.
/// </summary>
internal sealed class FakePrompts : IInitPrompts
{
    private readonly Queue<string?> _selects = new();
    private readonly Queue<string?> _texts = new();
    private readonly Queue<bool?> _confirms = new();

    /// <summary>What the engines question answers; null cancels.</summary>
    public IReadOnlyList<string>? Engines { get; set; } = [];

    /// <summary>What the skill question answers; null cancels.</summary>
    public IReadOnlyList<string>? Skills { get; set; } = [".agents/skills", ".claude/skills"];

    /// <summary>What the MCP question answers; null cancels.</summary>
    public IReadOnlyList<string>? Mcp { get; set; } = [".mcp.json", ".cursor/mcp.json"];

    public List<string> MultiSelectCalls { get; } = [];

    public List<SelectCall> SelectCalls { get; } = [];

    public List<TextCall> TextCalls { get; } = [];

    public List<ConfirmCall> ConfirmCalls { get; } = [];

    /// <summary>Queues the answer of the next select question; null cancels.</summary>
    public void NextSelect(string? answer) => _selects.Enqueue(answer);

    /// <summary>Queues the answer of the next text question; null cancels.</summary>
    public void NextText(string? answer) => _texts.Enqueue(answer);

    /// <summary>Queues the answers of the next confirm questions; null cancels.</summary>
    public void NextConfirm(params bool?[] answers)
    {
        foreach (var answer in answers)
        {
            _confirms.Enqueue(answer);
        }
    }

    public IReadOnlyList<string>? MultiSelect(string message, IReadOnlyList<PromptOption> options, IReadOnlyList<string> initialValues)
    {
        MultiSelectCalls.Add(message);
        return message.Contains("MCP", StringComparison.Ordinal) ? Mcp : message.Contains("Which engines", StringComparison.Ordinal) ? Engines : Skills;
    }

    public string? Select(string message, IReadOnlyList<PromptOption> options, string initialValue)
    {
        SelectCalls.Add(new SelectCall(message, options, initialValue));
        return _selects.Count > 0 ? _selects.Dequeue() : "none";
    }

    public string? Text(string message, string placeholder, Func<string?, string?> validate)
    {
        TextCalls.Add(new TextCall(message, placeholder, validate));
        return _texts.Count > 0 ? _texts.Dequeue() : "http://127.0.0.1:11434/v1";
    }

    public bool? Confirm(string message, bool initialValue = true)
    {
        ConfirmCalls.Add(new ConfirmCall(message, initialValue));
        return _confirms.Count > 0 ? _confirms.Dequeue() : false;
    }
}
