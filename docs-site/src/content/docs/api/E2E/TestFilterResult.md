---
title: "TestFilterResult"
description: "A filter's answer: every test it does not name runs (upstream's TestFilterResult)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFilterResult"
  hidden: true
---

<a id="E2E_TestFilterResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TestFilterResult
```


A filter's answer: every test it does not name runs (upstream's `TestFilterResult`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L66)

## Properties

<a id="E2E_TestFilterResult_Exclude_"></a>
<a id="E2E_TestFilterResult_Exclude"></a>

### Exclude


```csharp
public required IReadOnlyDictionary<string, string> Exclude { get; init; }
```


The tests to leave out, by [Id](/api/E2E/TestFilterCandidate/#E2E_TestFilterCandidate_Id), each with the reason the report shows.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L69)

<a id="E2E_TestFilterResult_Note_"></a>
<a id="E2E_TestFilterResult_Note"></a>

### Note


```csharp
public string? Note { get; init; }
```


One line about the whole decision, such as `the diff is too large to judge`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L72)
