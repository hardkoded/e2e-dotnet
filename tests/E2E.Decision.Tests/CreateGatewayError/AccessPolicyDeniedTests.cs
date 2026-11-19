// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayJson;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('access_policy_denied')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class AccessPolicyDeniedTests
{
    private const string Message =
        "Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).";

    [Fact]
    public void Creates_GatewayAccessPolicyDeniedError_with_the_policy_ids_and_removed_values()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":{"policyIds":["ap_1","ap_2"],"removedModels":["openai/gpt-5"],"removedProviders":["openai"]}},"generationId":"gen_123"}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal("access_policy_denied", error.Type);
        Assert.Equal(403, error.StatusCode);
        Assert.False(error.IsRetryable);
        Assert.Equal(Message + " [gen_123]", error.Message);
        Assert.Equal("gen_123", error.GenerationId);
        Assert.Equal(["ap_1", "ap_2"], denied.PolicyIds);
        Assert.Equal(["openai/gpt-5"], denied.RemovedModels);
        Assert.Equal(["openai"], denied.RemovedProviders);
    }

    [Fact]
    public void Ignores_extra_keys_in_param()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":{"name":"AccessPolicyDeniedError","type":"access_policy_denied","statusCode":403,"message":"x","policyIds":["ap_1"],"removedModels":[],"removedProviders":["openai"]}}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(["ap_1"], denied.PolicyIds);
        Assert.Empty(denied.RemovedModels!);
        Assert.Equal(["openai"], denied.RemovedProviders);
    }

    [Fact]
    public void Leaves_the_arrays_undefined_when_the_response_has_no_param()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied"}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(403, error.StatusCode);
        Assert.Equal(Message, error.Message);
        Assert.Null(denied.PolicyIds);
        Assert.Null(denied.RemovedModels);
        Assert.Null(denied.RemovedProviders);
    }

    [Fact]
    public void Keeps_the_arrays_that_are_present_when_others_are_missing()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":{"policyIds":["ap_1"]}}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(["ap_1"], denied.PolicyIds);
        Assert.Null(denied.RemovedModels);
        Assert.Null(denied.RemovedProviders);
    }

    [Fact]
    public void Leaves_the_arrays_undefined_when_param_is_a_string()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":"model"}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(403, error.StatusCode);
        Assert.Null(denied.PolicyIds);
        Assert.Null(denied.RemovedModels);
        Assert.Null(denied.RemovedProviders);
    }

    [Fact]
    public void Leaves_the_arrays_undefined_when_param_is_null()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":null}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(403, error.StatusCode);
        Assert.Null(denied.PolicyIds);
        Assert.Null(denied.RemovedModels);
        Assert.Null(denied.RemovedProviders);
    }

    [Fact]
    public void Leaves_the_arrays_undefined_when_param_is_an_array_field_with_non_string_entries()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Access policy denied: model openai/gpt-5 is not allowed by the access policies that apply to this request (policies: ap_1).","type":"access_policy_denied","param":{"policyIds":[1,2]}}}"""
            ),
            403
        );

        var denied = Assert.IsType<GatewayAccessPolicyDeniedException>(error);
        Assert.Equal(403, error.StatusCode);
        Assert.Null(denied.PolicyIds);
        Assert.Null(denied.RemovedModels);
        Assert.Null(denied.RemovedProviders);
    }
}
