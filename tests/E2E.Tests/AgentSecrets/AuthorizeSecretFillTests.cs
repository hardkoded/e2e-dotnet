// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E.Tests.AgentSecrets;

/// <summary>
/// Host-side secret fill authorization against the node shapes engines project, ported from upstream <c>agent-secrets.test.ts</c>
/// (<c>describe('authorizeSecretFill')</c>). The Android password field is the case that motivated it: the mobile engine once projected it
/// as a plain textbox, which this policy refuses for a credential's password, so the shape core accepts is pinned here next to the shape
/// it turns away. Each <c>it</c> calls <c>SecretFill.AuthorizeSecretFillAsync</c> directly, as upstream calls <c>authorizeSecretFill</c>.
/// Difference: the credential's password is the handle the <c>admin</c> credential carries, not <c>Credentials.User("admin").Password</c>,
/// because the registry <c>Credentials.User</c> reads is process-wide and the tests run in parallel.
/// </summary>
public sealed class AuthorizeSecretFillTests
{
    private static readonly Secret AdminPassword = Secret.Create("admin.password", "hunter2", "password");
    private static readonly ResolvedCredential Admin = new("admin", "admin@example.com", AdminPassword);

    /// <summary>
    /// An Android password <c>EditText</c> as <c>E2E.Mobile</c> projects it: UIAutomator flags it <c>password</c> while the class stays the
    /// plain one, so the engine derives <c>secure</c> and the purpose from the flag and drops the value.
    /// </summary>
    private static SemanticNode AndroidPasswordField(bool disabled = false, string role = "textbox", bool secure = true) => new()
    {
        Ref = "n3",
        Role = role,
        Name = "Password",
        Text = "Password",
        InputPurpose = "password",
        States = new NodeStates { Secure = secure, Disabled = disabled },
        TestId = "com.example:id/password",
        Rect = new BoundingBox(0, 270, 390, 44),
        Selector = "id=com.example:id/password",
    };

    /// <summary>The same field as the engine projected it before it read the flag: an open textbox carrying the typed value.</summary>
    private static SemanticNode AsPlainTextbox() => new()
    {
        Ref = "n3",
        Role = "textbox",
        Name = "Password",
        Text = "Password",
        Value = "hunter2",
        TestId = "com.example:id/password",
        Rect = new BoundingBox(0, 270, 390, 44),
        Selector = "id=com.example:id/password",
    };

    [Fact]
    public async Task Lets_a_credential_password_into_an_Android_password_field_as_the_mobile_engine_projects_it()
    {
        var recorder = new RecordingHost();

        var resolved = await SecretFill.AuthorizeSecretFillAsync(recorder, new Runtime(), Admin.Password, AndroidPasswordField());

        Assert.Equal("hunter2", resolved.Value);
        Assert.Equal(["secret.fill:allowed"], recorder.Decisions);
    }

    [Fact]
    public async Task Refuses_the_same_field_projected_as_a_plain_textbox_the_shape_before_the_mobile_fix()
    {
        var recorder = new RecordingHost();

        var error = await Assert.ThrowsAsync<AgentException>(() => SecretFill.AuthorizeSecretFillAsync(recorder, new Runtime(), Admin.Password, AsPlainTextbox()));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Equal("field purpose none is incompatible with secret purpose password", error.Message);
        Assert.Equal(["secret.purpose:denied:POLICY_DENIED"], recorder.Decisions);
    }

    [Fact]
    public async Task Judges_the_purpose_by_the_config_not_by_what_a_handle_claims()
    {
        var claimsGeneric = Secret.Create(AdminPassword.Name, "hunter2", "generic-secret");
        var recorder = new RecordingHost();
        var runtime = new Runtime();

        var error = await Assert.ThrowsAsync<AgentException>(() => SecretFill.AuthorizeSecretFillAsync(recorder, runtime, claimsGeneric, AsPlainTextbox()));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Equal("field purpose none is incompatible with secret purpose password", error.Message);
        Assert.Equal(["secret.purpose:denied:POLICY_DENIED"], recorder.Decisions);
        Assert.Empty(runtime.Resolved);
    }

    [Theory]
    [InlineData("a disabled password field", "textbox", true, "the target field is disabled")]
    [InlineData("a button", "button", false, "the target is not an editable input (role button)")]
    public async Task Refuses_sink_before_reading_the_plaintext(string sink, string role, bool disabled, string message)
    {
        _ = sink;
        var recorder = new RecordingHost();
        var runtime = new Runtime();
        var node = AndroidPasswordField(disabled: disabled, role: role, secure: !string.Equals(role, "button", StringComparison.Ordinal));

        var error = await Assert.ThrowsAsync<AgentException>(() => SecretFill.AuthorizeSecretFillAsync(recorder, runtime, Admin.Password, node));

        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(["secret.sink:denied:POLICY_DENIED"], recorder.Decisions);
        Assert.Empty(runtime.Resolved);
    }

    /// <summary>The two members <c>AuthorizeSecretFillAsync</c> reads off the runtime, over one registered credential; <see cref="Resolved"/> hears each plaintext read.</summary>
    private sealed class Runtime : ISecretFillRuntime
    {
        public List<string> Resolved { get; } = [];

        public Redactor Redactor => Redactor.None;

        public Secret? RegisteredSecret(string name) => string.Equals(name, AdminPassword.Name, StringComparison.Ordinal) ? AdminPassword : null;

        public Task<Secret> ResolveAsync(Secret secret, CancellationToken cancellationToken)
        {
            Resolved.Add(secret.Name);
            return Task.FromResult(secret);
        }
    }

    private sealed class RecordingHost : ISecretFillHost
    {
        public List<string> Decisions { get; } = [];

        public void RecordPolicy(string name, PolicyDecision decision, string? code = null) =>
            Decisions.Add(code is null ? name + ":" + decision.ToString().ToLowerInvariant() : name + ":" + decision.ToString().ToLowerInvariant() + ":" + code);
    }
}
