// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('text model history')</c> of <c>executor.test.ts</c>.</summary>
public sealed class TextModelHistoryTests
{
    [Fact]
    public async Task Shows_a_field_fill_the_last_six_actions_only()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id == "operation"
                    ? new ScriptedAnswer(
                        call < 18 ? "tap"
                        : call == 18 ? "type"
                        : "blocked"
                    )
                    : new ScriptedAnswer(keys[0])
        );
        var text = ScriptedText("Ada");
        var fixture = Context(new ContextOptions { Tree = Field, Model = text });
        fixture.ObserveWith = look => Obs(Ticking(Field, look), look);
        await Run(model, fixture, textModel: text);
        var raw = text.Requests[0].Messages.Single(message => message.Role == "user").Content!;
        var sent = JsonDocument.Parse(raw[(raw.IndexOf('\n', StringComparison.Ordinal) + 1)..]).RootElement;
        Assert.Equal(6, sent.GetProperty("recentActions").GetArrayLength());
    }
}
