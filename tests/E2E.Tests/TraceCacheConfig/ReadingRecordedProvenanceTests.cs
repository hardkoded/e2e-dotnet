// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Tests.TraceCacheConfig;

/// <summary>Upstream's <c>describe('reading recorded provenance')</c>, over <see cref="TraceRead.ReadTraceEntry(JsonNode?)"/>.</summary>
public sealed class ReadingRecordedProvenanceTests
{
    public static TheoryData<string, string> Digests =>
        new()
        {
            { "paramsDigest", "\"not-a-digest\"" },
            { "paramsDigest", "42" },
            { "identityDigest", "\"not-a-digest\"" },
            { "identityDigest", "[\"" + new string('a', 64) + "\"]" },
            { "identityDigest", "42" },
        };

    [Theory]
    [MemberData(nameof(Digests))]
    public void Refuses_an_entry_whose_field_is_not_a_digest(string field, string digest)
    {
        var entry = new CacheEntry
        {
            Schema = FileStepCache.SchemaVersion,
            Test = "tests/a.e2e.ts::a",
            TargetId = "web",
            Instruction = "open billing",
            Engine = "web",
            ParamsDigest = new string('e', 64),
            CallIndex = 0,
            Agent = "default",
            Summary = "step passed",
            Route = "/",
            EndRoute = "/customers",
            Actions = [new RecordedAction { Kind = "navigate", Url = "/customers" }],
        };
        var document = JsonSerializer.SerializeToNode(entry, JsonDefaults.Options)!.AsObject();
        Assert.NotNull(TraceRead.ReadTraceEntry(document));

        document[field] = JsonNode.Parse(digest);

        Assert.Null(TraceRead.ReadTraceEntry(document));
    }
}
