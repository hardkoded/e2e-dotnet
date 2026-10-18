// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// What an engine says an attempt runs on, such as the browser and its user agent (upstream <c>createEnvironmentFacts</c> in
/// <c>engine/session.ts</c>). A name the engine tells again overwrites the earlier value. At most <see cref="MaxFacts"/> names are
/// kept, and each is redacted, then clipped, when it is read.
/// </summary>
internal sealed class EnvironmentFacts
{
    /// <summary>Facts the session keeps.</summary>
    internal const int MaxFacts = 8;

    /// <summary>Characters of a fact's name once redacted.</summary>
    internal const int MaxKeyChars = 40;

    /// <summary>Characters of a fact's value once redacted.</summary>
    internal const int MaxValueChars = 200;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _facts = new(StringComparer.Ordinal);

    /// <summary>Merges what the engine told: blank names and values are dropped.</summary>
    public void Push(IReadOnlyDictionary<string, string> next)
    {
        lock (_gate)
        {
            foreach (var (key, value) in next)
            {
                // Held whole, however long: a cut before redaction could leave a secret's head behind.
                var name = key.Trim();
                if (name.Length == 0 || string.IsNullOrWhiteSpace(value) || (!_facts.ContainsKey(name) && _facts.Count >= MaxFacts))
                {
                    continue;
                }

                _facts[name] = TextRules.Normalize(value);
            }
        }
    }

    /// <summary>The facts, redacted before they are clipped so no clip leaves the head of a secret behind; null when the engine told none.</summary>
    public IReadOnlyDictionary<string, string>? Read(Func<string, string> redact)
    {
        lock (_gate)
        {
            if (_facts.Count == 0)
            {
                return null;
            }

            // Two names that redact or clip to the same text keep the later value.
            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in _facts)
            {
                facts[TextRules.Bound(redact(name), MaxKeyChars)] = TextRules.Bound(redact(value), MaxValueChars);
            }

            return facts;
        }
    }
}
