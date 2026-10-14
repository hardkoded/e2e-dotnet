// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceIdentity;

public sealed class ParamsIdentityTests
{
    [Fact]
    public void Digests_missing_params_as_the_empty_object_independent_of_key_order()
    {
        Assert.Equal(CacheKeys.ParamsDigest(null), CacheKeys.ParamsDigest(new Dictionary<string, object?>()));
        Assert.Equal(
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }),
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 }));
        Assert.NotEqual(
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["a"] = 1 }),
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["a"] = 2 }));
    }

    // No upstream it: upstream digests the params as canonical JSON, the port as name=value lines.
    [Fact]
    public void Keeps_a_newline_or_an_equals_sign_in_one_param_from_passing_as_another()
    {
        var multiline = new Dictionary<string, object?> { ["note"] = "a\nb=c" };
        var two = new Dictionary<string, object?> { ["note"] = "a", ["b"] = "c" };
        Assert.NotEqual(CacheKeys.ParamsDigest(multiline), CacheKeys.ParamsDigest(two));
        Assert.NotEqual(
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["a=b"] = "c" }),
            CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["a"] = "b=c" }));
    }
}
