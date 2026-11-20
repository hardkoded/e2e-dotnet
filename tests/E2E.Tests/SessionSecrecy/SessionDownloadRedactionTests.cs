// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SessionSecrecy;

/// <summary>The <c>describe('session download redaction')</c> of upstream's <c>session-secrecy.test.ts</c>. A session's ledger is its <see cref="AttemptScope"/>.</summary>
[Collection(SharedSecretsCollection.Name)]
public sealed class SessionDownloadRedactionTests : IDisposable
{
    private readonly List<AttemptScope> _opened = [];

    public SessionDownloadRedactionTests() => SecretsSharedBySessionsInOneProcessTests.ClearShared();

    public void Dispose()
    {
        foreach (var scope in _opened)
        {
            scope.ReleaseSessionLedger();
        }

        SecretsSharedBySessionsInOneProcessTests.ClearShared();
    }

    private AttemptScope NewSession(params (string Name, string Value)[] known)
    {
        var scope = SecretsSharedBySessionsInOneProcessTests.Scope(known);
        _opened.Add(scope);
        return scope;
    }

    [Fact]
    public void Rewrites_downloads_whenever_the_ledger_redacts_a_value_its_own_or_one_of_a_session_open_beside_it()
    {
        var host = SecretsSharedBySessionsInOneProcessTests.OpenHost();
        var unsecret = NewSession();
        Assert.False(Secrecy.RedactsDownloads(unsecret.Redactor));
        var beside = NewSession(("password", "static-config-password-5521"));
        Assert.True(Secrecy.RedactsDownloads(beside.Redactor));
        // The app may hand the session a file holding what the one beside it typed.
        Assert.True(Secrecy.RedactsDownloads(unsecret.Redactor));
        beside.ReleaseSessionLedger();
        Assert.False(Secrecy.RedactsDownloads(unsecret.Redactor));
        unsecret.Register("token", "provider-minted-token-8804");
        Assert.True(Secrecy.RedactsDownloads(unsecret.Redactor));
        host();
    }
}
