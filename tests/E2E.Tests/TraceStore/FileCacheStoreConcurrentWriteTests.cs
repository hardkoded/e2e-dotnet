// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceStore;

/// <summary>
/// No upstream <c>it</c> races writers on one key: upstream relies on a POSIX rename replacing the target in one step, while on Windows
/// the port's <c>AtomicWrite</c> must retry a move another move is replacing (see COMPATIBILITY.md, "Replay cache").
/// </summary>
public sealed class FileCacheStoreConcurrentWriteTests : IDisposable
{
    private readonly string _directory = CoreTests.TempCache();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Two_writers_on_one_key_leave_one_whole_entry_and_no_temp_file()
    {
        var store = new FileStepCache(_directory);
        var key = new string('b', 64);

        Parallel.For(0, 40, index => store.Write(key, new CacheEntry { Instruction = "step " + index, Route = "/" }));

        Assert.Equal([key + ".json"], Directory.GetFiles(_directory).Select(Path.GetFileName));
        Assert.NotNull(store.Read(key).Entry);
    }
}
