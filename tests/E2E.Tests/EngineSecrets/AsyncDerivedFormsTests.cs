// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineSecrets;

/// <summary>
/// .NET-only: upstream's <c>ResolveSecretOptions.derived</c> may return a promise, which C# spells as <see cref="ResolveSecretOptions.DerivedAsync"/>.
/// No upstream <c>it</c> covers an asynchronous callback.
/// </summary>
public sealed class AsyncDerivedFormsTests
{
    [Fact]
    public async Task Awaits_the_asynchronous_callback_and_keeps_the_synchronous_one()
    {
        var options = new ResolveSecretOptions
        {
            Derived = value => [value + "-sync"],
            DerivedAsync = async value =>
            {
                await Task.Delay(10);
                return [value + "-async"];
            },
        };

        Assert.Equal(["pw-sync", "pw-async"], await Secrecy.DeriveAsync(options, "pw"));
        Assert.Empty(await Secrecy.DeriveAsync(null, "pw"));
    }
}
