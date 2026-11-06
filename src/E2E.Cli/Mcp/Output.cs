// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>Which of the process's standard streams user code wrote to.</summary>
internal enum OutputStream
{
    Stdout,
    Stderr,
}

/// <summary>
/// What the <c>e2e mcp</c> process prints besides the protocol. User code runs in this process (a test the
/// server runs, engines), and what it writes to stdout or stderr reaches the operator's stderr redacted with
/// every secret value the process knows, as a test's output leaves an <c>e2e run</c> worker. The server's own
/// diagnostics are redacted where they are written and pass as they are. Upstream's <c>McpOutput</c>.
/// </summary>
internal sealed class McpOutput
{
    private readonly Action<string> _sink;
    private readonly StreamRedactor _redactor;
    private readonly Lock _gate = new();

    /// <summary>Config loads in flight: until each ends, what anyone prints is held.</summary>
    private int _loading;

    /// <summary>Whether a load in flight failed, so its secrets are unknown and what is held cannot be redacted.</summary>
    private bool _loadFailed;

    /// <summary>Tool calls in flight: a held tail is released only once none is, since any of them may be partway through a value.</summary>
    private int _calls;

    private List<string> _held = [];

    /// <summary>Whether the last text passed on ended its line.</summary>
    private bool _atLineStart = true;

    /// <summary>Prints to <paramref name="sink"/>, the operator's stderr, redacting with <paramref name="ledger"/>. Both streams share one redactor: they land on one stderr, where a value split across them reads whole.</summary>
    public McpOutput(Action<string> sink, SecretLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(ledger);
        _sink = sink;
        _redactor = new StreamRedactor(ledger);
    }

    /// <summary>
    /// One write of user code, redacted across writes: the tail a later write could complete into a value waits for
    /// it, or for the end of the tool calls in flight. Held while a config loads.
    /// </summary>
    public void Write(OutputStream stream, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _ = stream;
        lock (_gate)
        {
            if (_loading > 0)
            {
                _held.Add(text);
                return;
            }

            Emit(_redactor.Push(text));
        }
    }

    /// <summary>One line of the server's own, already redacted where it was written; on a line of its own even when user code left one unfinished.</summary>
    public void Log(string line)
    {
        lock (_gate)
        {
            Emit((_atLineStart ? "" : "\n") + line + "\n");
        }
    }

    /// <summary>A line of the server's that carries user text (an error nobody caught): redacted and held like what user code writes.</summary>
    public void Report(string line)
    {
        lock (_gate)
        {
            Write(OutputStream.Stderr, (_atLineStart ? "" : "\n") + line + "\n");
        }
    }

    /// <summary>
    /// Runs <paramref name="load"/>, a config load that registers the config's secrets, holding what anyone prints
    /// until it ends: the load may print a value before the process knows it is one. Released redacted once every
    /// load in flight succeeded; withheld when one failed, since the secrets of a config that did not load are unknown.
    /// </summary>
    public async Task<T> WithholdDuringAsync<T>(Func<Task<T>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        lock (_gate)
        {
            _loading++;
        }

        try
        {
            return await load().ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _loadFailed = true;
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                _loading--;
                if (_loading == 0)
                {
                    ReleaseHeld();
                }
            }
        }
    }

    /// <summary>
    /// Runs one tool call. Once no call is in flight, the tail held for a later write is released, redacted: user
    /// code that printed without a newline is seen when its call ends, not when the server exits.
    /// </summary>
    public async Task<T> DuringCallAsync<T>(Func<Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (_gate)
        {
            _calls++;
        }

        try
        {
            return await call().ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _calls--;
                if (_calls == 0 && _loading == 0)
                {
                    Emit(_redactor.Flush());
                }
            }
        }
    }

    /// <summary>Releases what is still held, redacted; nothing more will follow. Output held for a load still in flight is withheld.</summary>
    public void End()
    {
        lock (_gate)
        {
            if (_loading > 0)
            {
                Withhold();
            }

            Emit(_redactor.Flush());
        }
    }

    private void ReleaseHeld()
    {
        if (_loadFailed)
        {
            Withhold();
            return;
        }

        var held = _held;
        _held = [];
        foreach (var text in held)
        {
            Emit(_redactor.Push(text));
        }
    }

    private void Withhold()
    {
        var bytes = _held.Sum(text => Encoding.UTF8.GetByteCount(text));
        _held = [];
        _loadFailed = false;
        if (bytes > 0)
        {
            Log("e2e mcp: [warning] withheld " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes of output printed while a config failed to load: its secrets are unknown, so the output cannot be redacted");
        }
    }

    private void Emit(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _atLineStart = text.EndsWith('\n');
        _sink(text);
    }
}

/// <summary>
/// A <see cref="TextWriter"/> that hands everything written to it to a <see cref="McpOutput"/>. .NET-only: upstream
/// replaces <c>process.stdout.write</c> and <c>process.stderr.write</c>; .NET swaps <see cref="Console.Out"/> and <see cref="Console.Error"/> instead.
/// </summary>
internal sealed class McpOutputWriter(McpOutput output, OutputStream stream) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => output.Write(stream, value.ToString());

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            output.Write(stream, value);
        }
    }

    public override void Write(char[] buffer, int index, int count) => output.Write(stream, new string(buffer, index, count));

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (!buffer.IsEmpty)
        {
            output.Write(stream, new string(buffer));
        }
    }
}
