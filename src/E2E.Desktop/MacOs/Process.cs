// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>
/// Processes: whether one runs, when it started (which tells it from a later process the OS gives the same id), and whether its
/// environment carries a launch token. macOS hides the environment of its own apps and of builds signed with the <c>restrict</c>
/// flag: that answers null, no evidence either way. Their arguments stay readable.
/// </summary>
internal static unsafe class ProcessInfo
{
    private const int Eperm = 1;
    private const int CtlKern = 1;
    private const int KernProcargs2 = 49;

    /// <summary><c>PROC_PIDTBSDINFO</c>.</summary>
    private const int PidTbsdInfo = 3;

    /// <summary><c>sizeof(struct proc_bsdinfo)</c>: <c>pbi_start_tvsec</c> is the 64-bit field at offset 120 and <c>pbi_start_tvusec</c> the next, at 128.</summary>
    private const int BsdInfoSize = 136;

    private const int StartSecondsOffset = 120;
    private const int StartMicrosecondsOffset = 128;

    /// <summary>Whether a process with this id exists, this user's or another's.</summary>
    public static bool Alive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        var result = Native.kill(pid, 0);
        return result == 0 || Marshal.GetLastPInvokeError() == Eperm;
    }

    /// <summary>When a process started, in microseconds since the epoch; null when it is not running.</summary>
    public static long? Started(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        var buffer = stackalloc byte[BsdInfoSize];
        new Span<byte>(buffer, BsdInfoSize).Clear();
        var read = Native.proc_pidinfo(pid, PidTbsdInfo, 0, buffer, BsdInfoSize);
        if (read != BsdInfoSize)
        {
            return null;
        }

        var seconds = *(ulong*)(buffer + StartSecondsOffset);
        var microseconds = *(ulong*)(buffer + StartMicrosecondsOffset);
        return (long)(seconds * 1_000_000UL + microseconds);
    }

    /// <summary>A process's arguments (after the executable path) and its environment; the environment is null when macOS hides it, and the whole answer null when the process cannot be read.</summary>
    private static (List<string> Arguments, List<string>? Environment)? ProcArgs(int pid)
    {
        var mib = stackalloc int[3];
        mib[0] = CtlKern;
        mib[1] = KernProcargs2;
        mib[2] = pid;
        nuint size = 0;
        if (Native.sysctl(mib, 3, null, &size, null, 0) != 0 || size < 4)
        {
            return null;
        }

        var buffer = new byte[(int)size];
        fixed (byte* bytes = buffer)
        {
            if (Native.sysctl(mib, 3, bytes, &size, null, 0) != 0)
            {
                return null;
            }
        }

        return ParseProcArgs(buffer.AsMemory(0, (int)Math.Min(size, (nuint)buffer.Length)).ToArray());
    }

    /// <summary>
    /// Reads a <c>KERN_PROCARGS2</c> buffer: <c>argc</c>, the executable path, the NUL padding after it, exactly <c>argc</c> argument
    /// strings (an empty argument included), then the environment up to the first empty string.
    /// </summary>
    internal static (List<string> Arguments, List<string>? Environment)? ParseProcArgs(byte[] buffer)
    {
        if (buffer.Length < 4)
        {
            return null;
        }

        var argc = Math.Max(BinaryPrimitives.ReadInt32LittleEndian(buffer), 0);
        var at = 4;

        // The executable path, then its padding.
        var end = Array.IndexOf(buffer, (byte)0, at);
        if (end < 0)
        {
            return null;
        }

        at = end;
        while (at < buffer.Length && buffer[at] == 0)
        {
            at++;
        }

        string? Next()
        {
            if (at >= buffer.Length)
            {
                return null;
            }

            var zero = Array.IndexOf(buffer, (byte)0, at);
            var length = (zero < 0 ? buffer.Length : zero) - at;
            var entry = Encoding.UTF8.GetString(buffer, at, length);
            at += length + 1;
            return entry;
        }

        var argv = new List<string>(argc);
        for (var index = 0; index < argc; index++)
        {
            if (Next() is not { } argument)
            {
                return null;
            }

            argv.Add(argument);
        }

        var environment = new List<string>();
        while (Next() is { Length: > 0 } entry)
        {
            environment.Add(entry);
        }

        // argv[0] names the program again.
        var arguments = argv.Skip(1).ToList();

        // A restricted process shows its arguments alone: no variable a real environment always has.
        // The launch token is in it, or a variable a real environment always has: a helper the app started with a pared-down
        // environment that kept the token still shows it.
        var token = $"{Launch.TokenVariable}=";
        var shown = environment.Any(entry =>
            entry.StartsWith("HOME=", StringComparison.Ordinal)
            || entry.StartsWith("PATH=", StringComparison.Ordinal)
            || entry.StartsWith("USER=", StringComparison.Ordinal)
            || entry.StartsWith(token, StringComparison.Ordinal)
        );
        return (arguments, shown ? environment : null);
    }

    /// <summary>A process's environment, null when it cannot be read.</summary>
    private static List<string>? Environment(int pid) => ProcArgs(pid)?.Environment;

    /// <summary>Whether one of a process's arguments is <paramref name="argument"/>, whole.</summary>
    public static bool HoldsArgument(int pid, string argument) =>
        ProcArgs(pid) is { } read && read.Arguments.Any(entry => entry == argument);

    /// <summary>Whether a process's environment holds <paramref name="marker"/> (<c>NAME=value</c>); null when it cannot be read.</summary>
    public static bool? Carries(int pid, string marker) => Environment(pid)?.Any(entry => entry == marker);

    /// <summary>
    /// Every process of this user that carries the launch <paramref name="token"/>, in its environment or, where macOS hides that, in
    /// its arguments; null when the processes cannot be listed.
    /// </summary>
    public static List<int>? Carrying(string token)
    {
        var count = Native.proc_listallpids(null, 0);
        if (count <= 0)
        {
            return null;
        }

        var pids = new int[count + 64];
        int filled;
        fixed (int* buffer = pids)
        {
            filled = Native.proc_listallpids(buffer, pids.Length * sizeof(int));
        }

        if (filled <= 0)
        {
            return null;
        }

        var own = Native.getpid();
        var marker = Launch.TokenMarker(token);
        var argument = Launch.TokenArgument(token);
        var found = new List<int>();
        foreach (var pid in pids.Take(filled))
        {
            if (pid <= 0 || pid == own)
            {
                continue;
            }

            var carried = Carries(pid, marker) ?? HoldsArgument(pid, argument);
            if (carried)
            {
                found.Add(pid);
            }
        }

        return found;
    }
}
