// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// .NET-only: upstream has no <c>it</c> for this. <c>Command.PositiveMs</c> reads a JSON number the way JS reads a number for
/// <c>Number.isSafeInteger</c> (upstream's <c>positiveInt</c> and <c>timerMs</c>), and a .NET <c>JsonElement</c> needs its own code for that.
/// </summary>
public sealed class PositiveMsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

    [Fact]
    public void Reads_a_millisecond_count_as_a_JS_number_does()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(1000), E2EConfig.Parse("""{ "maxDuration": 1e3 }""", Root, Env()).MaxDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), E2EConfig.Parse("""{ "maxDuration": 1000.0 }""", Root, Env()).MaxDuration);
        var unsafeInt = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "maxDuration": 9007199254740993 }""", Root, Env()));
        Assert.Contains("must be a positive safe integer of milliseconds", unsafeInt.Message, StringComparison.Ordinal);
    }

    private static Func<string, string?> Env() => _ => null;
}
