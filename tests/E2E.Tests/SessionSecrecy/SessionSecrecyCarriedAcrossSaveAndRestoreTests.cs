// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SessionSecrecy;

/// <summary>The <c>describe('session secrecy carried across save and restore')</c> of upstream's <c>session-secrecy.test.ts</c>.</summary>
[Collection(SharedSecretsCollection.Name)]
public sealed class SessionSecrecyCarriedAcrossSaveAndRestoreTests : IDisposable
{
    private const string StaticValue = "static-config-password-5521";
    private const string ProviderValue = "provider-minted-token-8804";

    private readonly List<AttemptScope> _opened = [];

    private readonly IReadOnlyList<Secret> _secrets =
    [
        Secret.Create("password", StaticValue, "password"),
        Secret.Create("token", _ => Task.FromResult(ProviderValue), "generic-secret"),
    ];

    public SessionSecrecyCarriedAcrossSaveAndRestoreTests() => SecretsSharedBySessionsInOneProcessTests.ClearShared();

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
        return Secrecy.SessionLedger(scope, _secrets);
    }

    [Fact]
    public void Carries_the_provider_values_leaving_static_values_to_each_session()
    {
        var saving = NewSession();
        saving.Register("token", ProviderValue);
        Assert.Equal([("token", ProviderValue)], Secrecy.CarriedSecrets(saving, _secrets));
    }

    [Fact]
    public void Seeds_the_restoring_session_and_the_process_ledger_with_the_carried_values()
    {
        var restoring = NewSession();
        const string value = "provider-minted-token-adopted-2290";
        Assert.Equal(value, global::E2E.OpenSession.Redact(value));
        Secrecy.AdoptSecrets(restoring, [("token", value)]);
        Assert.Equal("echo <secret:token>", restoring.Redactor.Redact($"echo {value}"));
        Assert.Equal("log <secret:token>", global::E2E.OpenSession.Redact($"log {value}"));
    }
}
