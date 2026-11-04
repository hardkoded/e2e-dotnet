// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.Tests.AgentActVerbs;

/// <summary>
/// The gestures fixture page, the flows run on it, a scripted model, and a session,
/// shared by the <c>agent.act</c> grammar verb tests ported from upstream
/// <c>agent-act-verbs.test.ts</c>.
/// </summary>
internal static class GesturesSession
{
    /// <summary>
    /// Upstream's /gestures page: a hover-revealed control, a right-click menu, long-press and
    /// double-tap detection, a drag target, a checkbox, a file input, a memo field whose
    /// selection the status echoes, a radio the pick replaces with its summary, a windowed
    /// ledger, and a footnote far below the fold.
    /// </summary>
    public const string GesturesPage = """
        <!doctype html>
        <html>
        <head><title>Gestures</title></head>
        <body style="margin:0;padding:90px 16px 16px">
          <div id="account" style="position:fixed;left:40px;top:40px;width:200px;height:30px;line-height:30px;background:#eee">Account</div>
          <h1>Gestures</h1>
          <a href="/about">About</a>
          <output id="state" role="status" aria-label="Gesture state">idle</output>
          <button id="redeem" hidden>Redeem</button>

          <span id="file">report.pdf</span>
          <div id="file-menu" role="menu" aria-label="File actions" hidden><button id="rename" role="menuitem">Rename</button></div>

          <button id="hold">Hold me</button>
          <button id="twice">Tap me twice</button>

          <ul aria-label="Todo column"><li id="card" draggable="true">Design review</li></ul>
          <section id="done" aria-label="Done column" style="min-height:40px;border:1px solid #000">Done column</section>

          <label for="agree">Agree to terms</label>
          <input id="agree" type="checkbox" />

          <label for="attachment">Attachment</label>
          <input id="attachment" type="file" multiple />

          <label for="memo">Memo</label>
          <input id="memo" value="release approved" />

          <fieldset id="delivery"><legend>Delivery</legend><label><input type="radio" name="delivery" value="Express" />Express</label></fieldset>

          <p>Page the ledger down to Row 24 and stop there.</p>
          <span>Jump to Row 24</span>
          <div id="ledger" role="list" aria-label="Ledger" style="position:relative;height:200px;overflow:auto;border:1px solid #000"><div id="ledger-spacer"></div></div>
          <output aria-label="Ledger state">golden out of view</output>
          <div style="height:3000px"></div>
          <p id="footnote">Footnote</p>
          <output aria-label="Footnote state">out of view</output>
          <script>
            const state = document.getElementById('state');
            const redeem = document.getElementById('redeem');
            document.getElementById('account').addEventListener('mouseenter', () => { redeem.hidden = false; });
            redeem.addEventListener('click', () => { state.textContent = 'redeemed'; });
            const menu = document.getElementById('file-menu');
            document.getElementById('file').addEventListener('contextmenu', (event) => {
              event.preventDefault();
              menu.hidden = false;
              state.textContent = 'menu open';
            });
            document.getElementById('rename').addEventListener('click', () => { menu.hidden = true; state.textContent = 'renamed'; });
            let heldAt = 0;
            const hold = document.getElementById('hold');
            hold.addEventListener('pointerdown', () => { heldAt = performance.now(); });
            hold.addEventListener('pointerup', () => { state.textContent = performance.now() - heldAt >= 400 ? 'long-pressed' : 'tapped'; });
            document.getElementById('twice').addEventListener('dblclick', () => { state.textContent = 'double-tapped'; });
            const done = document.getElementById('done');
            done.addEventListener('dragover', (event) => event.preventDefault());
            done.addEventListener('drop', (event) => { event.preventDefault(); state.textContent = 'Design review is done'; });
            document.getElementById('agree').addEventListener('change', (event) => { state.textContent = 'agreed: ' + event.target.checked; });
            const delivery = document.getElementById('delivery');
            delivery.addEventListener('change', (event) => {
              delivery.innerHTML = '<p>' + event.target.value + ' delivery selected</p>';
              state.textContent = 'delivery: ' + event.target.value;
            });
            document.getElementById('attachment').addEventListener('change', (event) => {
              state.textContent = 'attached: ' + Array.from(event.target.files, (file) => file.name).join(', ');
            });
            // A windowed list: 400 rows of 40 px exist as data, and only the rows
            // inside the container's scrolled window are in the DOM, so the golden
            // row is nowhere in the tree until the list is paged down to it.
            const ROWS = 400, ROW_PX = 40, GOLDEN = 24;
            const ledger = document.getElementById('ledger');
            document.getElementById('ledger-spacer').style.height = ROWS * ROW_PX + 'px';
            const ledgerState = document.querySelector('output[aria-label="Ledger state"]');
            const renderLedger = () => {
              for (const row of ledger.querySelectorAll('[role="listitem"]')) row.remove();
              const first = Math.floor(ledger.scrollTop / ROW_PX);
              const last = Math.min(ROWS, Math.ceil((ledger.scrollTop + ledger.clientHeight) / ROW_PX));
              for (let index = first; index < last; index += 1) {
                const row = document.createElement('div');
                row.setAttribute('role', 'listitem');
                row.setAttribute('aria-label', 'Ledger row');
                row.style.cssText = 'position:absolute;left:0;right:0;height:' + ROW_PX + 'px;top:' + index * ROW_PX + 'px';
                row.textContent = index === GOLDEN ? 'Row ' + GOLDEN + ' · Golden' : 'Row ' + index;
                ledger.appendChild(row);
              }
              ledgerState.textContent = first <= GOLDEN && GOLDEN < last ? 'golden in view' : 'golden out of view';
            };
            ledger.addEventListener('scroll', renderLedger);
            renderLedger();
            const memo = document.getElementById('memo');
            // A click parks the caret at the end: Home and End scroll the page on macOS, so a flow cannot rely on them.
            memo.addEventListener('click', () => { memo.setSelectionRange(memo.value.length, memo.value.length); });
            memo.addEventListener('select', () => { state.textContent = 'selected: ' + memo.value.slice(memo.selectionStart, memo.selectionEnd); });
            const footnoteState = document.querySelector('output[aria-label="Footnote state"]');
            new IntersectionObserver((entries) => {
              footnoteState.textContent = entries[0].isIntersecting ? 'in view' : 'out of view';
            }).observe(document.getElementById('footnote'));
          </script>
        </body>
        </html>
        """;

