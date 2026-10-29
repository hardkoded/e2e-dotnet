// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Cli;

/// <summary>One choice of a <see cref="IInitPrompts.MultiSelect"/> or <see cref="IInitPrompts.Select"/> question (the <c>{ value, label, hint }</c> options of <c>@clack/prompts</c>).</summary>
/// <param name="Value">The answer the choice stands for.</param>
/// <param name="Label">What the list shows.</param>
/// <param name="Hint">A few words beside the label.</param>
internal sealed record PromptOption(string Value, string Label, string? Hint = null);

/// <summary>
/// .NET-only: the questions <c>e2e init</c> asks, standing for the <c>@clack/prompts</c> calls of upstream's <c>cli/init.ts</c>
/// (<c>multiselect</c>, <c>select</c>, <c>text</c>, <c>confirm</c>). Each one answers <see langword="null"/> where clack
/// answers its cancel symbol, so <c>isCancelled</c> is a null check. The seam is also how a test answers without a terminal.
/// </summary>
internal interface IInitPrompts
{
    /// <summary>Asks for any number of the options; <paramref name="initialValues"/> are the ones chosen to start with.</summary>
    IReadOnlyList<string>? MultiSelect(string message, IReadOnlyList<PromptOption> options, IReadOnlyList<string> initialValues);

    /// <summary>Asks for one of the options; <paramref name="initialValue"/> is the one chosen to start with.</summary>
    string? Select(string message, IReadOnlyList<PromptOption> options, string initialValue);

    /// <summary>Asks for a line of text, asking again while <paramref name="validate"/> returns a reason.</summary>
    string? Text(string message, string placeholder, Func<string?, string?> validate);

    /// <summary>Asks yes or no; <paramref name="initialValue"/> is the answer an empty reply takes.</summary>
    bool? Confirm(string message, bool initialValue = true);
}

/// <summary>
/// .NET-only: <see cref="IInitPrompts"/> on a text terminal, in place of <c>@clack/prompts</c>. A question prints its choices
/// and reads a line; an empty line takes the initial answer, and the end of the input, or <c>q</c>, cancels.
/// </summary>
internal sealed class ConsolePrompts(TextReader input, TextWriter output) : IInitPrompts
{
    public IReadOnlyList<string>? MultiSelect(string message, IReadOnlyList<PromptOption> options, IReadOnlyList<string> initialValues)
    {
        while (true)
        {
            output.WriteLine(Program.Clean(message));
            for (var i = 0; i < options.Count; i++)
            {
                output.WriteLine("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ") [" + (initialValues.Contains(options[i].Value) ? "x" : " ") + "] " + Describe(options[i]));
            }

            output.Write("Numbers or names separated by commas, Enter for the marked ones, none for none, q to cancel: ");
            var line = input.ReadLine()?.Trim();
            if (line is null || line.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (line == "")
            {
                return [.. options.Select(option => option.Value).Where(initialValues.Contains)];
            }

            if (line.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var chosen = new List<string>();
            var known = true;
            foreach (var word in line.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                var option = Find(options, word);
                if (option is null)
                {
                    output.WriteLine("Not one of the options: " + Program.Clean(word));
                    known = false;
                    break;
                }

                chosen.Add(option.Value);
            }

            if (known)
            {
                // List order, whatever order they were typed in.
                return [.. options.Select(option => option.Value).Where(chosen.Contains)];
            }
        }
    }

    public string? Select(string message, IReadOnlyList<PromptOption> options, string initialValue)
    {
        while (true)
        {
            output.WriteLine(Program.Clean(message));
            for (var i = 0; i < options.Count; i++)
            {
                output.WriteLine("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ") " + Describe(options[i]) + (options[i].Value == initialValue ? " (default)" : ""));
            }

            output.Write("Number or name, Enter for the default, q to cancel: ");
            var line = input.ReadLine()?.Trim();
            if (line is null || line.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (line == "")
            {
                return initialValue;
            }

            if (Find(options, line) is { } option)
            {
                return option.Value;
            }

            output.WriteLine("Not one of the options: " + Program.Clean(line));
        }
    }

    public string? Text(string message, string placeholder, Func<string?, string?> validate)
    {
        while (true)
        {
            output.Write(Program.Clean(message) + " (" + Program.Clean(placeholder) + ", q to cancel): ");
            var line = input.ReadLine();
            if (line is null || line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var problem = validate(line);
            if (problem is null)
            {
                return line;
            }

            output.WriteLine(Program.Clean(problem));
        }
    }

    public bool? Confirm(string message, bool initialValue = true)
    {
        while (true)
        {
            output.Write(Program.Clean(message) + (initialValue ? " [Y/n, q to cancel]: " : " [y/N, q to cancel]: "));
            var line = input.ReadLine()?.Trim();
            if (line is null || line.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (line == "")
            {
                return initialValue;
            }

            if (line.Equals("y", StringComparison.OrdinalIgnoreCase) || line.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (line.Equals("n", StringComparison.OrdinalIgnoreCase) || line.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            output.WriteLine("Answer y or n");
        }
    }

    private static string Describe(PromptOption option) =>
        Program.Clean(option.Label == option.Value ? option.Label : option.Label + " (" + option.Value + ")") + (option.Hint is null ? "" : ": " + Program.Clean(option.Hint));

    private static PromptOption? Find(IReadOnlyList<PromptOption> options, string word) =>
        int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= options.Count
            ? options[number - 1]
            : options.FirstOrDefault(option => option.Value.Equals(word, StringComparison.OrdinalIgnoreCase));
}
