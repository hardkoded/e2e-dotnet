// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceStore;

/// <summary>
/// Partial: "misses on an absent entry and rejects a malformed key hash" (a malformed key throws <c>INVALID_ARGUMENT</c>
/// where upstream answers <c>invalid</c> or writes nothing; issue #1258). The byte-cap and not-writable tests call
/// <c>FileStepCache.Write</c> directly: <c>IStepCache.Write</c> returns nothing (issue #1259).
/// </summary>
public sealed class FileCacheStoreTests : IDisposable
{
    private static readonly string Key = new('a', 64);

    private readonly string _directory = CoreTests.TempCache();
    private readonly FileStepCache _store;

    public FileCacheStoreTests()
    {
        _store = new FileStepCache(_directory);
    }

    private static CacheEntry Payload() => new()
    {
        Instruction = "upgrade the plan",
        Route = "/settings",
        Actions = [new RecordedAction { Kind = "tap", Role = "button", Name = "Upgrade" }],
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Round_trips_one_entry_under_the_key_digest()
    {
        _store.Write(Key, Payload());
        Assert.Equal([Key + ".json"], Directory.GetFiles(_directory).Select(Path.GetFileName));
        var read = _store.Read(Key).Entry;
        Assert.NotNull(read);
        Assert.Equal("upgrade the plan", read.Instruction);
        Assert.Equal("/settings", read.Route);
        var action = Assert.Single(read.Actions);
        Assert.Equal(("tap", "button", "Upgrade"), (action.Kind, action.Role, action.Name));
    }

    [Fact]
    public void Partial_Misses_on_an_absent_entry_and_throws_on_a_malformed_key_hash()
    {
        var miss = _store.Read(Key);
        Assert.Null(miss.Entry);
        Assert.Equal("no-entry", miss.Reason);
        Assert.Equal("INVALID_ARGUMENT", Assert.Throws<TestException>(() => _store.Read("../escape")).Code);
        Assert.Equal("INVALID_ARGUMENT", Assert.Throws<TestException>(() => _store.Write("../escape", Payload())).Code);
    }

    [Fact]
    public void Fails_closed_on_unreadable_and_oversized_entries()
    {
        var store = new FileStepCache(_directory, 4_096, true);
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, Key + ".json");
        File.WriteAllText(path, "{ torn");
        Assert.Equal("invalid-entry", store.Read(Key).Reason);
        File.WriteAllText(path, "{\"pad\":\"" + new string('x', 8_192) + "\"}");
        var oversized = store.Read(Key);
        Assert.Null(oversized.Entry);
        Assert.Equal("invalid-entry", oversized.Reason);
    }

    [Fact]
    public void Refuses_to_write_an_entry_over_its_byte_cap()
    {
        var store = new FileStepCache(_directory, 64, true);
        Assert.Null(store.Write(Key, Payload()));
        Assert.Equal("no-entry", store.Read(Key).Reason);
    }

    [Fact]
    public void Never_writes_when_not_writable()
    {
        var store = new FileStepCache(_directory, FileStepCache.MaxCacheWireBytes, false);
        Assert.Null(store.Write(Key, Payload()));
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Length > 0);
    }

    [Fact]
    public void Evicts_an_entry_on_delete_tolerating_absence_and_read_only_mode()
    {
        _store.Write(Key, Payload());
        _store.Delete(Key);
        Assert.Empty(Directory.GetFiles(_directory));
        _store.Delete(Key);
        var readOnly = new FileStepCache(_directory, FileStepCache.MaxCacheWireBytes, false);
        _store.Write(Key, Payload());
        readOnly.Delete(Key);
        Assert.Equal([Key + ".json"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }
}
