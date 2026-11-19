// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.FileLock;

/// <summary>
/// Stands where upstream's <c>file-lock.test.ts</c> mocks <c>statSync</c>, <c>rmSync</c>, <c>writeFileSync</c>, <c>utimesSync</c>,
/// <c>process.kill</c> and the fake timers: each hook sees the real call and decides what happens.
/// </summary>
internal sealed class LockSystem : FileLockSystem
{
    /// <summary>Runs on each <c>statSync</c>; gets the real stat.</summary>
    public Func<Func<DateTimeOffset>, DateTimeOffset>? OnStat { get; set; }

    /// <summary>Runs on each <c>rmSync</c>; gets the path and the real removal.</summary>
    public Action<string, Action>? OnRemove { get; set; }

    /// <summary>Runs on each <c>writeFileSync(fd, token)</c>; gets the stream, the token, and the real write.</summary>
    public Action<FileStream, byte[], Action>? OnWriteToken { get; set; }

    /// <summary>Runs on each <c>utimesSync</c>; gets the real call.</summary>
    public Action<Action>? OnTouch { get; set; }

    /// <summary>Runs on each <c>process.kill(pid, 0)</c>; gets the pid and the real check.</summary>
    public Func<int, Func<bool>, bool>? OnIsRunning { get; set; }

    public FakeTimeProvider? FakeClock { get; init; }

    public int Touches { get; private set; }

    public override TimeProvider Clock => FakeClock ?? TimeProvider.System;

    protected internal override DateTimeOffset Stat(string path) =>
        OnStat is null ? base.Stat(path) : OnStat(() => base.Stat(path));

    public override void Remove(string path)
    {
        if (OnRemove is null)
        {
            base.Remove(path);
            return;
        }

        OnRemove(path, () => base.Remove(path));
    }

    public override void WriteToken(FileStream stream, byte[] token)
    {
        if (OnWriteToken is null)
        {
            base.WriteToken(stream, token);
            return;
        }

        OnWriteToken(stream, token, () => base.WriteToken(stream, token));
    }

    protected internal override void SetLastWriteTime(string path, DateTime when)
    {
        Touches++;
        if (OnTouch is null)
        {
            base.SetLastWriteTime(path, when);
            return;
        }

        OnTouch(() => base.SetLastWriteTime(path, when));
    }

    public override bool IsRunning(int pid) =>
        OnIsRunning is null ? base.IsRunning(pid) : OnIsRunning(pid, () => base.IsRunning(pid));
}
