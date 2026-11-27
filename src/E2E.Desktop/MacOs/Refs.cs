// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>One element a ref names, and where it was read.</summary>
internal sealed record Entry(Element Element, int Pid, uint Window);

/// <summary>
/// The elements snapshots handed out refs for, held in this process: an action names a ref, and acts on that very element. An
/// element whose control is gone answers <c>AX_INVALID_ELEMENT</c>, a stale ref. The oldest refs are dropped first once there are
/// too many.
/// </summary>
internal static class Refs
{
    /// <summary>How many refs stay usable, across every snapshot.</summary>
    private const int MaxRefs = 100_000;

    private static readonly Lock s_lock = new();
    private static readonly Dictionary<string, Entry> s_entries = [];
    private static readonly Queue<string> s_order = new();
    private static readonly ulong s_base = (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.ProcessId << 32);
    private static long s_next;

    /// <summary>A snapshot id no other snapshot of this process has: a ref from another run or process never resolves here.</summary>
    public static string SnapshotId()
    {
        var next = (ulong)Interlocked.Increment(ref s_next);
        unchecked
        {
            return "s" + Radix36(s_base + next * 0x9E37_79B9_7F4A_7C15UL);
        }
    }

    private static string Radix36(ulong value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var text = new StringBuilder();
        do
        {
            text.Insert(0, digits[(int)(value % 36)]);
            value /= 36;
        } while (value != 0);
        return text.ToString();
    }

    /// <summary>Keeps a snapshot's refs, dropping the oldest beyond the cap.</summary>
    public static void InsertAll(IEnumerable<(string Reference, Entry Entry)> entries)
    {
        lock (s_lock)
        {
            foreach (var (reference, entry) in entries)
            {
                s_order.Enqueue(reference);
                s_entries[reference] = entry;
            }

            while (s_order.Count > MaxRefs)
            {
                s_entries.Remove(s_order.Dequeue());
            }
        }
    }

    public static Entry? Get(string reference)
    {
        lock (s_lock)
        {
            return s_entries.GetValueOrDefault(reference);
        }
    }

    /// <summary>Drops every ref of a process, once it quit.</summary>
    public static void ForgetProcess(int pid)
    {
        lock (s_lock)
        {
            foreach (var reference in s_entries.Where(pair => pair.Value.Pid == pid).Select(pair => pair.Key).ToList())
            {
                s_entries.Remove(reference);
            }

            var kept = s_order.Where(s_entries.ContainsKey).ToList();
            s_order.Clear();
            foreach (var reference in kept)
            {
                s_order.Enqueue(reference);
            }
        }
    }
}
