// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Text;

namespace E2E.Playground;

/// <summary>
/// The playground pages the testbed tests run against, served on a loopback port for the whole
/// test run. A port of upstream's <c>apps/testbed</c> with only the pages these tests use:
/// the landing page, a plans page of copy with inline links and emphasis (unlisted in the nav), a todo list kept in localStorage, a checkout with one planted bug, a profile
/// form, a sign-in form that sets a session cookie for the dashboard behind it, the workspace wizard, the swatches page that the screenshot tests compare,
/// a page that loads users from <c>/api/users</c>, a pointer pad, the controls page, the browser fixture's own page, a counter for the speed floor, and the
/// about page navigation lands on. E2E.NUnit.Tests links this file for its browser fixture tests.
/// </summary>
internal static class Testbed
{
    private static readonly (string Path, string Label)[] Nav =
    [
        ("/", "Home"),
        ("/todos", "Todos"),
        ("/checkout", "Checkout"),
        ("/forms", "Forms"),
        ("/login", "Login"),
        ("/dashboard", "Dashboard"),
        ("/wizard", "Wizard"),
        ("/network", "Network"),
        ("/pointer", "Pointer"),
        ("/controls", "Controls"),
        ("/browser", "Browser"),
        ("/speed", "Speed"),
        ("/about", "About"),
    ];

