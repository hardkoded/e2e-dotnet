---
title: "IOAuthProvider"
description: "The contract between a subscription login and the model clients: a provider signs the user in, refreshes what the login returned, and adjusts one API request..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IOAuthProvider"
  hidden: true
---

<a id="E2E_OAuth_IOAuthProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public interface IOAuthProvider
```


The contract between a subscription login and the model clients: a provider signs the user in, refreshes
what the login returned, and adjusts one API request the vendor's way. The credentials file, the handler that
swaps the token in, and the models are shared.

**Derived:** [CodexProvider](/api/E2E.OAuth/CodexProvider/), [CopilotProvider](/api/E2E.OAuth/CopilotProvider/), [OpenCodeConsoleProvider](/api/E2E.OAuth/OpenCodeConsoleProvider/), [XaiProvider](/api/E2E.OAuth/XaiProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L12)
