---
title: "LogWarnings"
description: "The AI SDK's logWarnings (logger/log-warnings.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LogWarnings"
  hidden: true
---

<a id="E2E_Decision_Sdk_LogWarnings"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class LogWarnings
```


The AI SDK's `logWarnings` (`logger/log-warnings.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LogWarnings.cs)

## Properties

<a id="E2E_Decision_Sdk_LogWarnings_AiSdkLogWarnings_"></a>
<a id="E2E_Decision_Sdk_LogWarnings_AiSdkLogWarnings"></a>

### AiSdkLogWarnings


```csharp
public static Action<LogWarningsOptions>? AiSdkLogWarnings { get; set; }
```


The AI SDK's `globalThis.AI_SDK_LOG_WARNINGS`: the logger that replaces the default one. Null uses the default.

**Value:** [Action&lt;LogWarningsOptions&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)
