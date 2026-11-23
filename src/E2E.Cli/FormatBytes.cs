// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Cli;

/// <summary>Upstream's <c>format-bytes.ts</c>.</summary>
internal static class FormatBytes
{
    /// <summary>
    /// A size as <c>512 B</c>, <c>12.3 KiB</c>, or <c>4.0 MiB</c>. Rounds to a tenth first, so just under 1 MiB is <c>1.0 MiB</c>,
    /// not <c>1024.0 KiB</c>.
    /// </summary>
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var tenths = Math.Round(bytes * 10 / 1024.0, MidpointRounding.AwayFromZero);
        return tenths < 10_240
            ? (tenths / 10).ToString("0.0", CultureInfo.InvariantCulture) + " KiB"
            : (bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
    }
}