    /// <summary>Upstream's /about page.</summary>
    public const string AboutPage = """
        <!doctype html>
        <html>
        <head><title>About page</title></head>
        <body>
          <h1>About</h1>
          <a href="/">Home</a>
        </body>
        </html>
        """;

    /// <summary>Upstream's "uploads a project file" flow: the project's own fixture, named relative to the project root.</summary>
    public static readonly Flow UploadFlow = new(
        "uploads a project file",
        "attach the fixture file",
        (calls, prompt) => calls == 0 ? ModelResponses.Call("upload", new { @ref = RefFor(prompt, "button \"Attachment\""), files = new[] { "fixtures/attachment.txt" } }) : null,
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("attached: attachment.txt"));

    // How the observation lists the gestures page's plain elements.
    private const string AccountLine = "text \"Account\"";
    private const string FileLine = "text \"report.pdf\"";
    private const string CardLine = "listitem \"Design review\"";
    private const string DoneLine = "region \"Done column\"";

    /// <summary>Upstream's "hovers the menu trigger and taps what it reveals" flow: the hover shows the control, which the next turn taps.</summary>
    public static readonly Flow HoverFlow = new(
        "hovers the menu trigger and taps what it reveals",
        "hover the account menu and redeem the voucher",
        (calls, prompt) => calls switch
        {
            0 => ModelResponses.Call("hover", new { @ref = RefFor(prompt, AccountLine) }),
            1 => ModelResponses.Call("tap", new { @ref = RefFor(prompt, "button \"Redeem\"") }),
            _ => null,
        },
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("redeemed"));

    /// <summary>Upstream's "right-clicks a file and renames it through the context menu" flow. It is not in <see cref="ReplayFlows"/>, as upstream's replay pass does not run it either.</summary>
    public static readonly Flow RightClickFlow = new(
        "right-clicks a file and renames it through the context menu",
        "right-click report.pdf and rename it",
        (calls, prompt) => calls switch
        {
            0 => ModelResponses.Call("right_click", new { @ref = RefFor(prompt, FileLine) }),
            1 => ModelResponses.Call("tap", new { @ref = RefFor(prompt, "menuitem \"Rename\"") }),
            _ => null,
        },
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("renamed"));

    /// <summary>Upstream's "double-taps and long-presses" flow. It is not in <see cref="ReplayFlows"/>, as upstream's replay pass does not run it either.</summary>
    public static readonly Flow DoubleTapAndLongPressFlow = new(
        "double-taps and long-presses",
        "double-tap the twice button, then long-press hold me",
        (calls, prompt) => calls switch
        {
            0 => ModelResponses.Call("double_tap", new { @ref = RefFor(prompt, "button \"Tap me twice\"") }),
            1 => ModelResponses.Call("long_press", new { @ref = RefFor(prompt, "button \"Hold me\"") }),
            _ => null,
        },
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("long-pressed"));

