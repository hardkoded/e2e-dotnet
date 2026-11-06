// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('exit code mapping')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class ExitCodeMappingTests
{
    [Fact]
    public void Maps_categories()
    {
        Assert.Equal(1, ErrorSerializer.ExitCodeForCategory("test"));
        Assert.Equal(2, ErrorSerializer.ExitCodeForCategory("configuration"));
        Assert.Equal(3, ErrorSerializer.ExitCodeForCategory("infrastructure"));
        Assert.Equal(4, ErrorSerializer.ExitCodeForCategory("internal"));
        Assert.Equal(130, ErrorSerializer.ExitCodeForCategory("interrupted"));
    }

    [Fact]
    public void Combines_with_precedence_130_gt_4_gt_3_gt_2_gt_1_gt_0()
    {
        Assert.Equal(2, ErrorSerializer.CombineExitCodes([0, 1, 2]));
        Assert.Equal(3, ErrorSerializer.CombineExitCodes([1, 3]));
        Assert.Equal(130, ErrorSerializer.CombineExitCodes([4, 3, 130]));
        Assert.Equal(0, ErrorSerializer.CombineExitCodes([0]));
        Assert.Equal(0, ErrorSerializer.CombineExitCodes([]));
    }
}