    private static readonly Dictionary<string, (string Title, string Body)> Pages = new(StringComparer.Ordinal)
    {
        ["/"] = ("Playground", """
            <h1>Playground</h1>
                   <p>A tiny app exercised by the e2e dogfood suite.</p>
            """),

        // Copy with inline links and emphasis, unlisted in the nav.
        ["/plans"] = ("Plans", """
            <h1>Plans</h1>
                   <p>We ship every week. Read the <a href="/release-notes">release notes</a> for what changed this week.</p>
                   <p>Plans start at <strong>$12</strong> per seat, billed <em>annually</em>, and include the <code>e2e</code> CLI.</p>
            """),

        ["/todos"] = ("Todos", """
            <h1>Todos</h1>
                   <label for="new-todo">New todo</label>
                   <input id="new-todo" placeholder="What needs doing?" />
                   <button id="add">Add</button>

                   <div role="tablist" aria-label="Filter">
                     <button role="tab" aria-selected="true" data-filter="all">All</button>
                     <button role="tab" aria-selected="false" data-filter="open">Open</button>
                     <button role="tab" aria-selected="false" data-filter="done">Done</button>
                   </div>

                   <ul id="list" data-testid="todo-list"></ul>
                   <output role="status" aria-label="Remaining"></output>

                   <script>
                     const load = () => JSON.parse(localStorage.getItem('todos') ?? '[]');
                     const store = (todos) => localStorage.setItem('todos', JSON.stringify(todos));
                     let filter = 'all';

                     function render() {
                       const todos = load();
                       const list = document.getElementById('list');
                       list.innerHTML = '';
                       for (const [index, todo] of todos.entries()) {
                         if (filter === 'open' && todo.done) continue;
                         if (filter === 'done' && !todo.done) continue;
                         const item = document.createElement('li');
                         item.className = todo.done ? 'done' : '';
                         item.setAttribute('data-testid', 'todo');
                         const box = document.createElement('input');
                         box.type = 'checkbox';
                         box.id = 'todo-' + index;
                         box.checked = todo.done;
                         box.addEventListener('change', () => {
                           const next = load();
                           next[index].done = box.checked;
                           store(next);
                           render();
                         });
                         const label = document.createElement('label');
                         label.htmlFor = box.id;
                         label.textContent = todo.title;
                         const remove = document.createElement('button');
                         remove.textContent = 'Delete ' + todo.title;
                         remove.addEventListener('click', () => {
                           const next = load();
                           next.splice(index, 1);
                           store(next);
                           render();
                         });
                         item.append(box, label, remove);
                         list.append(item);
                       }
                       const open = load().filter((todo) => !todo.done).length;
                       document.querySelector('output').textContent = open + ' remaining';
                     }

                     function add() {
                       const input = document.getElementById('new-todo');
                       const title = input.value.trim();
                       if (title === '') return;
                       store([...load(), { title, done: false }]);
                       input.value = '';
                       render();
                     }

                     document.getElementById('add').addEventListener('click', add);
                     document.getElementById('new-todo').addEventListener('keydown', (event) => {
                       if (event.key === 'Enter') add();
                     });
                     for (const tab of document.querySelectorAll('[role=tab]')) {
                       tab.addEventListener('click', () => {
                         for (const other of document.querySelectorAll('[role=tab]')) {
                           other.setAttribute('aria-selected', String(other === tab));
                         }
                         filter = tab.dataset.filter;
                         render();
                       });
                     }
                     render();
                   </script>
            """),

        // Planted bug: the pay button keeps the total from page load, so after a
        // quantity change the summary and the button show different totals.
        ["/checkout"] = ("Checkout", """
            <h1>Checkout</h1>
                   <table aria-label="Cart">
                     <thead><tr><th>Item</th><th>Price</th><th>Quantity</th><th>Line total</th></tr></thead>
                     <tbody>
                       <tr>
                         <td>Notebook</td><td>$12.00</td>
                         <td><input id="notebook-qty" type="number" min="1" value="1" aria-label="Notebook quantity" data-price="12" /></td>
                         <td data-line>$12.00</td>
                       </tr>
                       <tr>
                         <td>Pen</td><td>$3.00</td>
                         <td><input id="pen-qty" type="number" min="1" value="2" aria-label="Pen quantity" data-price="3" /></td>
                         <td data-line>$6.00</td>
                       </tr>
                     </tbody>
                   </table>
                   <section aria-label="Order summary">
                     <h2>Order summary</h2>
                     <p>Order total: <strong id="summary-total">$18.00</strong></p>
                   </section>
                   <button id="pay">Pay $18.00</button>

                   <script>
                     const money = (amount) => '$' + amount.toFixed(2);
                     for (const input of document.querySelectorAll('input[data-price]')) {
                       input.addEventListener('input', () => {
                         let total = 0;
                         for (const row of document.querySelectorAll('tbody tr')) {
                           const qty = row.querySelector('input');
                           const line = Number(qty.dataset.price) * Number(qty.value);
                           row.querySelector('[data-line]').textContent = money(line);
                           total += line;
                         }
                         document.getElementById('summary-total').textContent = money(total);
                       });
                     }
                   </script>
            """),

        ["/network"] = ("Network", """
            <h1>Network</h1>
                   <button id="load">Load users</button>
                   <ul id="users"></ul>
                   <output role="status" aria-label="Network state">idle</output>

                   <script>
                     document.getElementById('load').addEventListener('click', async () => {
                       const state = document.querySelector('output');
                       state.textContent = 'loading';
                       try {
                         const response = await fetch('/api/users');
                         if (!response.ok) throw new Error('HTTP ' + response.status);
                         const users = await response.json();
                         const list = document.getElementById('users');
                         list.innerHTML = '';
                         for (const user of users) {
                           const item = document.createElement('li');
                           item.textContent = user.name;
                           list.append(item);
                         }
                         state.textContent = 'loaded ' + users.length;
                       } catch {
                         state.textContent = 'failed';
                       }
                     });
                   </script>
            """),

        ["/forms"] = ("Forms", """
            <h1>Forms</h1>
                   <form id="profile">
                     <label for="name">Full name</label>
                     <input id="name" placeholder="Ada Lovelace" autocomplete="username" />

                     <label for="bio">Bio</label>
                     <textarea id="bio" placeholder="Tell us about yourself"></textarea>

                     <label for="city">City</label>
                     <input id="city" autocomplete="off" />
                     <ul id="city-suggestions" aria-label="City suggestions"></ul>

                     <label for="team">Team</label>
                     <select id="team">
                       <option value="platform">Platform</option>
                       <option value="web">Web</option>
                       <option value="mobile">Mobile</option>
                     </select>

                     <fieldset>
                       <legend>Notifications</legend>
                       <label for="email-notifications">Email notifications</label>
                       <input id="email-notifications" type="checkbox" />
                       <label for="digest">Weekly digest</label>
                       <input id="digest" type="checkbox" checked />
                     </fieldset>

                     <button type="submit">Save profile</button>
                   </form>
                   <output role="status" aria-label="Save result"></output>
                   <input aria-label="Prefilled field" value="prefilled-value" readonly />

                   <script>
                     document.getElementById('profile').addEventListener('submit', (event) => {
                       event.preventDefault();
                       const name = document.getElementById('name').value.trim();
                       document.querySelector('output').textContent =
                         name === '' ? 'Name is required' : 'Saved profile for ' + name;
                     });
                     // Search-as-you-type: suggestions render on keyup, so a value set
                     // without key events leaves the list empty.
                     const cities = ['Warsaw', 'Wroclaw', 'Gdansk', 'Krakow'];
                     const city = document.getElementById('city');
                     city.addEventListener('keyup', () => {
                       const typed = city.value.toLowerCase();
                       const list = document.getElementById('city-suggestions');
                       list.replaceChildren();
                       if (typed === '') return;
                       for (const name of cities.filter((candidate) => candidate.toLowerCase().startsWith(typed))) {
                         const item = document.createElement('li');
                         item.textContent = name;
                         list.appendChild(item);
                       }
                     });
                   </script>
            """),

        // The browser fixture's own surface: navigation with a delay, the viewport
        // size, the cookies the page sees, and a load counter for reload.
        ["/browser"] = ("Browser", """
            <h1>Browser</h1>
                   <button id="go-about">Go to about, soon</button>
                   <output aria-label="Viewport"></output>
                   <output aria-label="Cookies"></output>
                   <output aria-label="Loads"></output>
                   <output aria-label="Random"></output>
                   <script>
                     document.getElementById('go-about').addEventListener('click', () => {
                       setTimeout(() => {
                         location.assign('/about');
                       }, 400);
                     });
                     const viewport = document.querySelector('output[aria-label="Viewport"]');
                     const report = () => {
                       viewport.textContent = innerWidth + 'x' + innerHeight;
                     };
                     addEventListener('resize', report);
                     report();
                     document.querySelector('output[aria-label="Cookies"]').textContent = document.cookie || 'no cookies';
                     const loads = Number(sessionStorage.getItem('loads') ?? '0') + 1;
                     sessionStorage.setItem('loads', String(loads));
                     document.querySelector('output[aria-label="Loads"]').textContent = 'loads: ' + loads;
                     document.querySelector('output[aria-label="Random"]').textContent = 'random: ' + (Math.random() === 0.5 ? 'seeded' : 'unseeded');
                   </script>
            """),

        // Solid blocks with no text, so a screenshot of one renders the same on every operating system:
        // what toHaveScreenshot is tested against, with one stored file per system. The noise block takes
        // a new color on every load and is only ever compared masked; the far block sits below the fold;
        // the framed block is measured through the iframe.
        ["/swatches"] = ("Swatches", """
            <h1>Swatches</h1>
                   <div data-testid="palette" style="display:flex;gap:8px;padding:8px;width:max-content;background:#ffffff">
                     <div style="width:40px;height:40px;background:#d92b2b"></div>
                     <div style="width:40px;height:40px;background:#2b56d9"></div>
                     <div data-testid="noise" style="width:40px;height:40px"></div>
                   </div>
                   <iframe id="swatch-frame" src="/swatches/frame" title="swatch frame" style="width:120px;height:80px;border:0"></iframe>
                   <div data-testid="far" style="margin-top:1600px;width:64px;height:24px;background:#16a34a"></div>
                   <script>
                     document.querySelector('[data-testid="noise"]').style.background =
                       '#' + Math.floor(Math.random() * 0xffffff).toString(16).padStart(6, '0');
                   </script>
            """),

        ["/swatches/frame"] = ("Swatch frame", """
            <div data-testid="framed" style="position:fixed;left:10px;top:10px;width:30px;height:20px;background:#9333ea"></div>
            """),

        ["/wizard"] = ("Wizard", """
            <h1>Workspace wizard</h1>
                   <section id="step-1">
                     <h2>Step 1: Name</h2>
                     <label for="workspace">Workspace name</label>
                     <input id="workspace" />
                     <button data-next="2">Next</button>
                   </section>
                   <section id="step-2" hidden>
                     <h2>Step 2: Plan</h2>
                     <label for="plan">Plan</label>
                     <select id="plan">
                       <option>Free</option>
                       <option>Pro</option>
                     </select>
                     <button data-next="3">Next</button>
                   </section>
                   <section id="step-3" hidden>
                     <h2>Step 3: Confirm</h2>
                     <button id="create">Create workspace</button>
                     <output role="status" aria-label="Summary"></output>
                   </section>

                   <script>
                     for (const button of document.querySelectorAll('[data-next]')) {
                       button.addEventListener('click', () => {
                         for (const section of document.querySelectorAll('section')) section.hidden = true;
                         document.getElementById('step-' + button.dataset.next).hidden = false;
                       });
                     }
                     document.getElementById('create').addEventListener('click', () => {
                       const name = document.getElementById('workspace').value;
                       const plan = document.getElementById('plan').value;
                       document.querySelector('output').textContent =
                         'Created "' + name + '" on the ' + plan + ' plan';
                     });
                   </script>
            """),

        ["/pointer"] = ("Pointer", """
            <h1>Pointer</h1>
                   <div id="pad" role="img" aria-label="Pointer pad" style="width: 320px; height: 200px; background: #e5e7eb; touch-action: none;"></div>
                   <output aria-label="Pad state">untouched</output>
                   <div aria-label="Hidden pad" hidden>never shown</div>
                   <script>
                     const pad = document.getElementById('pad');
                     const padState = document.querySelector('output[aria-label="Pad state"]');
                     const local = (event) => {
                       const box = pad.getBoundingClientRect();
                       return Math.round(event.clientX - box.left) + ',' + Math.round(event.clientY - box.top);
                     };
                     let downAt = null;
                     let swiped = false;
                     pad.addEventListener('pointerdown', (event) => {
                       downAt = local(event);
                       swiped = false;
                     });
                     pad.addEventListener('pointerup', (event) => {
                       const upAt = local(event);
                       if (downAt !== null && upAt !== downAt) {
                         swiped = true;
                         padState.textContent = 'swiped from ' + downAt + ' to ' + upAt;
                       }
                       downAt = null;
                     });
                     pad.addEventListener('click', (event) => {
                       if (!swiped) padState.textContent = 'tapped at ' + local(event);
                     });
                   </script>
            """),

        ["/controls"] = ("Controls", """
            <h1>Controls</h1>

                   <ul aria-label="Files">
                     <li id="file">report.pdf</li>
                   </ul>
                   <menu id="file-menu" role="menu" aria-label="File actions" hidden>
                     <li><button role="menuitem" id="rename">Rename</button></li>
                     <li><button role="menuitem" id="trash">Move to trash</button></li>
                   </menu>
                   <output role="status" aria-label="File state">untouched</output>

                   <button id="hold">Hold me</button>
                   <button id="twice">Tap me twice</button>
                   <output role="status" aria-label="Gesture state">none</output>

                   <button id="details-toggle" aria-expanded="false" aria-controls="details">Details</button>
                   <p id="details" hidden>The fine print.</p>

                   <button id="prepare">Prepare</button>
                   <button id="publish" disabled>Publish</button>

                   <fieldset>
                     <legend>Size</legend>
                     <label><input type="radio" name="size" value="s" /> Small</label>
                     <label><input type="radio" name="size" value="m" /> Medium</label>
                     <label><input type="radio" name="size" value="l" /> Large</label>
                   </fieldset>

                   <label for="agree">Agree to terms</label>
                   <input id="agree" type="checkbox" />

                   <label for="newsletter">Subscribe to newsletter</label>
                   <input id="newsletter" type="checkbox" />
                   <button id="dark-mode" role="switch" aria-checked="false">Dark mode</button>

                   <label for="color">Color</label>
                   <select id="color" size="3">
                     <option value="red">Red</option>
                     <option value="green" selected>Green</option>
                     <option value="blue">Blue</option>
                   </select>

                   <label for="coupon">Coupon</label>
                   <input id="coupon" value="SAVE10" />

                   <label for="first">First</label>
                   <input id="first" />
                   <label for="second">Second</label>
                   <input id="second" />

                   <label for="keys">Key log input</label>
                   <input id="keys" />
                   <output aria-label="Key log"></output>

                   <a id="docs" href="/about" data-kind="external" aria-label="Documentation" class="link primary">Docs</a>

                   <span data-testid="banner" hidden>Sale</span>
                   <span data-testid="banner">Sale</span>

                   <ul aria-label="Tickets">
                     <li>Ticket A <span class="badge">urgent</span> <button>Close A</button></li>
                     <li>Ticket B <button>Close B</button></li>
                     <li>Ticket C <span class="badge">urgent</span></li>
                   </ul>

                   <label for="attachments">Attachments</label>
                   <input id="attachments" type="file" multiple />
                   <output aria-label="Attachments state">none</output>

                   <div style="height: 3000px"></div>
                   <p id="footnote">Footnote</p>
                   <output aria-label="Footnote state">out of view</output>

                   <script>
                     const fileState = document.querySelector('output[aria-label="File state"]');
                     const menu = document.getElementById('file-menu');
                     document.getElementById('file').addEventListener('contextmenu', (event) => {
                       event.preventDefault();
                       menu.hidden = false;
                       fileState.textContent = 'menu open';
                     });
                     document.getElementById('rename').addEventListener('click', () => {
                       menu.hidden = true;
                       fileState.textContent = 'renamed';
                     });
                     document.getElementById('trash').addEventListener('click', () => {
                       menu.hidden = true;
                       fileState.textContent = 'trashed';
                     });

                     const gesture = document.querySelector('output[aria-label="Gesture state"]');
                     let heldAt = 0;
                     const hold = document.getElementById('hold');
                     hold.addEventListener('pointerdown', () => {
                       heldAt = performance.now();
                     });
                     hold.addEventListener('pointerup', () => {
                       const held = Math.round(performance.now() - heldAt);
                       gesture.textContent = held >= 500 ? 'long-pressed' : 'tapped';
                     });
                     document.getElementById('twice').addEventListener('dblclick', () => {
                       gesture.textContent = 'double-tapped';
                     });

                     const toggle = document.getElementById('details-toggle');
                     toggle.addEventListener('click', () => {
                       const expanded = toggle.getAttribute('aria-expanded') === 'true';
                       toggle.setAttribute('aria-expanded', String(!expanded));
                       document.getElementById('details').hidden = expanded;
                     });

                     // Controlled toggles that commit their state after a save round
                     // trip, as a React checkbox or a headless switch does: the click
                     // leaves the control as it was, and the new state lands later.
                     document.getElementById('newsletter').addEventListener('click', (event) => {
                       const box = event.target;
                       const next = box.checked;
                       event.preventDefault();
                       setTimeout(() => {
                         box.checked = next;
                       }, 400);
                     });
                     const darkMode = document.getElementById('dark-mode');
                     darkMode.addEventListener('click', () => {
                       const next = darkMode.getAttribute('aria-checked') !== 'true';
                       setTimeout(() => {
                         darkMode.setAttribute('aria-checked', String(next));
                       }, 400);
                     });

                     document.getElementById('prepare').addEventListener('click', () => {
                       setTimeout(() => {
                         document.getElementById('publish').disabled = false;
                       }, 1000);
                     });

                     const keyLog = document.querySelector('output[aria-label="Key log"]');
                     document.getElementById('keys').addEventListener('keydown', (event) => {
                       const parts = [];
                       if (event.ctrlKey) parts.push('Control');
                       if (event.altKey) parts.push('Alt');
                       if (event.shiftKey) parts.push('Shift');
                       if (event.metaKey) parts.push('Meta');
                       parts.push(event.key);
                       keyLog.textContent = parts.join('+');
                     });

                     document.getElementById('attachments').addEventListener('change', (event) => {
                       document.querySelector('output[aria-label="Attachments state"]').textContent =
                         Array.from(event.target.files, (file) => file.name).join(', ') || 'none';
                     });

                     const footnoteState = document.querySelector('output[aria-label="Footnote state"]');
                     new IntersectionObserver((entries) => {
                       footnoteState.textContent = entries[0].isIntersecting ? 'in view' : 'out of view';
                     }).observe(document.getElementById('footnote'));
                   </script>
            """),

        ["/speed"] = ("Speed", """
            <h1>Speed</h1>
                   <button id="increment">Increment</button>
                   <output role="status" aria-label="Count">0</output>
                   <label for="echo">Echo</label>
                   <input id="echo" />
                   <output role="status" aria-label="Echoed"></output>
                   <script>
                     let count = 0;
                     document.getElementById('increment').addEventListener('click', () => {
                       count += 1;
                       document.querySelector('output[aria-label="Count"]').textContent = String(count);
                     });
                     document.getElementById('echo').addEventListener('input', (event) => {
                       document.querySelector('output[aria-label="Echoed"]').textContent = event.target.value;
                     });
                   </script>
            """),

        ["/about"] = ("About page", """
            <h1>About</h1>
                   <p>The playground, described.</p>
            """),
    };

