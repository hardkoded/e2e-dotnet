// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.ParallelSearch;

/// <summary>Upstream's <c>describe('parallelSearch')</c> of <c>tool/parallel-search.test.ts</c>, without its nested <c>describe</c>s.</summary>
public sealed class ParallelSearchTests
{
    [Fact]
    public void Creates_a_provider_executed_Parallel_Search_tool()
    {
        var tool = ParallelSearchTool.ParallelSearch(
            new ParallelSearchConfig
            {
                Mode = ParallelSearchMode.Fast,
                Location = "us",
                MaxCharsTotal = 8000,
                FetchPolicy = new ParallelSearchFetchPolicy { TimeoutSeconds = 10, DisableCacheFallback = true },
            }
        );

        AssertProviderExecutedTool(
            tool,
            "gateway.parallel_search",
            """
            {
              "mode": "fast",
              "location": "us",
              "maxCharsTotal": 8000,
              "fetchPolicy": { "timeoutSeconds": 10, "disableCacheFallback": true }
            }
            """
        );
    }
}
