// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SessionSecrecy;

/// <summary>
/// The <c>describe('session download redaction')</c> of upstream's <c>session-secrecy.test.ts</c>. The port has no <c>SessionSecrecy</c>,
/// <c>sessionLedger</c>, or <c>carriedSecrets</c>, so the other <c>it</c>s of the file (carrying and adopting a session's secrets, the
/// redaction before and after a session opens) are not ported. <c>RedactsDownloads</c> takes the ledger alone, and the ledger is a
/// <see cref="Redactor"/>.
/// </summary>
public sealed class SessionDownloadRedactionTests
{
    [Fact]
    public void Rewrites_downloads_whenever_the_ledger_holds_a_value()
    {
        var secrets = new[] { (Name: "password", Value: "static-config-password-5521") };
        Assert.True(Secrecy.RedactsDownloads(Redactor.For(secrets)));
        Assert.False(Secrecy.RedactsDownloads(Redactor.None));
    }
}
