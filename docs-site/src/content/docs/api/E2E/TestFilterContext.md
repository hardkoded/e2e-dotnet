---
title: "TestFilterContext"
description: "What the host hands a filter besides its tests (upstream's TestFilterContext)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFilterContext"
  hidden: true
---

<a id="E2E_TestFilterContext"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TestFilterContext
```


What the host hands a filter besides its tests (upstream's `TestFilterContext`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L40)

## Properties

<a id="E2E_TestFilterContext_Changes_"></a>
<a id="E2E_TestFilterContext_Changes"></a>

### Changes


```csharp
public required TestFilterChanges Changes { get; init; }
```


The change the filter judges; the same for every filter of the run.

**Value:** [TestFilterChanges](/api/E2E/TestFilterChanges/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L46)

<a id="E2E_TestFilterContext_ConfigFile_"></a>
<a id="E2E_TestFilterContext_ConfigFile"></a>

### ConfigFile


```csharp
public string? ConfigFile { get; init; }
```


The config file this run loaded, whatever its name, as a POSIX path relative to `Changes.Root` like `Changes.Files`, in its
on-disk bytes (compare in NFC, as with a test's `File`); null when the run was given a config object or loaded one outside the
repository.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L53)

<a id="E2E_TestFilterContext_Log_"></a>
<a id="E2E_TestFilterContext_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


Prints one progress line as a run notice.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L59)

<a id="E2E_TestFilterContext_ProjectRoot_"></a>
<a id="E2E_TestFilterContext_ProjectRoot"></a>

### ProjectRoot


```csharp
public required string ProjectRoot { get; init; }
```


Absolute project root, spelled as on disk like [Changes](#E2E_TestFilterContext_Changes).`Root`: symlinks resolved, names in on-disk case; a test's `File` is relative to it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L43)

<a id="E2E_TestFilterContext_Redact_"></a>
<a id="E2E_TestFilterContext_Redact"></a>

### Redact


```csharp
public required Func<string, string> Redact { get; init; }
```


Replaces every fixed-value secret and credential password in the text, and any run of 8 or more consecutive characters of one.

**Value:** [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L62)

<a id="E2E_TestFilterContext_Signal_"></a>
<a id="E2E_TestFilterContext_Signal"></a>

### Signal


```csharp
public required CancellationToken Signal { get; init; }
```


Cancelled when the run is interrupted, or after 5 minutes; hand it to every model and network call.

**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L56)
