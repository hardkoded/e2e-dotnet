---
title: "Text"
description: "Upstream's text.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Text"
  hidden: true
---

<a id="E2E_Decision_Text"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Text
```


Upstream's `text.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Properties

<a id="E2E_Decision_Text_DrawnField_"></a>
<a id="E2E_Decision_Text_DrawnField"></a>

### DrawnField


```csharp
public static Ask<DrawnFieldAnswer> DrawnField { get; }
```


The drawn field to fill next and the exact string to enter, in one ask: the field as a short visual description the point questions
locate, the text as [TextAsk](#E2E_Decision_Text_TextAsk) takes it from the goal (upstream's `DRAWN_FIELD`).

**Value:** `Ask&lt;DrawnFieldAnswer&gt;`

<a id="E2E_Decision_Text_Paths_"></a>
<a id="E2E_Decision_Text_Paths"></a>

### Paths


```csharp
public static Ask<PathsAnswer> Paths { get; }
```


The project-relative paths to attach to a file input; empty when the goal supplies none (upstream's `PATHS`).

**Value:** `Ask&lt;PathsAnswer&gt;`

<a id="E2E_Decision_Text_Target_"></a>
<a id="E2E_Decision_Text_Target"></a>

### Target


```csharp
public static Ask<TargetAnswer> Target { get; }
```


The drawn control to tap next, as a short visual description the point questions then locate (upstream's `TARGET`). A decision
model picks among offered choices; which key of a drawn keypad comes next is the text model's call.

**Value:** `Ask&lt;TargetAnswer&gt;`

<a id="E2E_Decision_Text_TextAsk_"></a>
<a id="E2E_Decision_Text_TextAsk"></a>

### TextAsk


```csharp
public static Ask<TextAnswer> TextAsk { get; }
```


The exact string to enter in a field; empty or null when the goal supplies none (upstream's `TEXT`).

**Value:** `Ask&lt;TextAnswer&gt;`

## Methods

<a id="E2E_Decision_Text_AskTextAsync_"></a>
<a id="E2E_Decision_Text_AskTextAsync__1_E2E_StepExecutorContext_E2E_IAgentModel_E2E_Decision_Ask___0__E2E_Decision_FieldInput_System_Threading_CancellationToken_"></a>

### `AskTextAsync<T>(StepExecutorContext, IAgentModel, Ask<T>, FieldInput, CancellationToken)`


```csharp
public static Task<T> AskTextAsync<T>(StepExecutorContext ctx, IAgentModel model, Ask<T> spec, FieldInput input, CancellationToken cancellationToken = default)
```


Upstream's `askText`. One structured call to the text model, recorded against the step budget and mapped onto the runner's error
codes. Never called for password fields; the harness still authorizes every path an upload names.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `model` | [IAgentModel](/api/E2E/IAgentModel/) |  |
| `spec` | `Ask&lt;T&gt;` |  |
| `input` | [FieldInput](/api/E2E.Decision/FieldInput/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
