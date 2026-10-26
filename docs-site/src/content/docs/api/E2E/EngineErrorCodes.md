---
title: "EngineErrorCodes"
description: "Stable engine error codes, as in the upstream engine contract."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineErrorCodes"
  hidden: true
---

<a id="E2E_EngineErrorCodes"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class EngineErrorCodes
```


Stable engine error codes, as in the upstream engine contract.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L166)

## Fields

<a id="E2E_EngineErrorCodes_ActionMayHaveCommitted"></a>

### ActionMayHaveCommitted


```csharp
public const string ActionMayHaveCommitted = "ACTION_MAY_HAVE_COMMITTED"
```


The input was dispatched before the failure, so the action may have taken effect. Do not repeat it blindly.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L179)

<a id="E2E_EngineErrorCodes_Cancelled"></a>

### Cancelled


```csharp
public const string Cancelled = "CANCELLED"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L184)

<a id="E2E_EngineErrorCodes_EngineFailure"></a>

### EngineFailure


```csharp
public const string EngineFailure = "ENGINE_FAILURE"
```


Any other engine failure.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L191)

<a id="E2E_EngineErrorCodes_FrameAmbiguous"></a>

### FrameAmbiguous


```csharp
public const string FrameAmbiguous = "FRAME_AMBIGUOUS"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L173)

<a id="E2E_EngineErrorCodes_FrameNotFound"></a>

### FrameNotFound


```csharp
public const string FrameNotFound = "FRAME_NOT_FOUND"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L171)

<a id="E2E_EngineErrorCodes_InvalidState"></a>

### InvalidState


```csharp
public const string InvalidState = "INVALID_STATE"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L188)

<a id="E2E_EngineErrorCodes_NodeStale"></a>

### NodeStale


```csharp
public const string NodeStale = "NODE_STALE"
```


The node or document a ref named is gone. Observe again and retry.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L169)

<a id="E2E_EngineErrorCodes_NotActionable"></a>

### NotActionable


```csharp
public const string NotActionable = "NOT_ACTIONABLE"
```


The target never became visible, enabled, stable, or editable. Nothing was dispatched.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L176)

<a id="E2E_EngineErrorCodes_OperationTimeout"></a>

### OperationTimeout


```csharp
public const string OperationTimeout = "OPERATION_TIMEOUT"
```


A navigation, read, or keyboard operation ran out of time. A timeout stays this code and is not retried, even when the call log mentions a detached element or a navigation.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L182)

<a id="E2E_EngineErrorCodes_UnsupportedCapability"></a>

### UnsupportedCapability


```csharp
public const string UnsupportedCapability = "UNSUPPORTED_CAPABILITY"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L186)

## Methods

<a id="E2E_EngineErrorCodes_IsRetryable_"></a>
<a id="E2E_EngineErrorCodes_IsRetryable_System_String_"></a>

### IsRetryable(string?)


```csharp
public static bool IsRetryable(string? code)
```


Whether &lt;code class="paramref"&gt;code&lt;/code&gt; describes a repeatable read and may be retried.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L194)
