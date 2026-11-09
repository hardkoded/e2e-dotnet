// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E;

/// <summary>
/// The narrow surface a secret fill needs from its step machinery: the act dispatch is its one caller today, and any future tier that
/// fills secrets satisfies the same member rather than re-deriving the policy (upstream's <c>SecretFillHost</c>, <c>agent/secrets.ts</c>).
/// </summary>
internal interface ISecretFillHost
{
    void RecordPolicy(string name, PolicyDecision decision, string? code = null);
}

/// <summary>
/// The two members <see cref="SecretFill.AuthorizeSecretFillAsync"/> reads off upstream's <c>AgentContext</c> (<c>config.allSecrets.get</c>
/// and <c>secrets.resolve</c>), plus the redactor the refusal's text goes through. .NET-only: upstream passes the whole context.
/// </summary>
internal interface ISecretFillRuntime
{
    /// <summary>The secret the attempt knows under <paramref name="name"/>, or null (upstream's <c>config.allSecrets.get</c>).</summary>
    Secret? RegisteredSecret(string name);

    /// <summary>The secret with its plaintext known, joined to redaction when a provider computed it (upstream's <c>secrets.resolve</c>).</summary>
    Task<Secret> ResolveAsync(Secret secret, CancellationToken cancellationToken);

    Redactor Redactor { get; }
}

/// <summary>Host-side secret fill authorization (upstream <c>agent/secrets.ts</c>).</summary>
internal static class SecretFill
{
    /// <summary>
    /// Authorizes one secret fill and resolves the plaintext only after every check passes. The resolved secret is returned to the caller
    /// for a single immediate handoff to the trusted engine and is never logged, cached, or sent to a model. Upstream's
    /// <c>authorizeSecretFill</c> returns the plaintext string; the port returns the resolved <see cref="Secret"/>, which holds it.
    /// The checks: a secret the attempt knows, an enabled editable field, and a password field for a password (judged by the secret the
    /// attempt registered, not by the handle). There is no origin check.
    /// </summary>
    public static async Task<Secret> AuthorizeSecretFillAsync(ISecretFillHost host, ISecretFillRuntime runtime, Secret secret, SemanticNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(node);
        var registered = runtime.RegisteredSecret(secret.Name);
        if (registered is null)
        {
            var code = secret.UnavailableCode;
            host.RecordPolicy("secret.registered", PolicyDecision.Denied, code);
            throw new AgentException(code, "secret \"" + secret.Name + "\" is not configured", blocked: true);
        }

        if (node.States.Disabled)
        {
            host.RecordPolicy("secret.sink", PolicyDecision.Denied, "POLICY_DENIED");
            throw Denied("the target field is disabled");
        }

        if (!Roles.IsEditable(node))
        {
            host.RecordPolicy("secret.sink", PolicyDecision.Denied, "POLICY_DENIED");
            throw Denied("the target is not an editable input (role " + (node.Role is null ? "none" : AgentObservation.RedactText(node.Role, runtime.Redactor)) + ")");
        }

        // A password belongs in a password field, where the surface masks it. A generic secret has no field of its
        // own: it goes wherever the test says, and redaction covers the value the moment it shows on screen.
        // The config says what the secret is for, not the handle a test built.
        var inputPurpose = string.IsNullOrEmpty(node.InputPurpose) ? "none" : node.InputPurpose;
        if (registered.Purpose == "password" && inputPurpose != "password")
        {
            host.RecordPolicy("secret.purpose", PolicyDecision.Denied, "POLICY_DENIED");
            throw Denied("field purpose " + inputPurpose + " is incompatible with secret purpose " + registered.Purpose);
        }

        host.RecordPolicy("secret.fill", PolicyDecision.Allowed);
        return await runtime.ResolveAsync(secret, cancellationToken).ConfigureAwait(false);
    }

    private static AgentException Denied(string reason) => new("POLICY_DENIED", reason, blocked: true);
}