    /// <summary>Upstream's "drags the card onto the done column" flow.</summary>
    public static readonly Flow DragFlow = new(
        "drags the card onto the done column",
        "drag the design review card to the done column",
        (calls, prompt) => calls == 0
            ? ModelResponses.Call("drag", new { @ref = RefFor(prompt, CardLine), to = new { @ref = RefFor(prompt, DoneLine) } })
            : null,
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("Design review is done"));

    /// <summary>Upstream's "selects one word with a repeated press and sees the selection" flow: a tap parks the caret, then one press selects the word.</summary>
    public static readonly Flow SelectWordFlow = new(
        "selects one word with a repeated press and sees the selection",
        "select the last word of the memo",
        (calls, prompt) => calls switch
        {
            0 => ModelResponses.Call("tap", new { @ref = RefFor(prompt, "textbox \"Memo\"") }),
            1 => ModelResponses.Call("press", new { @ref = RefFor(prompt, "textbox \"Memo\""), key = "Shift+ArrowLeft", times = 8 }),
            _ => null,
        },
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("selected: approved"));

    /// <summary>
    /// Upstream's "tabs from the terms checkbox to the memo with one repeated press" flow. Tabbing into a text input selects its contents, which
    /// the page's select listener echoes.
    /// </summary>
    public static readonly Flow TabFlow = new(
        "tabs from the terms checkbox to the memo with one repeated press",
        "tab from the terms checkbox to the memo",
        (calls, prompt) => calls == 0 ? ModelResponses.Call("press", new { @ref = RefFor(prompt, "checkbox \"Agree to terms\""), key = "Tab", times = 2 }) : null,
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("selected: release approved"));

    /// <summary>
    /// Upstream's "hovers a bare point in the screenshot" flow. The trigger's center is CSS (140, 55); the screenshot is 768 wide for the 1280-wide
    /// viewport, so the model reads it at (84, 33).
    /// </summary>
    public static readonly Flow HoverAtFlow = new(
        "hovers a bare point in the screenshot",
        "hover the corner to reveal the redeem button",
        (calls, prompt) => calls switch
        {
            0 => ModelResponses.Call("screenshot", new { }),
            1 => ModelResponses.Call("hover_at", new { x = 84, y = 33 }),
            2 => ModelResponses.Call("tap", new { @ref = RefFor(prompt, "button \"Redeem\"") }),
            _ => null,
        },
        screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("redeemed"));

    /// <summary>
    /// The flows of upstream's record-then-replay pass this port can run: the instruction, the
    /// scripted model's tool call for each turn (null concludes the step), and the check after it.
    /// </summary>
    public static readonly IReadOnlyList<Flow> ReplayFlows =
    [
        HoverFlow,
        DragFlow,
        new(
            "checks the box and leaves a checked box alone",
            "agree to the terms, then make sure the box stays checked",
            // The same check twice: the second finds the box already checked and flips nothing.
            (calls, prompt) => calls <= 1 ? ModelResponses.Call("check", new { @ref = RefFor(prompt, "checkbox \"Agree to terms\"") }) : null,
            async screen =>
            {
                await Expect.That(screen.GetByLabel("Agree to terms")).ToBeCheckedAsync();
                await Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("agreed: true");
            }),
        new(
            "checks a radio the pick replaces with its summary",
            "pick Express delivery",
            (calls, prompt) => calls == 0 ? ModelResponses.Call("check", new { @ref = RefFor(prompt, "radio \"Express\"") }) : null,
            screen => Expect.That(screen.GetByRole("status", "Gesture state")).ToHaveTextAsync("delivery: Express")),
        UploadFlow,
        new(
            "scrolls a listed node into view",
            "scroll to the footnote",
            (calls, prompt) => calls == 0 ? ModelResponses.Call("scroll_to", new { @ref = RefFor(prompt, "paragraph \"Footnote\"") }) : null,
            screen => Expect.That(screen.GetByLabel("Footnote state")).ToHaveTextAsync("in view")),
        new(
            "pages a windowed list to a row it has not rendered",
            "scroll the ledger to the golden row",
            (calls, prompt) => calls == 0 ? ModelResponses.Call("scroll_to", new { text = "Row 24", @ref = RefFor(prompt, "list \"Ledger\"") }) : null,
            screen => Expect.That(screen.GetByLabel("Ledger state")).ToHaveTextAsync("golden in view")),
        SelectWordFlow,
        TabFlow,
        new(
            "opens a page and comes back",
            "open the about page and come back",
            (calls, prompt) => calls switch
            {
                0 => ModelResponses.Call("tap", new { @ref = RefFor(prompt, "link \"About\"") }),
                1 => ModelResponses.Call("back", new { }),
                _ => null,
            },
            screen => Expect.That(screen.GetByRole("heading", "Gestures")).ToBeVisibleAsync()),
    ];

