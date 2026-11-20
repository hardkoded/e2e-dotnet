---
title: "Verdict"
description: "What a verdict question can settle on (upstream's Verdict)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Verdict"
  hidden: true
---

<a id="E2E_Decision_Verdict"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public enum Verdict
```


What a verdict question can settle on (upstream's `Verdict`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Picks.cs)

## Fields

<a id="E2E_Decision_Verdict_BelowGate"></a>

### BelowGate


```csharp
BelowGate = 3
```


The answer failed the gates (upstream's `'below gate'` string).

**Value:** [Verdict](./)

<a id="E2E_Decision_Verdict_Fails"></a>

### Fails


```csharp
Fails = 1
```


**Value:** [Verdict](./)

<a id="E2E_Decision_Verdict_Holds"></a>

### Holds


```csharp
Holds = 0
```


**Value:** [Verdict](./)

<a id="E2E_Decision_Verdict_Inconclusive"></a>

### Inconclusive


```csharp
Inconclusive = 2
```


**Value:** [Verdict](./)
