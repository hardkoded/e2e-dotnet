---
title: "DerivedReason"
description: "Upstream's DerivedReason (cache/trace.ts): why a typed value was this run's data rather than the flow's, by the rule that flagged it (agent/derived.ts): read..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DerivedReason"
  hidden: true
---

<a id="E2E_DerivedReason"></a>

Namespace: [E2E](/api/E2E/)


```csharp
[JsonConverter(typeof(DerivedReasonConverter))]
public enum DerivedReason
```


Upstream's `DerivedReason` (`cache/trace.ts`): why a typed value was this run's data rather than the flow's, by the rule
that flagged it (`agent/derived.ts`): read off a screenshot, the whole name or text of a node, a token with a digit shown as a
word of its own, or a date or time reckoned from today. An entry stores it as `pixels`, `whole-node`,
`minted-token`, or `date` on a `tool` action.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs)

## Fields

<a id="E2E_DerivedReason_Date"></a>

### Date


```csharp
Date = 3
```


A date or a clock time, which the model reckons from today.

**Value:** [DerivedReason](./)

<a id="E2E_DerivedReason_MintedToken"></a>

### MintedToken


```csharp
MintedToken = 2
```


The value is data-shaped and some node showed it as a word of its own.

**Value:** [DerivedReason](./)

<a id="E2E_DerivedReason_Pixels"></a>

### Pixels


```csharp
Pixels = 0
```


The model was shown a screenshot this step, so a value that is not literal may have been read off the image.

**Value:** [DerivedReason](./)

<a id="E2E_DerivedReason_WholeNode"></a>

### WholeNode


```csharp
WholeNode = 1
```


The value is the whole of what some node said.

**Value:** [DerivedReason](./)
