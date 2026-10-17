// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.TraceRecorder;

/// <summary>"returns undefined for a node with nothing durable to say" is not ported: the port always records an action for a target.</summary>
public sealed class DescribeTargetTests
{
    /// <summary>Partial port of "drops text duplicating the name and never records values": the port's description holds no text and no input purpose, and the value a field holds never reaches it.</summary>
    [Fact]
    public void Never_records_the_text_or_the_value_the_target_holds()
    {
        var button = new SemanticNode { Ref = "n42", Role = "button", Name = "Upgrade", Text = "Upgrade", TestId = "upgrade-cta" };
        var field = new SemanticNode { Ref = "n7", Role = "textbox", Name = "Password", InputPurpose = "password", Value = "s3cr3t" };

        var described = Recorder.DescribeTarget(field, Redactor.None);

        Assert.Null(Recorder.DescribeTarget(button, Redactor.None).Text);
        Assert.Equal(("textbox", "Password"), (described.Role, described.Name));
        Assert.DoesNotContain("s3cr3t", JsonSerializer.Serialize(described), StringComparison.Ordinal);
    }
}
