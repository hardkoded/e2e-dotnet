// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.Tests.CheckActions;

/// <summary>
/// One control acted on through a target kind, for the tests of upstream's <c>describe.each(KINDS)</c> blocks in
/// <c>check-actions.test.ts</c>. .NET-only: upstream's <c>targetOf</c> and <c>perform</c> take a CSS selector; the port names a control by
/// its test id, since a screen locator and an observed node both find one that way.
/// </summary>
public sealed class CoveredControl
{
    /// <summary>A refusal waits out the whole deadline, so the cases that expect one take a short one (upstream's <c>REFUSAL_TIMEOUT_MS</c>, 500 ms). .NET-only: 1.5 s, since Playwright's call log reaches the line that names the blocker only after about that long.</summary>
    public static readonly TimeSpan RefusalTimeout = TimeSpan.FromMilliseconds(1500);

    public const string ElementPlusCss = """
        .el-radio-button{outline:none;display:inline-block;position:relative}
        .el-radio-button__original-radio{opacity:0;z-index:-1;outline:none;position:absolute}
        .el-radio-button__inner{white-space:nowrap;vertical-align:middle;background:#fff;border:1px solid #dcdfe6;line-height:1;cursor:pointer;display:inline-block;position:relative;padding:8px 15px}
        .el-checkbox{cursor:pointer;display:inline-flex;align-items:center;position:relative}
        .el-checkbox__input{white-space:nowrap;cursor:pointer;outline:none;display:inline-flex;position:relative}
        .el-checkbox__original{opacity:0;z-index:-1;outline:none;width:0;height:0;margin:0;position:absolute}
        .el-checkbox__inner{display:inline-block;position:relative;border:1px solid #dcdfe6;width:14px;height:14px;background:#fff;z-index:1}
        .el-radio{cursor:pointer;display:inline-flex;align-items:center;position:relative}
        .el-radio__input{white-space:nowrap;cursor:pointer;outline:none;display:inline-flex;position:relative}
        .el-radio__original{opacity:0;z-index:-1;outline:none;margin:0;position:absolute;top:0;bottom:0;left:0;right:0}
        .el-radio__inner{border:1px solid #dcdfe6;border-radius:100%;width:14px;height:14px;background:#fff;position:relative;display:inline-block}
        .modal{position:fixed;inset:0;background:rgba(0,0,0,.4);z-index:10}
        """;

    public const string ElementPlusControls = """
        <label class="el-radio-button"><input class="el-radio-button__original-radio" type="radio" name="mode" id="install" data-testid="install"><span class="el-radio-button__inner">安装</span></label>
        <label class="el-radio-button"><input class="el-radio-button__original-radio" type="radio" name="mode" id="remove" data-testid="remove"><span class="el-radio-button__inner">卸载</span></label>
        <label class="el-checkbox"><span class="el-checkbox__input"><input class="el-checkbox__original" type="checkbox" id="agree" data-testid="agree"><span class="el-checkbox__inner"></span></span><span class="el-checkbox__label">Agree</span></label>
        <label class="el-radio"><span class="el-radio__input"><input class="el-radio__original" type="radio" name="size" id="large" data-testid="large"><span class="el-radio__inner"></span></span><span class="el-radio__label">Large</span></label>
        """;

    public static readonly string ElementPlusPage = "<style>" + ElementPlusCss + "</style>" + ElementPlusControls;

    /// <summary>Upstream's <c>ELEMENT_PLUS_CASES</c>: the control's name and its test id.</summary>
    public static readonly (string Name, string Id)[] ElementPlusCases = [("radio-button", "install"), ("checkbox", "agree"), ("radio", "large")];

    private readonly Func<string, string, CancellationToken, Func<Task>?, Task<(string Code, string Message)?>> _perform;
    private readonly Func<string, Task<bool>> _isChecked;
    private readonly Func<string, Task<string>> _evaluate;

