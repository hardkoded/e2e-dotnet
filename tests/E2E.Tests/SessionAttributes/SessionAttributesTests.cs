// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SessionAttributes;

/// <summary>
/// Which saved session a test restores, from <see cref="E2ESessionAttribute"/> on its method and classes (upstream's <c>session</c> option of
/// <c>test</c> and <c>describe</c>). These are .NET-only tests: the attributes are the port's spelling of the option.
/// </summary>
public sealed class SessionAttributesTests
{
    [E2ESession("class-session")]
    private class Base
    {
        [E2ESession("method-session")]
        public void Pinned()
        {
        }

        public void Plain()
        {
        }
    }

    private sealed class Derived : Base;

    private sealed class Unmarked
    {
        public void Plain()
        {
        }
    }

    private sealed class SetupTest
    {
        public void Body()
        {
        }
    }

    [Fact]
    public void The_method_wins_over_its_class()
    {
        Assert.Equal("method-session", global::E2E.SessionAttributes.Restore(typeof(Base), typeof(Base).GetMethod(nameof(Base.Pinned))));
    }

    [Fact]
    public void A_test_without_its_own_session_takes_the_class_or_base_class_session()
    {
        Assert.Equal("class-session", global::E2E.SessionAttributes.Restore(typeof(Base), typeof(Base).GetMethod(nameof(Base.Plain))));
        Assert.Equal("class-session", global::E2E.SessionAttributes.Restore(typeof(Derived), typeof(Base).GetMethod(nameof(Base.Plain))));
    }

    [Fact]
    public void A_test_with_no_session_restores_none()
    {
        Assert.Null(global::E2E.SessionAttributes.Restore(typeof(Unmarked), typeof(Unmarked).GetMethod(nameof(Unmarked.Plain))));
    }

    [Fact]
    public void A_session_name_must_not_be_blank()
    {
        Assert.Throws<ArgumentException>(() => new E2ESessionAttribute(" "));
    }

    [Fact]
    public void A_test_that_is_not_a_setup_test_saves_none()
    {
        Assert.Null(global::E2E.SessionAttributes.Saves(typeof(SetupTest).GetMethod(nameof(SetupTest.Body))));
    }
}
