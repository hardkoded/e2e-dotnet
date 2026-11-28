// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;

namespace E2E.Desktop.Tests.Driver;

/// <summary>What the <c>driver.test.ts</c> classes share: its <c>later</c> and <c>failed</c> helpers.</summary>
internal static class DriverTestSupport
{
    /// <summary>An answer that arrives after <paramref name="ms"/>. Upstream's <c>later</c>.</summary>
    public static async Task<Envelope<T>> Later<T>(Envelope<T> answer, int ms)
    {
        await Task.Delay(ms);
        return answer;
    }

    /// <summary>A failure the driver reported. Upstream's <c>failed</c>.</summary>
    public static DriverFailure Failed(string code, string delivery, Dictionary<string, object?>? details = null) =>
        new(new DriverError(code, $"{code} from the driver", new Disposition(delivery), details));
}
