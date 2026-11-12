---
title: "ResolveExpressionOptions"
description: "Options for ResolveExpression: upstream's ResolveExpressionOptions (engine/resolve.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolveExpressionOptions"
  hidden: true
---

<a id="E2E_Engine_ResolveExpressionOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ResolveExpressionOptions : IEquatable<ResolveExpressionOptions>
```


Options for `ResolveExpression`: upstream's `ResolveExpressionOptions` (`engine/resolve.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ResolveExpressionOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Resolve.cs)

## Properties

<a id="E2E_Engine_ResolveExpressionOptions_Selector_"></a>
<a id="E2E_Engine_ResolveExpressionOptions_Selector"></a>

### Selector


```csharp
public Func<string, IReadOnlyList<SemanticNode>, IReadOnlyList<SemanticNode>>? Selector { get; init; }
```


Answers a `selector` expression: the nodes the platform-native selector addresses among the candidates, in document
order. Without it a selector is `UNSUPPORTED_CAPABILITY`, since a semantic tree has no selector language of its own.

**Value:** [Func&lt;string, IReadOnlyList&lt;SemanticNode&gt;, IReadOnlyList&lt;SemanticNode&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)
