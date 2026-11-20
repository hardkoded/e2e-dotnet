---
title: "E2E.Decision"
description: "Public types in the `E2E.Decision` namespace."
editUrl: false
sidebar:
  label: "E2E.Decision"
---

[API reference](/api/)

Public types in the `E2E.Decision` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [ActionSpace](/api/E2E.Decision/ActionSpace/) | The element table and the targets bound to it (upstream's ActionSpace). |
| [Ask&lt;T&gt;](/api/E2E.Decision/Ask-1/) | One structured ask (upstream's Ask): the system prompt, how the input is introduced, the answer's shape, and how far back it reads. Schema is the closed JSON schema the provider is asked to fill, and Parse is what upstream's zod schema does with the answer: it returns null for one that does not match. |
| [BooleanAnswers](/api/E2E.Decision/BooleanAnswers/) | One call's boolean answers and what it spent (upstream's BooleanAnswers). |
| [Budget](/api/E2E.Decision/Budget/) | Upstream's budget.ts. |
| [Chosen&lt;T&gt;](/api/E2E.Decision/Chosen-1/) | What a target question settled (upstream's Chosen): the offered value, with the answer that chose it when one was asked, or why the step cannot go on (Blocked). |
| [Decide](/api/E2E.Decision/Decide/) | Upstream's decide.ts. |
| [Decision](/api/E2E.Decision/Decision/) | One gated answer: the chosen option, its probability, and the provider confidence. A score answer reads its most probable level as the choice, carries the probability-weighted level as Score, and its probability is the mass on the levels within half a level of that score: a spread over two adjacent columns is a precise position, not an uncertain one. |
| [DecisionAction](/api/E2E.Decision/DecisionAction/) | One action the loop dispatches, with everything its operation needs (upstream's Action). |
| [DecisionExecutorOptions](/api/E2E.Decision/DecisionExecutorOptions/) | Options for an executor that acts through a decision model and writes field text with a language model (upstream's DecisionExecutorOptions). |
| [DecisionFilterOptions](/api/E2E.Decision/DecisionFilterOptions/) | Options for a test filter that asks a decision model which tests a change can affect (upstream's DecisionFilterOptions). |
| [DecisionRequest](/api/E2E.Decision/DecisionRequest/) | One decide call's input (upstream's DecisionRequest in questions.ts). |
| [Destination](/api/E2E.Decision/Destination/) | One drop destination for drag, keyed like an element (upstream's Destination). |
| [Dispatch](/api/E2E.Decision/Dispatch/) | Upstream's dispatch.ts. |
| [DrawnFieldAnswer](/api/E2E.Decision/DrawnFieldAnswer/) | The answer of DrawnField: the drawn field and the string to enter in it. |
| [Element](/api/E2E.Decision/Element/) | One row of the element table the decision model reads (upstream's Element). |
| [Elements](/api/E2E.Decision/Elements/) | Upstream's elements.ts. Its free functions live here as members (the class carries the module's name, as Model and Budget do). |
| [Executor](/api/E2E.Decision/Executor/) | Upstream's executor.ts. |
| [FieldDescription](/api/E2E.Decision/FieldDescription/) | The field a text ask describes: the element's label, role, and value (upstream's FieldInput['field'], an inline type there). |
| [FieldInput](/api/E2E.Decision/FieldInput/) | What the text helpers read: the goal, the page, recent actions, and for a fill, the field (upstream's FieldInput). |
| [Filter](/api/E2E.Decision/Filter/) | Upstream's filter.ts. |
| [Gates](/api/E2E.Decision/Gates/) | The gates every answer passes: the selected option's probability and the provider's confidence (upstream's Gates). |
| [Grid](/api/E2E.Decision/Grid/) | A grid over an image: at most ten columns and rows, one score level per column and row (upstream's Grid). Sizes are in the CSS pixels of the image's coordinates. |
| [HistoryEntry](/api/E2E.Decision/HistoryEntry/) | One step of history the decision model reads (upstream's HistoryEntry). |
| [Located](/api/E2E.Decision/Located/) | Where a drawn control is, or why the looks could not say (upstream's Located): a point, or an uncertainty. |
| [Model](/api/E2E.Decision/Model/) | Upstream's model.ts. |
| [ModelNeeds](/api/E2E.Decision/ModelNeeds/) | What a factory needs from its decision model, and how its errors name the fix (upstream's ModelNeeds). |
| [Overlay](/api/E2E.Decision/Overlay/) | Upstream's overlay.ts. A viewport point is an Point, in CSS pixels. |
| [PathsAnswer](/api/E2E.Decision/PathsAnswer/) | The answer of Paths: the project-relative paths to attach. |
| [Picks](/api/E2E.Decision/Picks/) | Upstream's picks.ts. |
| [PointLocator](/api/E2E.Decision/PointLocator/) | Locates drawn controls on the screenshot and remembers the ones a tap found: three looks per control, the column and row on the plain full screenshot, then again on two ever tighter zoomed crops with a grid drawn on them; a field takes the first look alone. A tap that changed the pixels around it found its control; the same control on the same page is tapped there again with no new looks (upstream's PointLocator). |
| [Questions](/api/E2E.Decision/Questions/) | Upstream's questions.ts. |
| [Recorded&lt;T&gt;](/api/E2E.Decision/Recorded-1/) | What one model call came back with: its value, the tokens it used, and the model id the response named (upstream's Recorded). |
| [Screenshot](/api/E2E.Decision/Screenshot/) | Pixels as the decision model receives them: the runner's capture without its masking count (upstream's Screenshot, Omit<ExecutorPixels, 'maskedRegionCount'>). |
| [SpaceObservation](/api/E2E.Decision/SpaceObservation/) | What the action space reads of the screen (upstream's SpaceObservation). |
| [Target](/api/E2E.Decision/Target/) | One target of an operation: the node it acts on, as data. The model only ever names the key (upstream's Target). |
| [TargetAnswer](/api/E2E.Decision/TargetAnswer/) | The answer of Target: a short visual description of the control. |
| [Text](/api/E2E.Decision/Text/) | Upstream's text.ts. |
| [TextAnswer](/api/E2E.Decision/TextAnswer/) | The answer of TextAsk: the string to enter, or null when the goal supplies none. |
| [WireNames](/api/E2E.Decision/WireNames/) | The names an operation, a control, and a terminal carry in a question and an answer. .NET-only: upstream's are string unions. |
| [Zoom](/api/E2E.Decision/Zoom/) | A zoomed crop: the image, and where its top-left corner sits in the screenshot's CSS pixels (upstream's Zoom). |

## Enums

| Type | Summary |
| --- | --- |
| [Aim](/api/E2E.Decision/Aim/) | What a point look is after: a control to tap, or a field to type into (upstream's Aim). |
| [Control](/api/E2E.Decision/Control/) | Viewport, history, and keyboard controls, offered when the engine declares the verb; dismiss_keyboard only while an on-screen keyboard is showing (upstream's Control). |
| [Operation](/api/E2E.Decision/Operation/) | Operations the executor offers on elements (upstream's Operation). |
| [Terminal](/api/E2E.Decision/Terminal/) | Terminal choices: the step claims completion, reports a product failure, or gives up (upstream's Terminal). |
| [Verdict](/api/E2E.Decision/Verdict/) | What a verdict question can settle on (upstream's Verdict). |

## Delegates

| Type | Summary |
| --- | --- |
| [AskDecision](/api/E2E.Decision/AskDecision/) | Asks the decision model one request, or nothing once the step budget is spent (upstream's AskDecision). |
