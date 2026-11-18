// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Decision;

/// <summary>The gates every answer passes: the selected option's probability and the provider's confidence (upstream's <c>Gates</c>).</summary>
public sealed record Gates(double MinProbability, double MinConfidence);

/// <summary>
/// What a target question settled (upstream's <c>Chosen</c>): the offered value, with the answer that chose it when one was asked, or why the
/// step cannot go on (<see cref="Blocked"/>).
/// </summary>
public sealed class Chosen<T>
{
    public string? Key { get; init; }

    public T? Value { get; init; }

    public Decision? Answer { get; init; }

    /// <summary>Why the step cannot go on, when it cannot.</summary>
    public string? Blocked { get; init; }
}

/// <summary>What a verdict question can settle on (upstream's <c>Verdict</c>).</summary>
public enum Verdict
{
    Holds,
    Fails,
    Inconclusive,

    /// <summary>The answer failed the gates (upstream's <c>'below gate'</c> string).</summary>
    BelowGate,
}

/// <summary>Upstream's <c>picks.ts</c>.</summary>
public static class Picks
{
    /// <summary>
    /// Upstream's <c>pick</c>. Resolves a target answer against what was offered. A lone offer needs no answer: the request never asked. The
    /// model's <c>none</c>, and an answer under the gates, block the step. Anything the request never offered is <c>MODEL_OUTPUT_INVALID</c>:
    /// our validation, never the provider's text.
    /// </summary>
    public static Chosen<T> Pick<T>(
        Decision? answer,
        IReadOnlyDictionary<string, T> offered,
        Gates gates,
        string what,
        string operation
    )
    {
        ArgumentNullException.ThrowIfNull(offered);
        if (answer is null)
        {
            if (offered.Count == 1)
            {
                var (key, value) = offered.First();
                return new Chosen<T> { Key = key, Value = value };
            }

            throw Invalid("The decision model returned no " + what + " answer.");
        }

        if (answer.Choice == Questions.None)
        {
            return new Chosen<T>
            {
                Blocked = "The decision model chose "
                    + operation
                    + " but no "
                    + what
                    + " for it ("
                    + Describe(answer)
                    + ").",
            };
        }

        if (!offered.TryGetValue(answer.Choice, out var chosen))
        {
            throw Invalid("The decision model chose an unavailable " + what + ".");
        }

        if (!Gated(answer, gates))
        {
            return new Chosen<T> { Blocked = "The " + what + " is uncertain (" + Describe(answer) + ")." };
        }

        return new Chosen<T>
        {
            Key = answer.Choice,
            Value = chosen,
            Answer = answer,
        };
    }

    /// <summary>Upstream's <c>verdictOf</c>. The verdict answer, typed, or <see cref="Verdict.BelowGate"/> when it fails the gates; a verdict never offered is <c>MODEL_OUTPUT_INVALID</c>.</summary>
    public static Verdict VerdictOf(Decision? answer, Gates gates)
    {
        var verdict = Need(answer, "verdict");
        var settled = verdict.Choice switch
        {
            "holds" => Verdict.Holds,
            "fails" => Verdict.Fails,
            "inconclusive" => Verdict.Inconclusive,
            _ => throw Invalid("The decision model chose an unavailable verdict."),
        };
        return Gated(verdict, gates) ? settled : Verdict.BelowGate;
    }

    /// <summary>Upstream's <c>invalid</c>. A model answer the request never offered: our validation, never the provider's text.</summary>
    public static AgentException Invalid(string message) => new("MODEL_OUTPUT_INVALID", message);

    /// <summary>Upstream's <c>need</c>. Requires an answer the model returned; its absence is our bug or a broken transport.</summary>
    public static Decision Need(Decision? answer, string what) =>
        answer ?? throw Invalid("The decision model returned no " + what + " answer.");

    /// <summary>Upstream's <c>gated</c>.</summary>
    public static bool Gated(Decision decision, Gates gates)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(gates);
        return decision.Probability >= gates.MinProbability && decision.Confidence >= gates.MinConfidence;
    }

    /// <summary>Upstream's <c>describe</c>. How an answer reads in a verdict or a turn: <c>p=0.950, confidence 0.930</c>.</summary>
    public static string Describe(Decision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return "p="
            + decision.Probability.ToString("F3", CultureInfo.InvariantCulture)
            + ", confidence "
            + decision.Confidence.ToString("F3", CultureInfo.InvariantCulture);
    }
}
