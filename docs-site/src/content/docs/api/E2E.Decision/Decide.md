---
title: "Decide"
description: "Upstream's decide.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Decide"
  hidden: true
---

<a id="E2E_Decision_Decide"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Decide
```


Upstream's `decide.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Decide.cs)

## Methods

<a id="E2E_Decision_Decide_DecideAsync_"></a>
<a id="E2E_Decision_Decide_DecideAsync_E2E_StepExecutorContext_E2E_Decision_Sdk_IDecisionModel_E2E_Decision_DecisionRequest_System_Collections_Generic_IReadOnlyDictionary_System_String_System_Text_Json_JsonElement__System_Threading_CancellationToken_"></a>

### `DecideAsync(StepExecutorContext, IDecisionModel, DecisionRequest, IReadOnlyDictionary<string, JsonElement>?, CancellationToken)`


```csharp
public static Task<IReadOnlyDictionary<string, Decision>> DecideAsync(StepExecutorContext ctx, IDecisionModel model, DecisionRequest request, IReadOnlyDictionary<string, JsonElement>? providerOptions = null, CancellationToken cancellationToken = default)
```


Upstream's `decide`. Makes one decide call under the step budget and returns the answers. This is the only file that touches the
decide API, so a rename there stays a one-file change.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `model` | [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) |  |
| `request` | [DecisionRequest](/api/E2E.Decision/DecisionRequest/) |  |
| `providerOptions` | [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyDictionary&lt;string, Decision&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Decide_DecideBooleansAsync_"></a>
<a id="E2E_Decision_Decide_DecideBooleansAsync_E2E_Decision_DecisionFilterOptions_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_DecisionStatePart__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_Threading_CancellationToken_"></a>

### `DecideBooleansAsync(DecisionFilterOptions, IReadOnlyList<DecisionStatePart>, IReadOnlyDictionary<string, string>, CancellationToken)`


```csharp
public static Task<BooleanAnswers> DecideBooleansAsync(DecisionFilterOptions options, IReadOnlyList<DecisionStatePart> state, IReadOnlyDictionary<string, string> questions, CancellationToken abortSignal)
```


Upstream's `decideBooleans`. Asks &lt;code class="paramref"&gt;questions&lt;/code&gt;, instructions by id, as boolean questions in one decide call
against &lt;code class="paramref"&gt;state&lt;/code&gt;, for the test filter.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [DecisionFilterOptions](/api/E2E.Decision/DecisionFilterOptions/) |  |
| `state` | [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `questions` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `abortSignal` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BooleanAnswers&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
