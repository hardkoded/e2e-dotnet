---
title: "E2E.Internal"
description: "Public types in the `E2E.Internal` namespace."
editUrl: false
sidebar:
  label: "E2E.Internal"
---

[API reference](/api/)

Public types in the `E2E.Internal` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [Deadline](/api/E2E.Internal/Deadline/) | A point in time a wait must end by, as upstream's Deadline. |
| [JsonValueRules](/api/E2E.Internal/JsonValueRules/) | Upstream's JsonValueRules (internal/json-value.ts): what the JSON-safety walk refuses beyond the defaults. |
| [JsonValueValidation](/api/E2E.Internal/JsonValueValidation/) | The one JSON-safety walker (upstream's validateJsonValue in internal/json-value.ts). Anything crossing a boundary that demands plain JSON validates here, so cycle detection, plain-container checks, and secret rejection cannot drift between call sites. .NET-only holder name: C# has no free functions. |
| [LaunchArgumentRules](/api/E2E.Internal/LaunchArgumentRules/) | The checks and spellings of launch arguments (upstream's core/internal/launch-arguments.ts: checkLaunchArguments, engineLaunchArguments, appleUserDefaults). .NET-only holder name: C# has no free functions. |
| [PollConditionOptions](/api/E2E.Internal/PollConditionOptions/) | What PollConditionAsync(PollConditionOptions) polls, and how it ends. |
| [ResolvedLaunchArguments](/api/E2E.Internal/ResolvedLaunchArguments/) | Launch arguments after the checks: a list of strings, or an object of argument name to value (upstream's ResolvedLaunchArguments, readonly string[] \| LaunchArgumentValues). Exactly one of List and Values is set. .NET-only holder: C# has no union type. |
| [TextRules](/api/E2E.Internal/TextRules/) | Whitespace and string matching used by locators. Exact string matches are case-sensitive after whitespace is collapsed. Inexact matches are case-insensitive substrings, which is how exact: false behaves upstream. |
| [Time](/api/E2E.Internal/Time/) | Deadline and polling helpers, as upstream's internal/time.ts. |
| [Urls](/api/E2E.Internal/Urls/) | URL normalization and origin policy helpers. |
