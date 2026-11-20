---
title: "E2E.Decision.Sdk.Gateway"
description: "Public types in the `E2E.Decision.Sdk.Gateway` namespace."
editUrl: false
sidebar:
  label: "E2E.Decision.Sdk.Gateway"
---

[API reference](/api/)

Public types in the `E2E.Decision.Sdk.Gateway` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [GatewayAccessPolicyDeniedException](/api/E2E.Decision.Sdk.Gateway/GatewayAccessPolicyDeniedException/) | Access policy denied: an AI Gateway access policy does not allow the requested model or provider. |
| [GatewayAuthenticationException](/api/E2E.Decision.Sdk.Gateway/GatewayAuthenticationException/) | Authentication failed: an invalid API key or OIDC token. |
| [GatewayAuthToken](/api/E2E.Decision.Sdk.Gateway/GatewayAuthToken/) | The token and method GetGatewayAuthTokenAsync gives. |
| [GatewayConfig](/api/E2E.Decision.Sdk.Gateway/GatewayConfig/) | The AI SDK's GatewayConfig (gateway-config.ts): where and how a Gateway model sends its requests. |
| [GatewayCreditsResponse](/api/E2E.Decision.Sdk.Gateway/GatewayCreditsResponse/) | Upstream's GatewayCreditsResponse. |
| [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/) | Answers questions through the Vercel AI Gateway, POST /decision-model (the AI SDK's GatewayDecisionModel in @ai-sdk/gateway). It answers choice, score, and boolean questions; the Gateway routes the call to the model named by the id, such as typesafe-ai/jev, and applies the gateway provider options (fallbacks, routing). |
| [GatewayDecisionModelConfig](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModelConfig/) | The config of a GatewayDecisionModel (upstream's GatewayConfig & { provider, o11yHeaders }). |
| [GatewayDecisionModelId](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModelId/) | The AI SDK's gateway-decision-model-settings.ts: the decision model ids the Gateway lists. Any other string is accepted too. |
| [GatewayErrors](/api/E2E.Decision.Sdk.Gateway/GatewayErrors/) | The AI SDK's createGatewayErrorFromResponse and asGatewayError. |
| [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/) | The AI SDK's GatewayError (packages/gateway/src/errors): a failure of the Vercel AI Gateway. |
| [GatewayFailedDependencyException](/api/E2E.Decision.Sdk.Gateway/GatewayFailedDependencyException/) | Failed dependency. |
| [GatewayFetchMetadata](/api/E2E.Decision.Sdk.Gateway/GatewayFetchMetadata/) | The AI SDK's GatewayFetchMetadata (gateway-fetch-metadata.ts): the models and the credits of the Gateway. |
| [GatewayFetchMetadataResponse](/api/E2E.Decision.Sdk.Gateway/GatewayFetchMetadataResponse/) | Upstream's GatewayFetchMetadataResponse. |
| [GatewayForbiddenException](/api/E2E.Decision.Sdk.Gateway/GatewayForbiddenException/) | Forbidden: the request was rejected by policy (for example a routing rule), not an authentication failure. |
| [GatewayGenerationInfo](/api/E2E.Decision.Sdk.Gateway/GatewayGenerationInfo/) | Upstream's GatewayGenerationInfo: the cost, token usage, latency, and provider of one generation. |
| [GatewayGenerationInfoFetcher](/api/E2E.Decision.Sdk.Gateway/GatewayGenerationInfoFetcher/) | The AI SDK's GatewayGenerationInfoFetcher (gateway-generation-info.ts): GET {origin}/v1/generation?id=.... |
| [GatewayGenerationInfoParams](/api/E2E.Decision.Sdk.Gateway/GatewayGenerationInfoParams/) | Upstream's GatewayGenerationInfoParams. |
| [GatewayInternalServerException](/api/E2E.Decision.Sdk.Gateway/GatewayInternalServerException/) | Internal server error from the Gateway. |
| [GatewayInvalidRequestException](/api/E2E.Decision.Sdk.Gateway/GatewayInvalidRequestException/) | Invalid request: missing headers, malformed data, and so on. |
| [GatewayLanguageModelEntry](/api/E2E.Decision.Sdk.Gateway/GatewayLanguageModelEntry/) | The AI SDK's GatewayLanguageModelEntry: a model the Gateway offers. |
| [GatewayLanguageModelSpecification](/api/E2E.Decision.Sdk.Gateway/GatewayLanguageModelSpecification/) | Upstream's GatewayLanguageModelSpecification: Pick<LanguageModelV4, 'specificationVersion' \| 'provider' \| 'modelId'>. |
| [GatewayModelEntryTypes](/api/E2E.Decision.Sdk.Gateway/GatewayModelEntryTypes/) | The AI SDK's GatewayLanguageModelEntry and KNOWN_MODEL_TYPES (gateway-model-entry.ts). |
| [GatewayModelNotFoundException](/api/E2E.Decision.Sdk.Gateway/GatewayModelNotFoundException/) | Model not found or not available. |
| [GatewayModelPricing](/api/E2E.Decision.Sdk.Gateway/GatewayModelPricing/) | Upstream's GatewayLanguageModelEntry.pricing: the cost of a model in USD. |
| [GatewayNotFoundException](/api/E2E.Decision.Sdk.Gateway/GatewayNotFoundException/) | Resource not found. |
| [GatewayProvider](/api/E2E.Decision.Sdk.Gateway/GatewayProvider/) | The AI SDK's createGateway and the gateway provider (gateway-provider.ts), for its decision models: gateway.decisionModel('typesafe-ai/jev'). |
| [GatewayProviderOptions](/api/E2E.Decision.Sdk.Gateway/GatewayProviderOptions/) | The AI SDK's gateway-provider-options.ts: the check of the gateway provider options a decision call takes. The models fallback list is validated (at most one conditional entry, and it comes first); every other option is the Gateway's own and passes through. |
| [GatewayProviderSettings](/api/E2E.Decision.Sdk.Gateway/GatewayProviderSettings/) | The AI SDK's GatewayProviderSettings (gateway-provider.ts) the decision models read. |
| [GatewayRateLimitException](/api/E2E.Decision.Sdk.Gateway/GatewayRateLimitException/) | Rate limit exceeded. |
| [GatewayResponseException](/api/E2E.Decision.Sdk.Gateway/GatewayResponseException/) | Gateway response parsing error: the error body is not the shape the Gateway documents. |
| [GatewaySpendReport](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReport/) | The AI SDK's GatewaySpendReport (gateway-spend-report.ts): GET {origin}/v1/report. |
| [GatewaySpendReportParams](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReportParams/) | Upstream's GatewaySpendReportParams. |
| [GatewaySpendReportResponse](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReportResponse/) | Upstream's GatewaySpendReportResponse. |
| [GatewaySpendReportRow](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReportRow/) | Upstream's GatewaySpendReportRow. |
| [GatewayTimeoutException](/api/E2E.Decision.Sdk.Gateway/GatewayTimeoutException/) | Client request timed out before receiving a response. |
| [ToGatewayStateParts](/api/E2E.Decision.Sdk.Gateway/ToGatewayStateParts/) | The AI SDK's to-gateway-state-parts.ts. |
