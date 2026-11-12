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
/// a page that loads users from <c>/api/users</c>, a pointer pad, a canvas with a drawn chart, a board with hover and drag (unlisted in the nav), the controls page, the browser fixture's own page, a counter for the speed floor, a scroll page of windowed rows (unlisted in the nav), and the
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
        ("/canvas", "Canvas"),
        ("/canvas-flow", "Keypad"),
        ("/canvas-wizard", "Canvas wizard"),
        ("/canvas-form", "Canvas form"),
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
        // size, the media its own @media rules see, the cookies the page sees, and
        // a load counter for reload.
        ["/browser"] = ("Browser", """
            <h1>Browser</h1>
                   <button id="go-about">Go to about, soon</button>
                   <output aria-label="Viewport"></output>
                   <output aria-label="Media"></output>
                   <span id="media-probe"></span>
                   <style>
                     #media-probe { color: rgb(0, 0, 0); transition-property: opacity; transition-duration: 0.3s; }
                     @media (prefers-color-scheme: dark) { #media-probe { color: rgb(255, 255, 255); } }
                     @media (prefers-reduced-motion: reduce) { #media-probe { transition-duration: 0s; } }
                   </style>
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
                     const probe = document.getElementById('media-probe');
                     const media = document.querySelector('output[aria-label="Media"]');
                     const reportMedia = () => {
                       const style = getComputedStyle(probe);
                       media.textContent = (style.color === 'rgb(255, 255, 255)' ? 'dark' : 'light') + ', ' +
                         (style.transitionDuration === '0s' ? 'reduced motion' : 'full motion');
                     };
                     for (const query of ['(prefers-color-scheme: dark)', '(prefers-reduced-motion: reduce)']) {
                       matchMedia(query).addEventListener('change', reportMedia);
                     }
                     reportMedia();
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
                   <div id="diagram-node" style="position: relative; width: 200px; height: 80px; margin-top: 24px;">
                     <svg width="200" height="80" style="position: absolute; inset: 0;">
                       <path d="M1 1H199V79H1Z" fill="white" stroke="black" />
                     </svg>
                     <textarea aria-label="Edit label" readonly
                       style="position: absolute; inset: 0; pointer-events: none; background: transparent;">Node</textarea>
                   </div>
                   <script>
                     const pad = document.getElementById('pad');
                     const node = document.getElementById('diagram-node');
                     const label = node.querySelector('textarea');
                     node.addEventListener('dblclick', () => {
                       label.readOnly = false;
                       label.style.pointerEvents = 'auto';
                       label.focus();
                     });
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

        // A canvas surface with no accessibility semantics whatsoever: the pins and the bar chart exist only as pixels.
        // Nothing here is reachable through the semantic tree, so this page is what the vision judgment is run against.
        ["/canvas"] = ("Canvas map", """
            <h1>Canvas map</h1>
                   <p>Every control below is drawn, not marked up.</p>
                   <canvas id="map" width="480" height="260"></canvas>
                   <output id="picked" role="status" aria-label="Picked">nothing picked</output>
                   <script>
                     const canvas = document.getElementById('map');
                     const context = canvas.getContext('2d');
                     const pins = [
                       { name: 'red', x: 360, y: 70, color: '#d92b2b' },
                       { name: 'blue', x: 110, y: 190, color: '#2b56d9' },
                     ];
                     // An upward-trending bar chart, for judgment assertions about pixels.
                     const bars = [30, 55, 70, 110, 150, 190];
                     context.fillStyle = '#f4f4f5';
                     context.fillRect(0, 0, 480, 260);
                     context.fillStyle = '#9ca3af';
                     bars.forEach((height, index) => {
                       context.fillRect(24 + index * 34, 240 - height, 24, height);
                     });
                     for (const pin of pins) {
                       context.fillStyle = pin.color;
                       context.beginPath();
                       context.arc(pin.x, pin.y, 16, 0, Math.PI * 2);
                       context.fill();
                     }
                     canvas.addEventListener('click', (event) => {
                       const box = canvas.getBoundingClientRect();
                       const x = event.clientX - box.left;
                       const y = event.clientY - box.top;
                       const picked = pins.find(
                         (pin) => Math.hypot(pin.x - x, pin.y - y) <= 20,
                       );
                       document.getElementById('picked').textContent = picked
                         ? 'picked the ' + picked.name + ' pin'
                         : 'missed at ' + Math.round(x) + ',' + Math.round(y);
                     });
                   </script>
            """),

        ["/canvas-flow"] = ("Drawn keypad", """
            <h1>Drawn keypad</h1>
            <p>Everything below is painted on a canvas; nothing is marked up.</p>
            <canvas id="pad" width="420" height="560"></canvas>
            <output id="result" role="status" aria-label="Result">waiting</output>
            <script>
              const canvas = document.getElementById('pad');
              const context = canvas.getContext('2d');
              const keys = [];
              const labels = ['1','2','3','4','5','6','7','8','9','CLR','0','OK'];
              labels.forEach((label, index) => {
                const column = index % 3;
                const row = Math.floor(index / 3);
                keys.push({ label, x: 30 + column * 125, y: 120 + row * 105, w: 105, h: 85 });
              });
              let entered = '';
              function draw() {
                context.fillStyle = '#f4f4f5';
                context.fillRect(0, 0, 420, 560);
                context.fillStyle = '#111827';
                context.fillRect(30, 30, 355, 60);
                context.fillStyle = '#f9fafb';
                context.font = '32px monospace';
                context.textBaseline = 'middle';
                context.textAlign = 'left';
                context.fillText(entered === '' ? 'enter code' : entered, 45, 60);
                for (const key of keys) {
                  context.fillStyle = key.label === 'OK' ? '#15803d' : key.label === 'CLR' ? '#b45309' : '#e5e7eb';
                  context.fillRect(key.x, key.y, key.w, key.h);
                  context.fillStyle = key.label === 'OK' || key.label === 'CLR' ? '#ffffff' : '#111827';
                  context.font = 'bold 30px sans-serif';
                  context.textAlign = 'center';
                  context.fillText(key.label, key.x + key.w / 2, key.y + key.h / 2);
                }
              }
              draw();
              canvas.addEventListener('click', (event) => {
                const box = canvas.getBoundingClientRect();
                const x = event.clientX - box.left;
                const y = event.clientY - box.top;
                const key = keys.find((k) => x >= k.x && x <= k.x + k.w && y >= k.y && y <= k.y + k.h);
                if (!key) return;
                if (key.label === 'CLR') entered = '';
                else if (key.label === 'OK') {
                  document.getElementById('result').textContent = 'code accepted: ' + entered;
                } else if (entered.length < 12) entered += key.label;
                draw();
              });
            </script>
            """),

        ["/canvas-wizard"] = ("Drawn wizard", """
            <h1>Drawn wizard</h1>
            <p>Eight drawn screens; nothing below is marked up.</p>
            <canvas id="wiz" width="520" height="420"></canvas>
            <output id="wizard-result" role="status" aria-label="Wizard result">in progress</output>
            <script>
              const canvas = document.getElementById('wiz');
              const context = canvas.getContext('2d');
              const shapes = [
                { key: 'red-circle', color: '#d92b2b', kind: 'circle', label: 'red circle' },
                { key: 'blue-square', color: '#2b56d9', kind: 'square', label: 'blue square' },
                { key: 'green-triangle', color: '#15803d', kind: 'triangle', label: 'green triangle' },
                { key: 'orange-diamond', color: '#ea580c', kind: 'diamond', label: 'orange diamond' },
              ];
              // Which shape each screen asks for, and where the four shapes sit on it
              // (a rotation per screen, so a position memorised on one screen is wrong on the next).
              const asks = [2, 0, 3, 1, 1, 3, 0, 2];
              const slots = [ { x: 90, y: 200 }, { x: 210, y: 200 }, { x: 330, y: 200 }, { x: 450, y: 200 } ];
              let screen = 0;
              let picked = null;
              const picks = [];
              function order(index) { return shapes.map((_, i) => shapes[(i + index) % 4]); }
              function draw() {
                context.fillStyle = '#f4f4f5';
                context.fillRect(0, 0, 520, 420);
                context.fillStyle = '#111827';
                context.font = 'bold 22px sans-serif';
                context.textAlign = 'left';
                context.textBaseline = 'middle';
                context.fillText('Step ' + (screen + 1) + ' of 8: tap the ' + shapes[asks[screen]].label + ', then Next', 20, 40);
                const arranged = order(screen);
                arranged.forEach((shape, i) => {
                  const { x, y } = slots[i];
                  context.fillStyle = shape.color;
                  context.beginPath();
                  if (shape.kind === 'circle') context.arc(x, y, 36, 0, Math.PI * 2);
                  else if (shape.kind === 'square') context.rect(x - 34, y - 34, 68, 68);
                  else if (shape.kind === 'triangle') { context.moveTo(x, y - 38); context.lineTo(x + 40, y + 32); context.lineTo(x - 40, y + 32); context.closePath(); }
                  else { context.moveTo(x, y - 40); context.lineTo(x + 40, y); context.lineTo(x, y + 40); context.lineTo(x - 40, y); context.closePath(); }
                  context.fill();
                  if (picked === shape.key) { context.lineWidth = 6; context.strokeStyle = '#111827'; context.stroke(); }
                });
                context.fillStyle = picked ? '#15803d' : '#9ca3af';
                context.fillRect(380, 330, 120, 60);
                context.fillStyle = '#ffffff';
                context.font = 'bold 26px sans-serif';
                context.textAlign = 'center';
                context.fillText('Next', 440, 360);
                context.fillStyle = '#374151';
                context.font = '18px sans-serif';
                context.textAlign = 'left';
                context.fillText(picked ? 'Selected: ' + shapes.find((s) => s.key === picked).label : 'Nothing selected', 20, 360);
              }
              draw();
              canvas.addEventListener('click', (event) => {
                if (screen >= 8) return;
                const box = canvas.getBoundingClientRect();
                const x = event.clientX - box.left;
                const y = event.clientY - box.top;
                if (x >= 380 && x <= 500 && y >= 330 && y <= 390) {
                  if (!picked) return;
                  picks.push(picked === shapes[asks[screen]].key ? 'ok' : 'wrong');
                  picked = null;
                  screen += 1;
                  if (screen === 8) {
                    document.getElementById('wizard-result').textContent = 'wizard done: ' + picks.join(',');
                    context.fillStyle = '#f4f4f5'; context.fillRect(0, 0, 520, 420);
                    context.fillStyle = '#111827'; context.font = 'bold 24px sans-serif'; context.textAlign = 'left';
                    context.fillText('All eight steps done.', 20, 40);
                    return;
                  }
                  draw();
                  return;
                }
                const arranged = order(screen);
                const hit = arranged.findIndex((_, i) => Math.hypot(slots[i].x - x, slots[i].y - y) <= 42);
                if (hit >= 0) { picked = arranged[hit].key; draw(); }
              });
            </script>
            """),

        ["/canvas-form"] = ("Drawn form", """
            <h1>Drawn form</h1>
            <p>Two fields and a button, all painted; nothing below is marked up.</p>
            <canvas id="form" width="480" height="300" tabindex="0" style="outline:none"></canvas>
            <output id="form-result" role="status" aria-label="Form result">unsubmitted</output>
            <script>
              const canvas = document.getElementById('form');
              const context = canvas.getContext('2d');
              const fields = [
                { label: 'Name', value: '', x: 40, y: 60, w: 400, h: 44 },
                { label: 'City', value: '', x: 40, y: 140, w: 400, h: 44 },
              ];
              const submit = { x: 40, y: 220, w: 160, h: 48 };
              let active = -1;
              function draw() {
                context.fillStyle = '#f4f4f5';
                context.fillRect(0, 0, 480, 300);
                context.textBaseline = 'middle';
                fields.forEach((field, index) => {
                  context.fillStyle = '#111827';
                  context.font = '16px sans-serif';
                  context.textAlign = 'left';
                  context.fillText(field.label, field.x, field.y - 14);
                  context.fillStyle = '#ffffff';
                  context.fillRect(field.x, field.y, field.w, field.h);
                  context.lineWidth = index === active ? 3 : 1;
                  context.strokeStyle = index === active ? '#2563eb' : '#9ca3af';
                  context.strokeRect(field.x, field.y, field.w, field.h);
                  context.fillStyle = '#111827';
                  context.font = '20px monospace';
                  context.fillText(field.value + (index === active ? '|' : ''), field.x + 10, field.y + field.h / 2);
                });
                context.fillStyle = '#15803d';
                context.fillRect(submit.x, submit.y, submit.w, submit.h);
                context.fillStyle = '#ffffff';
                context.font = 'bold 18px sans-serif';
                context.textAlign = 'center';
                context.fillText('Submit', submit.x + submit.w / 2, submit.y + submit.h / 2);
              }
              function inside(box, x, y) { return x >= box.x && x <= box.x + box.w && y >= box.y && y <= box.y + box.h; }
              function submitForm() {
                document.getElementById('form-result').textContent =
                  'submitted: name=' + fields[0].value + ' city=' + fields[1].value;
              }
              draw();
              canvas.addEventListener('click', (event) => {
                const box = canvas.getBoundingClientRect();
                const x = event.clientX - box.left;
                const y = event.clientY - box.top;
                const index = fields.findIndex((field) => inside(field, x, y));
                if (index >= 0) { active = index; canvas.focus(); }
                else if (inside(submit, x, y)) submitForm();
                draw();
              });
              let selected = false;
              canvas.addEventListener('keydown', (event) => {
                if (active < 0) return;
                event.preventDefault();
                const field = fields[active];
                if (event.key === 'Enter') submitForm();
                else if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'a') selected = field.value !== '';
                else if (event.metaKey || event.ctrlKey || event.altKey) return;
                else if (event.key === 'Backspace' || event.key === 'Delete') {
                  field.value = selected ? '' : field.value.slice(0, -1);
                  selected = false;
                } else if (event.key === 'Tab') { active = (active + 1) % fields.length; selected = false; }
                else if (event.key.length === 1) {
                  field.value = selected ? event.key : field.value + event.key;
                  selected = false;
                }
                draw();
              });
              canvas.addEventListener('blur', () => { active = -1; draw(); });
            </script>
            """),

        ["/board"] = ("Board", """
            <h1>Board</h1>

                   <div id="card-menu">Card actions</div>
                   <button id="archive" hidden>Archive card</button>

                   <ul aria-label="Todo column">
                     <li id="card" draggable="true">Design review</li>
                   </ul>
                   <section id="done" aria-label="Done column">Done column</section>
                   <output aria-label="Board state">Design review is in todo</output>

                   <label for="attachment">Attachment</label>
                   <input id="attachment" type="file" />
                   <output aria-label="Attachment state">none</output>

                   <script>
                     const boardState = document.querySelector('output[aria-label="Board state"]');
                     document.getElementById('card-menu').addEventListener('mouseenter', () => {
                       document.getElementById('archive').hidden = false;
                     });
                     document.getElementById('archive').addEventListener('click', () => {
                       boardState.textContent = 'Design review is archived';
                     });
                     const done = document.getElementById('done');
                     done.addEventListener('dragover', (event) => event.preventDefault());
                     done.addEventListener('drop', (event) => {
                       event.preventDefault();
                       boardState.textContent = 'Design review is done';
                     });
                     document.getElementById('attachment').addEventListener('change', (event) => {
                       document.querySelector('output[aria-label="Attachment state"]').textContent =
                         event.target.files[0] ? event.target.files[0].name : 'none';
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

        // A native confirm, and a link to a file download. Both are unlisted in the nav, so the agent tests see the nav they always did.
        ["/dialogs"] = ("Dialogs", """
            <h1>Dialogs</h1>
                   <button id="confirm">Delete everything</button>
                   <output role="status" aria-label="Decision"></output>
                   <script>
                     document.getElementById('confirm').addEventListener('click', () => {
                       const accepted = confirm('Really delete everything?');
                       document.querySelector('output').textContent = accepted ? 'deleted' : 'kept';
                     });
                   </script>
            """),

        ["/downloads"] = ("Downloads", """
            <h1>Downloads</h1>
                   <a href="/files/report.csv" download>Download report</a>
            """),

        // An iframe with an editor in it, and that editor's own page. Both are unlisted in the nav, as the pages above.
        ["/frames"] = ("Frames", """
            <h1>Frames</h1>
                   <iframe id="editor" src="/frames/child" title="editor"></iframe>
            """),

        ["/frames/child"] = ("Editor frame", """
            <h2>Embedded editor</h2>
                   <label for="note">Note</label>
                   <input id="note" />
                   <button id="save-note">Save note</button>
                   <output role="status" aria-label="Note state">empty</output>
                   <script>
                     document.getElementById('save-note').addEventListener('click', () => {
                       document.querySelector('output').textContent =
                         'saved: ' + document.getElementById('note').value;
                     });
                   </script>
            """),

        // A scroll pane of its own and a long page, each reporting its scroll offset: what scroll_to by text works against. Rows paint only
        // once scrolled into their scroll root, as a windowed list renders. Unlisted in the nav, as the pages above.
        ["/scroll"] = ("Scroll", $$"""
            <h1>Scroll</h1>
                   <output role="status" aria-label="Scroll position">0</output>
                   <output role="status" aria-label="Pane position">0</output>
                   <output role="status" aria-label="Picked row">none</output>
                   <ul aria-label="Pane" style="height: 200px; overflow: auto; padding: 0; list-style: none">
                     {{Slots(40, "Item", 40)}}
                   </ul>
                   <ul aria-label="Rows" style="padding: 0; list-style: none">
                     {{Slots(120, "Row", 60)}}
                   </ul>
                   <script>
                     const picked = document.querySelector('output[aria-label="Picked row"]');
                     document.addEventListener('click', (event) => {
                       if (event.target.tagName === 'BUTTON') picked.textContent = 'picked ' + event.target.textContent;
                     });
                     const position = document.querySelector('output[aria-label="Scroll position"]');
                     addEventListener('scroll', () => {
                       position.textContent = String(Math.round(scrollY));
                     });
                     const pane = document.querySelector('ul[aria-label="Pane"]');
                     const panePosition = document.querySelector('output[aria-label="Pane position"]');
                     pane.addEventListener('scroll', () => {
                       panePosition.textContent = String(Math.round(pane.scrollTop));
                     });
                     const reveal = (list, root) => {
                       const observer = new IntersectionObserver((entries) => {
                         for (const entry of entries) {
                           if (!entry.isIntersecting) continue;
                           const button = document.createElement('button');
                           button.textContent = entry.target.dataset.row;
                           entry.target.replaceChildren(button);
                           observer.unobserve(entry.target);
                         }
                       }, { root });
                       for (const slot of list.children) observer.observe(slot);
                     };
                     reveal(pane, pane);
                     reveal(document.querySelector('ul[aria-label="Rows"]'), null);
                   </script>
            """),
    };

    // `count` fixed-height list slots labelled `label 1..count`; the scroll page's script paints each one's button once it is scrolled into view.
    private static string Slots(int count, string label, int height) =>
        string.Join("\n", Enumerable.Range(0, count).Select(index => $"<li style=\"height: {height}px\" data-row=\"{label} {index + 1}\"></li>"));

    private static readonly Lazy<string> Server = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The base URL of the running playground, started on first use.</summary>
    public static string Url => Server.Value;

    /// <summary>Whether the app also answers on <c>localhost</c>, the second origin of the cross-origin cases. Starts the app when it is not running yet.</summary>
    public static bool CrossOriginAvailable => Url.Length > 0 && s_crossOrigin;

    private static bool s_crossOrigin;

    private static string Start()
    {
        // HttpListener cannot bind port 0: take a free port, and try again when another test takes it first.
        string url;
        HttpListener listener;
        bool crossOrigin;
        for (var attempt = 0; ; attempt++)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Prefixes.Add(url.Replace("127.0.0.1", "localhost", StringComparison.Ordinal));
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                listener.Close();
                if (attempt >= 5)
                {
                    throw;
                }

                continue;
            }

            // The second origin of the cross-origin cases (upstream's CROSS), the same app under another host name. A host that cannot
            // bind it (no mapping, no URL ACL) still serves the app; only the cross-origin tests skip themselves.
            try
            {
                listener.Prefixes.Add("http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/");
                crossOrigin = true;
            }
            catch (Exception ex) when (ex is HttpListenerException or System.Net.Sockets.SocketException or ArgumentException)
            {
                crossOrigin = false;
            }

            break;
        }

        s_crossOrigin = crossOrigin;

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
            else if (context.Request.Url?.AbsolutePath == "/files/report.csv")
            {
                var report = Encoding.UTF8.GetBytes("id,total\n1,42\n");
                response.ContentType = "text/csv";
                response.AddHeader("Content-Disposition", "attachment; filename=\"report.csv\"");
                response.ContentLength64 = report.Length;
                await response.OutputStream.WriteAsync(report).ConfigureAwait(false);
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
                // Its own task: a route that holds its response (upstream's /never) must not stall the other requests.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PlaywrightRoutes.HandleAsync(context).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or IOException or ObjectDisposedException)
                    {
                        // The client went away.
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // A route bug is a 500 the test can see, not an empty 200.
                        try
                        {
                            response.StatusCode = 500;
                        }
                        catch (Exception inner) when (inner is InvalidOperationException or ObjectDisposedException or System.Net.ProtocolViolationException)
                        {
                            // The response was already sent.
                        }
                    }
                    finally
                    {
                        try
                        {
                            response.Close();
                        }
                        catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or IOException or ObjectDisposedException)
                        {
                            // The response was aborted.
                        }
                    }
                });
                continue;
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
        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split('=', 2)))
        {
            form[Uri.UnescapeDataString(pair[0].Replace('+', ' '))] = pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "";
        }

        return form;
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
