---
title: "ToGatewayStateParts"
description: "The AI SDK's to-gateway-state-parts.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToGatewayStateParts"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_ToGatewayStateParts"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public static class ToGatewayStateParts
```


The AI SDK's `to-gateway-state-parts.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/ToGatewayStateParts.cs)

## Methods

<a id="E2E_Decision_Sdk_Gateway_ToGatewayStateParts_Convert_"></a>
<a id="E2E_Decision_Sdk_Gateway_ToGatewayStateParts_Convert_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_DecisionStatePart__System_String_"></a>

### `Convert(IReadOnlyList<DecisionStatePart>, string)`


```csharp
public static JsonArray Convert(IReadOnlyList<DecisionStatePart> state, string modelType)
```


Upstream's `toGatewayStateParts`: the state as the Gateway takes it. Inline file bytes become base64, and file data that cannot be
sent (a URL or a provider reference) is rejected.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `state` | [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `modelType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [JsonArray](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonarray)
