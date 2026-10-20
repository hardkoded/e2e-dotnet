// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentSecrets;

/// <summary>
/// The port has no credentials, no purpose-by-config check, and no sink check for a disabled or non-editable field.
/// So these upstream its are not ported: "lets a credential password into an Android password field as the mobile
/// engine projects it" and "judges the purpose by the config, not by what a handle claims". "refuses $sink before reading
/// the plaintext" (a disabled password field, a button) is partial: the port has no plaintext read to count, since a
/// <c>Secret</c> holds its value, and it records no policy decision, so only the refusal and the empty fill are checked.
/// </summary>
public sealed class AuthorizeSecretFillTests
{
    /// <summary>Partial: the port records no policy decision, and the refusal reaches the model as the failed call it is.</summary>
    [Fact]
    public async Task Refuses_a_secret_fill_into_a_plain_textbox_and_never_types_into_it()
    {
        var typed = new List<string>();
        var world = new DocumentWorld().Map("/login", page =>
        {
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Password", Value = "", OnFill = typed.Add });
        });
        string? refusal = null;
        var calls = 0;
        var model = new ScriptedModel(request =>
        {
            if (calls++ == 0)
            {
                return ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "admin-password" });
            }

            refusal = request.Messages.Last(message => message.Role == "tool").Content;
            return ModelResponses.Done("passed", "the field is not a secret field");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://login.test",
            TestTitle = "plain textbox",
        });
        await session.App.OpenAsync("/login");

        await session.Agent.ActAsync(
            "sign in",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin-password", "hunter2", "password") } });

        Assert.Contains("failed: field purpose none is incompatible with secret purpose password", refusal, StringComparison.Ordinal);
        Assert.Empty(typed);
        Assert.DoesNotContain(model.Requests, request => request.Messages.Any(message => message.Content?.Contains("hunter2", StringComparison.Ordinal) == true));
        session.Complete();
    }

    /// <summary>Partial: the port reads no plaintext through a resolver and records no policy decision.</summary>
    [Theory]
    [InlineData("a disabled password field", "textbox", true, "the target field is disabled")]
    [InlineData("a button", "button", false, "the target is not an editable input (role button)")]
    public async Task Refuses_a_secret_fill_into_a_sink_that_cannot_take_it_and_never_types_into_it(string sink, string role, bool disabled, string message)
    {
        _ = sink;
        var typed = new List<string>();
        var world = new DocumentWorld().Map("/login", page =>
        {
            page.Roots.Add(new DocumentElement { Role = role, Name = "Password", Secure = true, InputPurpose = "password", Disabled = disabled, OnFill = typed.Add });
        });
        string? refusal = null;
        var calls = 0;
        var model = new ScriptedModel(request =>
        {
            if (calls++ == 0)
            {
                return ModelResponses.Call("fill_secret", new { role, name = "Password", secret = "admin-password" });
            }

            refusal = request.Messages.Last(item => item.Role == "tool").Content;
            return ModelResponses.Done("passed", "the field cannot take a secret");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://login.test",
            TestTitle = "secret sink",
        });
        await session.App.OpenAsync("/login");

        await session.Agent.ActAsync(
            "sign in",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin-password", "hunter2", "password") } });

        Assert.Contains("failed: " + message, refusal, StringComparison.Ordinal);
        Assert.Empty(typed);
        Assert.DoesNotContain(model.Requests, request => request.Messages.Any(item => item.Content?.Contains("hunter2", StringComparison.Ordinal) == true));
        session.Complete();
    }
}
