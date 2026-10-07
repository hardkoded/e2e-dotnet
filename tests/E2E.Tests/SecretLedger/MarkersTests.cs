// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SecretLedger;

public sealed class MarkersTests
{
    [Fact]
    public void Redacts_twice_as_it_redacts_once_a_marker_it_wrote_is_never_read_again()
    {
        var secrets = new[] { Secret.Create("secret", "secret") };
        var once = SnapshotText.Redact("a secret here", secrets);
        Assert.Equal("a <secret:secret> here", once);
        Assert.Equal(once, SnapshotText.Redact(once, secrets));
    }
}