    private CoveredControl(
        Func<string, string, CancellationToken, Func<Task>?, Task<(string Code, string Message)?>> perform,
        Func<string, Task<bool>> isChecked,
        Func<string, Task<string>> evaluate)
    {
        _perform = perform;
        _isChecked = isChecked;
        _evaluate = evaluate;
    }

    /// <summary>Performs <paramref name="verb"/> (<c>check</c>, <c>uncheck</c>, or <c>tap</c>) on the control and returns the failure it ended with, as the engine reports it, or null.</summary>
    public Task<(string Code, string Message)?> PerformAsync(string id, string verb, CancellationToken cancellationToken = default, Func<Task>? hold = null) => _perform(id, verb, cancellationToken, hold);

    /// <summary>Whether the input with <paramref name="id"/> is checked in the page, or in the shadow root of <c>#host</c>.</summary>
    public Task<bool> IsCheckedAsync(string id) => _isChecked(id);

    /// <summary>The value of a page function body, as text.</summary>
    public Task<string> EvaluateAsync(string expression) => _evaluate(expression);

    /// <summary>Runs <paramref name="test"/> on <paramref name="body"/> through the element an observation names, or through a screen locator.</summary>
    public static Task RunAsync(bool locator, string body, Func<CoveredControl, Task> test, TimeSpan? actionTimeout = null) =>
        locator ? RunThroughLocatorAsync(body, test, actionTimeout) : RunThroughElementAsync(body, test, actionTimeout);

    private static async Task RunThroughElementAsync(string body, Func<CoveredControl, Task> test, TimeSpan? actionTimeout)
    {
        await CheckPage.RunAsync(
            body,
            async session =>
            {
                async Task<(string Code, string Message)?> Perform(string id, string verb, CancellationToken token, Func<Task>? hold)
                {
                    var observation = await session.ObserveAsync(CancellationToken.None);
                    var node = CheckPage.Flatten(observation.Roots).First(candidate => candidate.TestId == id);
                    LocatorAction action = verb switch { "check" => new LocatorAction.Check(), "uncheck" => new LocatorAction.Uncheck(), _ => new LocatorAction.Tap() };
                    if (hold is not null)
                    {
                        await hold();
                    }

                    try
                    {
                        await session.PerformAsync(node, action, token);
                        return null;
                    }
                    catch (EngineException error)
                    {
                        return (error.Code, error.Message);
                    }
                }

                await test(new CoveredControl(
                    Perform,
                    async id => await CheckPage.EvaluateAsync<bool>(session, $"() => document.getElementById('{id}')?.checked ?? document.getElementById('host').shadowRoot.getElementById('{id}').checked"),
                    async expression => await CheckPage.EvaluateAsync<string>(session, expression)));
            },
            actionTimeout);
    }

    private static async Task RunThroughLocatorAsync(string body, Func<CoveredControl, Task> test, TimeSpan? actionTimeout)
    {
        await CheckPage.RunWithScreenAsync(
            body,
            async session =>
            {
                async Task<(string Code, string Message)?> Perform(string id, string verb, CancellationToken token, Func<Task>? hold)
                {
                    var control = session.Screen.GetByTestId(id);
                    if (hold is not null)
                    {
                        await hold();
                    }

                    try
                    {
                        await (verb switch
                        {
                            "check" => control.CheckAsync(cancellationToken: token),
                            "uncheck" => control.UncheckAsync(cancellationToken: token),
                            _ => control.TapAsync(cancellationToken: token),
                        });
                        return null;
                    }
                    catch (TestException failure)
                    {
                        return failure.InnerException is EngineException error ? (error.Code, error.Message) : (failure.Code, failure.Message);
                    }
                }

                await test(new CoveredControl(
                    Perform,
                    async id => await session.Browser.EvaluateAsync<bool>($"() => document.getElementById('{id}')?.checked ?? document.getElementById('host').shadowRoot.getElementById('{id}').checked"),
                    async expression => await session.Browser.EvaluateAsync<string>(expression) ?? ""));
            },
            actionTimeout);
    }
}
