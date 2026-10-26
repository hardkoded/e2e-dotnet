---
title: "VisionMode"
description: "What evidence a judgment (CancellationToken), CancellationToken), CancellationToken)) is given: the semantic tree, a masked screenshot of the current screen,..."
editUrl: false
prev: false
next: false
sidebar:
  label: "VisionMode"
  hidden: true
---

<a id="E2E_VisionMode"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum VisionMode
```


What evidence a judgment (`CancellationToken)`, `CancellationToken)`,
`CancellationToken)`) is given: the semantic tree, a masked screenshot of the current screen, or both.

&lt;p&gt;
[Only](#E2E_VisionMode_Only) exists because a tree sent beside pixels is a cheaper path to an answer, and a model takes it:
asked whether a form is covered by an overlay, it can read from the tree that the form is present and named and
answer yes, while the pixels show the overlay. For a judgment about what the screen presents, the tree is a
distractor, so the mode that means it removes it. It also costs fewer input tokens than [On](#E2E_VisionMode_On).
&lt;/p&gt;
&lt;p&gt;
In every mode that sends pixels and the tree, pixel evidence degrades away rather than failing the call when it
cannot be proven redacted. [Only](#E2E_VisionMode_Only) has nothing to degrade to, so it fails with `POLICY_DENIED`
instead of answering the wrong question from the tree.
&lt;/p&gt;
&lt;p&gt;
`CancellationToken)` takes no vision mode: the act model works from the tree.
&lt;/p&gt;

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/VisionMode.cs#L25)

## Fields

<a id="E2E_VisionMode_Off"></a>

### Off


```csharp
Off = 0
```


The tree. The default.

**Value:** [VisionMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/VisionMode.cs#L28)

<a id="E2E_VisionMode_On"></a>

### On


```csharp
On = 1
```


The tree and a masked screenshot, on every call.

**Value:** [VisionMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/VisionMode.cs#L31)

<a id="E2E_VisionMode_Only"></a>

### Only


```csharp
Only = 2
```


The masked screenshot, and not the tree.

**Value:** [VisionMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/VisionMode.cs#L34)
