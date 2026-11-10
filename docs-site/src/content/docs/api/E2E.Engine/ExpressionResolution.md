---
title: "ExpressionResolution"
description: "The reference locator semantics over a semantic tree, as upstream's resolveExpression in engine/resolve.ts. An engine over such a tree (a device's accessibil..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExpressionResolution"
  hidden: true
---

<a id="E2E_Engine_ExpressionResolution"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class ExpressionResolution
```


The reference locator semantics over a semantic tree, as upstream's `resolveExpression` in `engine/resolve.ts`. An engine over such a
tree (a device's accessibility snapshot, an in-memory fake) calls this instead of interpreting the expression itself; an engine with a native
query engine (a browser) reproduces these rules. One immediate pass, every match in document order; polling, strictness, and staleness stay
with the runner. .NET-only holder name: C# has no free functions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Resolve.cs)

## Methods

<a id="E2E_Engine_ExpressionResolution_ResolveExpression_"></a>
<a id="E2E_Engine_ExpressionResolution_ResolveExpression_E2E_LocatorExpression_System_Collections_Generic_IReadOnlyList_E2E_Engine_SemanticNode__E2E_Engine_ResolveExpressionOptions_"></a>

### `ResolveExpression(LocatorExpression, IReadOnlyList<SemanticNode>, ResolveExpressionOptions?)`


```csharp
public static IReadOnlyList<SemanticNode> ResolveExpression(LocatorExpression expression, IReadOnlyList<SemanticNode> nodes, ResolveExpressionOptions? options = null)
```


Resolves one expression to the nodes it currently matches, in document order. &lt;code class="paramref"&gt;nodes&lt;/code&gt; are the tree's top-level nodes
(an observation root's children, or the root itself); every node under them is a candidate.
`frame` is `FRAME_NOT_FOUND`: a semantic tree has no nested documents.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [LocatorExpression](/api/E2E/LocatorExpression/) |  |
| `nodes` | [IReadOnlyList&lt;SemanticNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `options` | [ResolveExpressionOptions](/api/E2E.Engine/ResolveExpressionOptions/) |  |

**Returns:** [IReadOnlyList&lt;SemanticNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
