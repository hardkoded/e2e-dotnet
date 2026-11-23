// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCacheBytes;

/// <summary>Upstream: packages/e2e/tests/unit/cli-cache-bytes.test.ts, describe('formatBytes').</summary>
public sealed class FormatBytesTests
{
    [Fact]
    public void Rounds_before_choosing_KiB_or_MiB_so_just_under_1_MiB_reads_1_0_MiB()
    {
        Assert.Equal("512 B", FormatBytes.Format(512));
        Assert.Equal("1.3 KiB", FormatBytes.Format(1_280));
        Assert.Equal("1.5 KiB", FormatBytes.Format(1_536));
        Assert.Equal("1023.9 KiB", FormatBytes.Format(1_048_473));
        Assert.Equal("1.0 MiB", FormatBytes.Format(1_048_575));
        Assert.Equal("5.0 MiB", FormatBytes.Format(5L * 1024 * 1024));
    }
}
