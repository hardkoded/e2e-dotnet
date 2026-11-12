---
title: "SecretProvider"
description: "Resolves a secret's plaintext at fill time (a vault lookup, a freshly computed TOTP) instead of a value baked at config load (upstream's SecretProvider, conf..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SecretProvider"
  hidden: true
---

<a id="E2E_SecretProvider"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public delegate Task<string> SecretProvider(CancellationToken cancellationToken)
```


Resolves a secret's plaintext at fill time (a vault lookup, a freshly computed TOTP) instead of a value baked at config load (upstream's
`SecretProvider`, `config/secrets.ts`: `() => string | Promise<string>`). Called on every fill after the full
authorization policy passes; the resolved value goes straight to the trusted driver, joins redaction, and is never logged, cached, or
sent to a model. The token is .NET's cancellation: upstream's provider takes none.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | Cancels the lookup when the step is cancelled or times out. |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/ConfigSecrets.cs)