    private static readonly Lazy<string> Server = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The base URL of the running playground, started on first use.</summary>
    public static string Url => Server.Value;

    private static string Start()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        _ = Task.Run(() => ListenAsync(listener));
        return url.TrimEnd('/');
    }

    private static async Task ListenAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            var response = context.Response;
            if (context.Request.Url?.AbsolutePath == "/api/users")
            {
                var users = Encoding.UTF8.GetBytes("""[{"name":"Ada"},{"name":"Grace"},{"name":"Margaret"}]""");
                response.ContentType = "application/json";
                response.ContentLength64 = users.Length;
                await response.OutputStream.WriteAsync(users).ConfigureAwait(false);
            }
            else if (context.Request.Url?.AbsolutePath == "/login" && context.Request.HttpMethod == "POST")
            {
                var form = await ReadFormAsync(context.Request).ConfigureAwait(false);
                if (form.GetValueOrDefault("username") == "admin" && form.GetValueOrDefault("password") == "admin-pass")
                {
                    response.AddHeader("Set-Cookie", "session=admin; Path=/; HttpOnly");
                    Redirect(response, "/dashboard");
                }
                else
                {
                    await WriteHtmlAsync(response, "Sign in", LoginBody(failed: true)).ConfigureAwait(false);
                }
            }
            else if (context.Request.Url?.AbsolutePath == "/logout")
            {
                response.AddHeader("Set-Cookie", "session=; Path=/; Max-Age=0");
                Redirect(response, "/login");
            }
            else if (context.Request.Url?.AbsolutePath == "/dashboard")
            {
                if (context.Request.Cookies["session"]?.Value == "admin")
                {
                    await WriteHtmlAsync(response, "Dashboard", """
                        <h1>Dashboard</h1>
                               <p role="status" aria-label="Greeting">Welcome back, admin!</p>
                               <a href="/logout">Sign out</a>
                        """).ConfigureAwait(false);
                }
                else
                {
                    Redirect(response, "/login");
                }
            }
            else if (context.Request.Url?.AbsolutePath == "/login")
            {
                await WriteHtmlAsync(response, "Sign in", LoginBody(failed: false)).ConfigureAwait(false);
            }
            else if (Pages.TryGetValue(context.Request.Url?.AbsolutePath ?? "", out var page))
            {
                await WriteHtmlAsync(response, page.Title, page.Body).ConfigureAwait(false);
            }
            else
            {
                response.StatusCode = 404;
            }

            response.Close();
        }
    }

    private static async Task WriteHtmlAsync(HttpListenerResponse response, string title, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(Layout(title, body));
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static void Redirect(HttpListenerResponse response, string location)
    {
        response.StatusCode = 303;
        response.RedirectLocation = location;
    }

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "",
                StringComparer.Ordinal);
    }

    private static string LoginBody(bool failed) => $"""
        <h1>Sign in</h1>
        {(failed ? "<p role=\"alert\">Invalid credentials</p>" : "")}
        <form method="post" action="/login">
          <label for="username">Username</label>
          <input id="username" name="username" autocomplete="username" />
          <label for="password">Password</label>
          <input id="password" name="password" type="password" autocomplete="current-password" />
          <button type="submit">Sign in</button>
        </form>
        """;

    // The document shell every playground page shares: the stylesheet and the nav.
    private static string Layout(string title, string body)
    {
        var nav = string.Join("\n    ", Nav.Select(item => $"<a href=\"{item.Path}\">{item.Label}</a>"));
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>{{title}}</title>
              <style>
                body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 640px; }
                nav a { margin-right: 0.75rem; }
                li { margin: 0.25rem 0; }
                .done label { text-decoration: line-through; }
                [hidden] { display: none !important; }
              </style>
            </head>
            <body>
              <nav aria-label="Main">
                {{nav}}
              </nav>
              {{body}}
            </body>
            </html>
            """;
    }
}