    /// <summary>Upstream's "refuses a hidden file" flow: the model names the project's <c>.env</c>, which the policy never lets through. It fails, so it has no check.</summary>
    public static readonly Flow HiddenFileFlow = new(
        "refuses a hidden file",
        "attach the env file",
        (calls, prompt) => calls == 0 ? ModelResponses.Call("upload", new { @ref = RefFor(prompt, "button \"Attachment\""), files = new[] { ".env" } }) : ModelResponses.Done("failed", "the file was refused", "POLICY_DENIED"),
        _ => Task.CompletedTask);

    /// <summary>The project files beside the suite: one uploadable fixture and one hidden file the policy must refuse (upstream's <c>PROJECT_FILES</c>).</summary>
    public static string CreateProject()
    {
        var root = Directory.CreateTempSubdirectory("e2e-verbs-project-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "attachment.txt"), "attached\n");
        File.WriteAllText(Path.Combine(root, ".env"), "SECRET=never-uploaded\n");
        return root;
    }

    /// <summary>Serves the gestures page, and the about page at /about.</summary>
    public static Task<TinySite> StartSiteAsync()
    {
        return TinySite.StartAsync(context => TinySite.RespondAsync(
            context,
            context.Request.Url!.AbsolutePath == "/about" ? AboutPage : GesturesPage));
    }

    /// <summary>The act model for one flow: its script for the turn, then a passing verdict.</summary>
    public static ScriptedModel ModelFor(Flow flow)
    {
        var calls = 0;
        return new ScriptedModel(request =>
        {
            var prompt = string.Join('\n', request.Messages.Select(message => message.Content));
            return flow.Script(calls++, prompt) ?? ModelResponses.Done("passed", "done");
        });
    }

    /// <summary>
    /// Runs one flow as its own test: opens the gestures page, acts on the instruction, and
    /// runs the flow's check. Returns the act's result.
    /// </summary>
    public static async Task<ActResult> RunFlowAsync(TinySite site, Flow flow, ScriptedModel model, string cacheDirectory) =>
        (await RunFlowWithStepsAsync(site, flow, model, cacheDirectory)).Result;

    /// <summary><see cref="RunFlowAsync"/>, with the steps the session recorded: their events are what the upload tests read.</summary>
    public static async Task<(ActResult Result, IReadOnlyList<StepRecord> Steps)> RunFlowWithStepsAsync(TinySite site, Flow flow, ScriptedModel model, string cacheDirectory)
    {
        ActResult? result = null;
        IReadOnlyList<StepRecord> steps = [];
        var session = await StartAsync(site, model, new FileStepCache(cacheDirectory), flow.Title);
        await RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/gestures");
            result = await session.Agent.ActAsync(flow.Instruction);
            await flow.Check(session.Screen);
            steps = session.StepRecords;
        });
        return (result!, steps);
    }

    /// <summary>Runs a flow whose act is expected to fail, and returns the failure with the steps the session recorded.</summary>
    public static async Task<(AgentException Error, IReadOnlyList<StepRecord> Steps)> RunFailingFlowAsync(TinySite site, Flow flow, ScriptedModel model, string cacheDirectory)
    {
        await using var session = await StartAsync(site, model, new FileStepCache(cacheDirectory), flow.Title);
        await session.App.OpenAsync("/gestures");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync(flow.Instruction));
        session.Complete(error);
        return (error, session.StepRecords);
    }

    /// <summary>The ref of the last listed node whose line matches <paramref name="line"/>.</summary>
    public static string RefFor(string prompt, string line)
    {
        var matches = Regex.Matches(prompt, @"^\s*(?:added |changed )?#(e\d+) " + Regex.Escape(line) + @"(?: (?:text|testid|href|placeholder)=""[^""]*"")*(?: value=""[^""]*"")?(?: \[[^\]]*\])*(?: \(was: .*\))?\s*$", RegexOptions.Multiline);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : throw new InvalidOperationException("No line " + line + " in the screen:\n" + prompt);
    }

    public static async Task<E2ESession> StartAsync(TinySite site, ScriptedModel model, FileStepCache cache, string title)
    {
        return await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = model,
            BaseUrl = site.Url,
            ProjectRoot = CreateProject(),
            Cache = cache,
            CacheEnabled = true,
            TestTitle = "verbs > " + title,
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
    }

    public static async Task RunAsync(E2ESession session, Func<Task> body)
    {
        await using (session)
        {
            Exception? error = null;
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            session.Complete(error);
            Assert.Null(error);
        }
    }
}

/// <summary>One agentic flow: what the test asks, how the scripted model answers each turn, and the check that decides it.</summary>
internal sealed record Flow(string Title, string Instruction, Func<int, string, ModelResponse?> Script, Func<Screen, Task> Check);
