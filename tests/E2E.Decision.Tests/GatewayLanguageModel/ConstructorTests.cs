// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('constructor')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class ConstructorTests
{
    [Fact]
    public void Should_set_basic_properties()
    {
        using var model = CreateTestModel(new TestServer());

        Assert.Equal("test-model", model.ModelId);
        Assert.Equal("test-provider", model.Provider);
        Assert.Equal("v4", model.SpecificationVersion);
    }
}
