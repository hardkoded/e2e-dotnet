---
title: "OAuthLoginCallbacks"
description: "How a login flow talks to the terminal, or whatever hosts it."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthLoginCallbacks"
  hidden: true
---

<a id="E2E_OAuth_OAuthLoginCallbacks"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OAuthLoginCallbacks
```


How a login flow talks to the terminal, or whatever hosts it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L39)

## Properties

<a id="E2E_OAuth_OAuthLoginCallbacks_OnAuth_"></a>
<a id="E2E_OAuth_OAuthLoginCallbacks_OnAuth"></a>

### OnAuth


```csharp
public required Action<OAuthAuthInfo> OnAuth { get; init; }
```


The URL to open and how to finish there. Called once per flow.

**Value:** [Action&lt;OAuthAuthInfo&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L42)

<a id="E2E_OAuth_OAuthLoginCallbacks_OnProgress_"></a>
<a id="E2E_OAuth_OAuthLoginCallbacks_OnProgress"></a>

### OnProgress


```csharp
public Action<string>? OnProgress { get; init; }
```


**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L47)

<a id="E2E_OAuth_OAuthLoginCallbacks_OnPrompt_"></a>
<a id="E2E_OAuth_OAuthLoginCallbacks_OnPrompt"></a>

### OnPrompt


```csharp
public required Func<string, CancellationToken, Task<string>> OnPrompt { get; init; }
```


Asks the user for text: the pasted code when the local callback never arrives.

**Value:** [Func&lt;string, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L45)
