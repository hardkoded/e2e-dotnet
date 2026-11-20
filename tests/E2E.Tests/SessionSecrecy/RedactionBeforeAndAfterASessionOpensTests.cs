// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SessionSecrecy;

/// <summary>
/// The <c>describe('redaction before and after a session opens')</c> of upstream's <c>session-secrecy.test.ts</c>. Not ported: 'makes the
/// process ledger the default redactor once seeded, so a serialized error carries no secret' calls <c>serializeError</c> with no redactor,
/// which reads the process ledger through <c>setErrorRedactor</c>; the port's <c>ErrorSerializer.Serialize</c> takes its redactor as a
/// parameter and has no process default (issue #1977).
/// </summary>
[Collection(SharedSecretsCollection.Name)]
public sealed class RedactionBeforeAndAfterASessionOpensTests : IDisposable
{
    private const string StaticValue = "static-config-password-7730";

    // Not the values of the other describes: the process ledger keeps what it met, where upstream gives each file a worker.
    private const string ProviderValue = "provider-minted-token-6615";

    private readonly List<AttemptScope> _opened = [];

    private readonly IReadOnlyList<Secret> _secrets =
    [
        Secret.Create("password", StaticValue, "password"),
        Secret.Create("token", _ => Task.FromResult(ProviderValue), "generic-secret"),
    ];

    public RedactionBeforeAndAfterASessionOpensTests() => SecretsSharedBySessionsInOneProcessTests.ClearShared();

    public void Dispose()
    {
        foreach (var scope in _opened)
        {
            scope.ReleaseSessionLedger();
        }

        SecretsSharedBySessionsInOneProcessTests.ClearShared();
    }

    private AttemptScope NewSession()
    {
        var scope = SecretsSharedBySessionsInOneProcessTests.Scope();
        _opened.Add(scope);
        return scope;
    }

    [Fact]
    public void Redacts_the_static_values_with_no_session_and_every_value_the_session_learned_once_one_is_open()
    {
        // Seeding the process, as a run's config does, is what covers text before a session opens.
        global::E2E.OpenSession.SeedProcess(_secrets.Where(secret => secret.Provider is null));
        Assert.Equal("title <secret:password>", Secrecy.RedactForSession(null, _secrets, $"title {StaticValue}"));
        // A provider value is not known before it resolves.
        Assert.Equal($"title {ProviderValue}", Secrecy.RedactForSession(null, _secrets, $"title {ProviderValue}"));
        var session = NewSession();
        Secrecy.SessionLedger(session, _secrets).Register("token", ProviderValue);
        Assert.Equal(
            "<secret:password> <secret:token>",
            Secrecy.RedactForSession(session, _secrets, $"{StaticValue} {ProviderValue}")
        );
    }

    [Fact]
    public void Keeps_one_static_ledger_per_secrets_map_untouched_by_what_a_session_learns()
    {
        var ledger = Secrecy.StaticSecretLedger(_secrets);
        Assert.Same(ledger, Secrecy.StaticSecretLedger(_secrets));
        Secrecy.SessionLedger(NewSession(), _secrets).Register("token", "provider-minted-token-6616");
        Assert.Equal([("password", StaticValue)], ledger.Entries());
    }
}
