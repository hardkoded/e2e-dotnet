// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.SessionStaging;

/// <summary>
/// The setup session contract of <c>SessionStaging</c> (<c>run/sessions.ts</c>). These are .NET-only tests: upstream has no <c>it</c>
/// for the class; its runner tests reach it through <c>test.setup</c> and <c>session.save</c>, which the port does not have.
/// </summary>
public sealed class SessionStagingTests
{
    private static readonly EngineState State = new("fake-state", 1, new JsonObject { ["stored"] = "token" });

    private static readonly SavedSecrecy Secrecy = new([("token", "s3cret")], false);

    [Fact]
    public void Stages_each_declared_name_once_and_lists_them()
    {
        var staging = new E2E.SessionStaging(["acct", "admin"]);
        staging.Stage("acct", State, Secrecy);

        Assert.Equal(["admin"], staging.Missing());
        var entry = Assert.Single(staging.Entries());
        Assert.Equal("acct", entry.Key);
        Assert.Same(State, entry.Value.State);
        Assert.Same(Secrecy, entry.Value.Secrecy);

        staging.Stage("admin", State, Secrecy);
        Assert.Empty(staging.Missing());
    }

    [Fact]
    public void Refuses_a_name_saved_twice()
    {
        var staging = new E2E.SessionStaging(["acct"]);
        staging.Stage("acct", State, Secrecy);

        var error = Assert.Throws<TestException>(() => staging.Stage("acct", State, Secrecy));
        Assert.Equal("SESSION_CONTRACT", error.Code);
        Assert.Equal("session \"acct\" saved twice", error.Message);
    }

    [Fact]
    public void Refuses_a_name_the_setup_test_did_not_declare()
    {
        var staging = new E2E.SessionStaging(["acct"]);

        var error = Assert.Throws<TestException>(() => staging.Stage("other", State, Secrecy));
        Assert.Equal("SESSION_CONTRACT", error.Code);
        Assert.Equal("session \"other\" was not declared by this setup test", error.Message);
        Assert.Empty(staging.Entries());
    }
}
