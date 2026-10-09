// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>
/// Every secret value this process knows: the secrets of each config a session loaded. What the server returns
/// or logs passes through <see cref="Redact"/> before it leaves, so a value in an error message or a log line
/// reaches the client and the operator by its name.
/// </summary>
internal sealed class SecretLedger
{
    private readonly object _gate = new();
    private readonly List<Secret> _secrets = [];
    private Redactor _redactor = Redactor.None;

    /// <summary>Registers secrets; one registered already is kept once.</summary>
    public void Add(IEnumerable<Secret> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        lock (_gate)
        {
            var before = _secrets.Count;
            foreach (var secret in secrets)
            {
                if (!_secrets.Any(known => known.Name == secret.Name && known.Value == secret.Value))
                {
                    _secrets.Add(secret);
                }
            }

            if (_secrets.Count != before)
            {
                _redactor = Redactor.For(_secrets);
            }
        }
    }

    /// <summary>The text with every known secret value replaced by its marker.</summary>
    public string Redact(string text)
    {
        Redactor redactor;
        lock (_gate)
        {
            redactor = _redactor;
        }

        return redactor.Redact(text);
    }
}
