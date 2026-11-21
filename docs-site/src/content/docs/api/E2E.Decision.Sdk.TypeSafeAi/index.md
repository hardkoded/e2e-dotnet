---
title: "E2E.Decision.Sdk.TypeSafeAi"
description: "Public types in the `E2E.Decision.Sdk.TypeSafeAi` namespace."
editUrl: false
sidebar:
  label: "E2E.Decision.Sdk.TypeSafeAi"
---

[API reference](/api/)

Public types in the `E2E.Decision.Sdk.TypeSafeAi` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/) | Answers questions through TypeSafe's SystemOne API, POST /systemone (the AI SDK's DecisionTypeSafeAiModel in @ai-sdk/typesafe-ai). It answers choice, score, boolean, and box questions, takes PNG and JPEG images, and reports each answer's confidence as typesafe provider metadata. |
| [SerializedModelOptions](/api/E2E.Decision.Sdk.TypeSafeAi/SerializedModelOptions/) | What crosses a workflow boundary for a model (the object upstream's serializeModelOptions returns). |
| [TypeSafeAiDecisionModelConfig](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiDecisionModelConfig/) | The config of a DecisionTypeSafeAiModel (upstream's TypeSafeAiDecisionModelConfig). |
| [TypeSafeAiDecisionModelOptions](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiDecisionModelOptions/) | The AI SDK's typesafeAiDecisionModelOptions: the typesafe provider options of a decision call. |
| [TypeSafeAiFilePartProviderOptions](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiFilePartProviderOptions/) | The AI SDK's typesafeAiFilePartProviderOptions: the typesafe options of an image file part. |
| [TypeSafeAiModelCatalog](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiModelCatalog/) | The AI SDK's typesafe-ai-model-catalog.ts: the decision models TypeSafe lists and the questions they answer. |
| [TypeSafeAiProvider](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiProvider/) | The AI SDK's createTypeSafeAi and the typeSafeAi provider (typesafe-ai-provider.ts), for TypeSafe's experimental decision capability: typeSafeAi.decisionModel('jev-latest'). |
| [TypeSafeAiProviderSettings](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiProviderSettings/) | The AI SDK's TypeSafeAiProviderSettings (typesafe-ai-provider.ts). |
