---
title: "ResolveSecretOptions"
description: "What an engine asks of ResolveSecret beside the value. Upstream's ResolveSecretOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolveSecretOptions"
  hidden: true
---

<a id="E2E_Engine_ResolveSecretOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ResolveSecretOptions : IEquatable<ResolveSecretOptions>
```


What an engine asks of `ResolveSecret` beside the value. Upstream's `ResolveSecretOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ResolveSecretOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_ResolveSecretOptions_Derived_"></a>
<a id="E2E_Engine_ResolveSecretOptions_Derived"></a>

### Derived


```csharp
public Func<string, IReadOnlyList<string>>? Derived { get; init; }
```


The forms of the plaintext the engine hands to the app in its place, which are redacted wherever the value is.

**Value:** [Func&lt;string, IReadOnlyList&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_ResolveSecretOptions_DerivedAsync_"></a>
<a id="E2E_Engine_ResolveSecretOptions_DerivedAsync"></a>

### DerivedAsync


```csharp
public Func<string, Task<IReadOnlyList<string>>>? DerivedAsync { get; init; }
```


The asynchronous form of [Derived](#E2E_Engine_ResolveSecretOptions_Derived), for an engine that derives a credential by waiting on something, such as a
device. Upstream's `derived` may return a promise; C# keeps the synchronous callback and adds this one. When both are
set, the forms of both are redacted.

**Value:** [Func&lt;string, Task&lt;IReadOnlyList&lt;string&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)
