---
title: "E2E.Decision.Sdk"
description: "Public types in the `E2E.Decision.Sdk` namespace."
editUrl: false
sidebar:
  label: "E2E.Decision.Sdk"
---

[API reference](/api/)

Public types in the `E2E.Decision.Sdk` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [ApiCallException](/api/E2E.Decision.Sdk/ApiCallException/) | The AI SDK's APICallError: a provider call that failed. RunAsync retries it when IsRetryable, waiting for the provider's retry-after-ms or retry-after header when it is reasonable. |
| [Channel](/api/E2E.Decision.Sdk/Channel/) | A named channel that delivers each published message to its subscribers (diagnosticsChannel.channel). |
| [CreateDefaultDownloadFunction](/api/E2E.Decision.Sdk/CreateDefaultDownloadFunction/) | The AI SDK's createDefaultDownloadFunction (util/download/download-function.ts). |
| [CreateDownload](/api/E2E.Decision.Sdk/CreateDownload/) | The AI SDK's createDownload (util/download/create-download.ts). |
| [CreateDownloadOptions](/api/E2E.Decision.Sdk/CreateDownloadOptions/) | What Create(CreateDownloadOptions) takes (the options of the AI SDK's createDownload). |
| [CustomProvider](/api/E2E.Decision.Sdk/CustomProvider/) | The decision models of the AI SDK's customProvider (custom-provider.ts): a provider that serves its own models and falls back to another provider. |
| [CustomProviderOptions](/api/E2E.Decision.Sdk/CustomProviderOptions/) | The decision members of the arguments of the AI SDK's customProvider (ai/src/registry/custom-provider.ts). |
| [DecideEndEvent](/api/E2E.Decision.Sdk/DecideEndEvent/) | The event passed to OnEnd (the AI SDK's DecideEndEvent), when the operation completes successfully. The result's response is flattened as on DecisionResult. |
| [DecideOptions](/api/E2E.Decision.Sdk/DecideOptions/) | What RunAsync takes (the arguments of the AI SDK's experimental_decide). |
| [DecideStartEvent](/api/E2E.Decision.Sdk/DecideStartEvent/) | The event passed to OnStart (the AI SDK's DecideStartEvent), when the operation begins, before the decision model is called. |
| [DecisionAnswer](/api/E2E.Decision.Sdk/DecisionAnswer/) | One model answer (the AI SDK's DecisionAnswer). Numbers are returned as the provider sent them. Type is the question's type, or refusal when the model declined the question. |
| [DecisionBox](/api/E2E.Decision.Sdk/DecisionBox/) | Edges in [0, 1], measured from the image's top-left corner. |
| [DecisionCapabilities](/api/E2E.Decision.Sdk/DecisionCapabilities/) | Upstream's ModelCapabilities.decision. |
| [DecisionChoice](/api/E2E.Decision.Sdk/DecisionChoice/) | One option of a choice question. |
| [DecisionFilePart](/api/E2E.Decision.Sdk/DecisionFilePart/) | A file, such as a screenshot, as bytes with its media type (the AI SDK's file part). The data is the bytes, or the same bytes as base64 text in Base64; DataType names another kind (url, reference, or text) a provider may reject. |
| [DecisionFileUrlPart](/api/E2E.Decision.Sdk/DecisionFileUrlPart/) | A file at a URL (the AI SDK's FilePart with URL data). ExperimentalDecide downloads it through ExperimentalDownload and hands the model a DecisionFilePart with the bytes. .NET-only name: C# has no union of bytes and a URL, so the URL form is its own part. |
| [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) | An instruction or a criterion description (the AI SDK's DecisionModelV4Input): text, or a JSON value the provider serializes into the prompt. A string converts implicitly. |
| [DecisionInputCapabilities](/api/E2E.Decision.Sdk/DecisionInputCapabilities/) | The decision inputs of ModelCapabilities.decision.inputs. |
| [DecisionJsonPart](/api/E2E.Decision.Sdk/DecisionJsonPart/) | A JSON value the model reads as data. |
| [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) | What DecisionModelCall hands a decision model (the AI SDK's DecisionModelV4CallOptions). |
| [DecisionModelCallEndEvent](/api/E2E.Decision.Sdk/DecisionModelCallEndEvent/) | The event fired after the decision model response has been validated (the AI SDK's DecisionModelCallEndEvent): the result of the logical model call, including any retries. |
| [DecisionModelCallStartEvent](/api/E2E.Decision.Sdk/DecisionModelCallStartEvent/) | The event fired when the decision model call begins (the AI SDK's DecisionModelCallStartEvent). The logical model call includes any provider retries. |
| [DecisionModelResult](/api/E2E.Decision.Sdk/DecisionModelResult/) | What a decision model answers a call with (the AI SDK's DecisionModelV4Result). |
| [DecisionQuestion](/api/E2E.Decision.Sdk/DecisionQuestion/) | One decision question (the AI SDK's DecisionModelV4Question). A choice question carries its options, a score question its ordered levels, and a boolean question instructions or at least one criterion. |
| [DecisionQuestionTypeCapabilities](/api/E2E.Decision.Sdk/DecisionQuestionTypeCapabilities/) | The decision question types of ModelCapabilities.decision.questionTypes. |
| [DecisionRefusalException](/api/E2E.Decision.Sdk/DecisionRefusalException/) | The AI SDK's Experimental_DecisionRefusalError: the model refused to answer some questions. |
| [DecisionResult](/api/E2E.Decision.Sdk/DecisionResult/) | What RunAsync returns (the AI SDK's DecisionResult). |
| [DecisionRounding](/api/E2E.Decision.Sdk/DecisionRounding/) | Declared rounding precision of the numbers a model returns (decimal places, 0 through 15). |
| [DecisionStatePart](/api/E2E.Decision.Sdk/DecisionStatePart/) | A part of the state every question of a decision call shares (the AI SDK's DecisionModelV4State). |
| [DecisionTextPart](/api/E2E.Decision.Sdk/DecisionTextPart/) | Plain text. |
| [DecisionUnsupportedQuestionTypeException](/api/E2E.Decision.Sdk/DecisionUnsupportedQuestionTypeException/) | The AI SDK's Experimental_DecisionUnsupportedQuestionTypeError: the model cannot answer a question's type. |
| [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/) | Tokens a decision call used. |
| [DiagnosticsChannel](/api/E2E.Decision.Sdk/DiagnosticsChannel/) | .NET-only stand-in for Node's node:diagnostics_channel, which the AI SDK's tracing channel publishes to (tracing-channel-publisher.ts). A tracing channel is found by name, and every caller that asks for a name gets the same one. |
| [Download](/api/E2E.Decision.Sdk/Download/) | The AI SDK's Download type: the function Create(CreateDownloadOptions) returns. Called with a single URL, it downloads that URL. Called with an array of requests, it behaves like the default experimental_download of decide: URLs the model supports are passed through (null), all others are downloaded. A Download converts to a DownloadFunction, so it can be passed as ExperimentalDownload. |
| [DownloadedFile](/api/E2E.Decision.Sdk/DownloadedFile/) | A file a DownloadFunction downloaded. |
| [DownloadException](/api/E2E.Decision.Sdk/DownloadException/) | The AI SDK's DownloadError (@ai-sdk/provider-utils): a file the SDK could not download. |
| [DownloadRequest](/api/E2E.Decision.Sdk/DownloadRequest/) | One URL a DownloadFunction is asked for (an element of the options array of upstream's DownloadFunction). |
| [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/) | A decision model that also has the deprecated doEvaluate (upstream's DecisionModelV4 & { doEvaluate }, what asEvaluationModel returns). .NET-only: C# needs a named type for the intersection. |
| [ExperimentalDecide](/api/E2E.Decision.Sdk/ExperimentalDecide/) | The AI SDK's experimental_decide (ai/src/decide/decide.ts) and its input and answer validation (validate-decision.ts), which the SDK keeps in its ai package and the port has no such package for. .NET-only home. |
| [InvalidArgumentException](/api/E2E.Decision.Sdk/InvalidArgumentException/) | The AI SDK's InvalidArgumentError: a call argument the SDK rejects before any request. |
| [InvalidResponseDataException](/api/E2E.Decision.Sdk/InvalidResponseDataException/) | The AI SDK's InvalidResponseDataError: the provider's answers break the contract of the questions. |
| [LoadApiKeyException](/api/E2E.Decision.Sdk/LoadApiKeyException/) | The AI SDK's LoadAPIKeyError: the provider has no API key. |
| [ModelCapabilities](/api/E2E.Decision.Sdk/ModelCapabilities/) | Upstream's ModelCapabilities. Only the decision section exists in the port. |
| [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) | The AI SDK's ModelCapability: true, false, 'unknown', or { condition }. A condition is documentation, not an expression to execute. |
| [ModelEntry](/api/E2E.Decision.Sdk/ModelEntry/) | Upstream's ModelEntry: one provider-owned model record. |
| [ModelProviderOptions](/api/E2E.Decision.Sdk/ModelProviderOptions/) | Upstream's ModelProviderOptions: per-model support for a provider's options, keyed by section and option name. |
| [NoSuchModelException](/api/E2E.Decision.Sdk/NoSuchModelException/) | The AI SDK's NoSuchModelError: no model of that type has that id. |
| [NoSuchProviderException](/api/E2E.Decision.Sdk/NoSuchProviderException/) | The AI SDK's NoSuchProviderError: the registry has no provider with that id. |
| [OpenAIDecisionModel](/api/E2E.Decision.Sdk/OpenAIDecisionModel/) | Answers questions through the OpenAI Decisions API, POST /decisions (the AI SDK's OpenAIDecisionModel in @ai-sdk/openai). It answers choice, score, and boolean questions, and the answers carry the model's probabilities. |
| [OpenAIDecisionModelOptions](/api/E2E.Decision.Sdk/OpenAIDecisionModelOptions/) | Settings of an OpenAIDecisionModel (the AI SDK's createOpenAI settings it reads). |
| [ProviderOptionDefinition](/api/E2E.Decision.Sdk/ProviderOptionDefinition/) | A provider option, declared once at the provider level. Upstream's ProviderOptionDefinition. |
| [ProviderOptionDefinitions](/api/E2E.Decision.Sdk/ProviderOptionDefinitions/) | Upstream's ProviderOptionDefinitions: the provider options a provider accepts, grouped by operation. |
| [ProviderRegistry](/api/E2E.Decision.Sdk/ProviderRegistry/) | The AI SDK's createProviderRegistry and DefaultProviderRegistry (provider-registry.ts), for decision models. |
| [ProviderRegistryOptions](/api/E2E.Decision.Sdk/ProviderRegistryOptions/) | The decision members of the options of the AI SDK's createProviderRegistry (ai/src/registry/provider-registry.ts). |
| [ResolveModel](/api/E2E.Decision.Sdk/ResolveModel/) | The AI SDK's packages/ai/src/model/resolve-model.ts (resolveDecisionModel, asEvaluationModel). |
| [TelemetryErrorEvent](/api/E2E.Decision.Sdk/TelemetryErrorEvent/) | The event a telemetry integration's OnErrorAsync(TelemetryErrorEvent) receives when the operation fails. |
| [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/) | What a telemetry integration adds to every event it receives (the AI SDK's augmentEvent of create-telemetry-dispatcher.ts): the call's telemetry settings and the SDK version. Null on an event a caller's OnStart or OnEnd callback receives. |
| [TelemetryOptions](/api/E2E.Decision.Sdk/TelemetryOptions/) | Telemetry configuration (the AI SDK's TelemetryOptions, telemetry/telemetry-options.ts). |
| [TelemetryRegistry](/api/E2E.Decision.Sdk/TelemetryRegistry/) | The AI SDK's global telemetry integration registry (telemetry/telemetry-registry.ts). A decide call that sets no Integrations uses the integrations registered here. |
| [TelemetryTracingChannel](/api/E2E.Decision.Sdk/TelemetryTracingChannel/) | The AI SDK's tracing channel definitions (telemetry/tracing-channel.ts). |
| [TelemetryTracingChannelMessage](/api/E2E.Decision.Sdk/TelemetryTracingChannelMessage/) | TelemetryTracingChannelMessage: what a tracing channel publishes. Result and Error are set, as Node does on the message, before asyncEnd (and error) are published. |
| [TelemetryTracingEventType](/api/E2E.Decision.Sdk/TelemetryTracingEventType/) | TelemetryTracingEventType: the type of a TelemetryTracingChannelMessage. |
| [TracingChannel](/api/E2E.Decision.Sdk/TracingChannel/) | The five channels of a tracing channel (diagnosticsChannel.TracingChannel): start, end, asyncStart, asyncEnd and error. Each is a Channel named tracing:{name}:{event}, as in Node. |
| [TracingChannelSubscribers](/api/E2E.Decision.Sdk/TracingChannelSubscribers/) | The subscribers of a TracingChannel (TracingChannelSubscribers). Each one is optional. |
| [UnsupportedFunctionalityException](/api/E2E.Decision.Sdk/UnsupportedFunctionalityException/) | The AI SDK's UnsupportedFunctionalityError: the provider cannot take an input, such as a file of an unsupported media type. |

## Interfaces

| Type | Summary |
| --- | --- |
| [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) | A model that answers typed questions with probabilities (the AI SDK's Experimental_DecisionModelV4). Only specification version v4 is accepted. |
| [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/) | A provider that creates decision models (the AI SDK's DecisionProvider, ai/src/decide/decision-provider.ts). Both members are optional, as upstream's: a provider overrides the ones it has. A member that is not overridden returns null. |
| [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/) | A deprecated decision model shape (the AI SDK's Experimental_EvaluationModelV4): the same model with doEvaluate in place of doDecide. ResolveDecisionModel(IEvaluationModel) adapts it to an IDecisionModel. |
| [IProviderRegistryProvider](/api/E2E.Decision.Sdk/IProviderRegistryProvider/) | The decision members of the AI SDK's ProviderRegistryProvider (provider-registry.ts): models by providerId:modelId. |
| [ITelemetry](/api/E2E.Decision.Sdk/ITelemetry/) | A telemetry integration (the decide members of the AI SDK's Telemetry, telemetry/telemetry.ts). Every member does nothing by default, so an integration overrides the ones it listens to. |
| [ITelemetryRedactable](/api/E2E.Decision.Sdk/ITelemetryRedactable/) | A value that carries a provider-owned telemetry view (the AI SDK's withTelemetryRedaction, provider-utils/src/telemetry-redaction.ts). .NET-only name: upstream attaches the view to an object with a symbol; a .NET type implements this interface instead. The original value is never changed. |

## Delegates

| Type | Summary |
| --- | --- |
| [DownloadFile](/api/E2E.Decision.Sdk/DownloadFile/) | The type of the AI SDK's download (typeof originalDownload), which Create takes. Receives the requested URL and the signal of the enclosing call. |
| [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/) | The AI SDK's DownloadFunction (util/download/download-function.ts), experimental. Called with the requested URLs, it answers one entry for each, in the same order: the downloaded file, or null when the URL goes to the model as is. It throws DownloadException when a download fails. |
| [FetchFunction](/api/E2E.Decision.Sdk/FetchFunction/) | The AI SDK's FetchFunction (@ai-sdk/provider-utils): the transport a download goes through. It must not follow redirects itself, because every hop is validated before it is requested. |
