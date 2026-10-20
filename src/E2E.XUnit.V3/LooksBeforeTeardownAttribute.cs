// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit.v3;

namespace E2E.XUnit.V3;

/// <summary>
/// Runs right after the test method, so the app is looked at the moment the body fails, as upstream does. The test
/// class is disposed after this, and a derived <c>DisposeAsync</c> is the teardown that could navigate away first.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class LooksBeforeTeardownAttribute : BeforeAfterTestAttribute
{
    public override void After(MethodInfo methodUnderTest, IXunitTest test) => E2ETest.LookAtFailure(test);
}
