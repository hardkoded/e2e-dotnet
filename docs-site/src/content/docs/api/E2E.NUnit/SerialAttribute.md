---
title: "SerialAttribute"
description: "Makes the tests of an E2ETest fixture a serial group, as upstream's test.describe(..., { serial: true }): the members share one session, so the page state of..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SerialAttribute"
  hidden: true
---

<a id="E2E_NUnit_SerialAttribute"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SerialAttribute : Attribute
```


Makes the tests of an [E2ETest](/api/E2E.NUnit/E2ETest/) fixture a serial group, as upstream's `test.describe(..., { serial: true })`: the members
share one session, so the page state of one member is still there in the next, and the fixture's fields carry values between them.
.NET-only shape (the port has no serial groups, the runner owns them): NUnit runs the members, so give them
`[DependsOnTest]` to fix their sequence. After a member fails, the later members are skipped. The session starts with the first member,
commits the replay cache at the end of the group, and is the first member's, so its title, `[Agent]` and `[AgentContext]` apply to all.
Do not combine it with `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` or with tests running in parallel in the fixture.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/SerialAttribute.cs)
