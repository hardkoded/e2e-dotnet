// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayExtractionModel.GatewayExtractionModelFixture;

namespace E2E.Decision.Tests.GatewayExtractionModel;

/// <summary>Upstream's <c>describe('GatewayExtractionModel')</c> of <c>gateway-extraction-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GatewayExtractionModelTests
{
    [Fact]
    public void Should_report_every_field_type_as_supported()
    {
        using var model = CreateTestModel(new TestServer());

        Assert.Equal(["text", "number", "date", "time", "datetime", "duration", "list"], model.SupportedFieldTypes);
    }

    [Fact]
    public void Partial_Should_create_the_model_from_both_provider_factories()
    {
        var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
            new GatewayProviderSettings { BaseUrl = "https://api.test.com", ApiKey = "test-token" }
        );

        // Upstream also checks that provider.extraction is provider.extractionModel; in C# they are two methods.
        foreach (
            var factory in new Func<string, E2E.Decision.Sdk.Gateway.GatewayExtractionModel>[]
            {
                provider.ExtractionModel,
                provider.Extraction,
            }
        )
        {
            using var model = factory("scaledown/extract-1");

            Assert.IsType<E2E.Decision.Sdk.Gateway.GatewayExtractionModel>(model);
            Assert.Equal("gateway", model.Provider);
            Assert.Equal("scaledown/extract-1", model.ModelId);
        }
    }
}
