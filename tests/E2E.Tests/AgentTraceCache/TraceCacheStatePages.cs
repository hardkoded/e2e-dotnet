// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// The pages of the end-state and typed-value trace cache tests, from upstream's fixture app: <c>/storage</c> (a marker saved in
/// local storage), <c>/row-fields</c> (two fields named only by the row text beside them), and <c>/todos</c> with its <c>/api/todos</c>,
/// a list the server keeps for the life of the app, the way an app with a database shows the last run's data on the next run's first
/// screen. Adding a todo shows a status the page load owns, so every run has an effect to anchor on even when the list already held it.
/// </summary>
internal sealed class TraceCacheStatePages : IDisposable
{
    private const string Storage = """
        <!doctype html>
        <html>
        <head><title>Storage</title></head>
        <body>
          <h1>Storage</h1>
          <button onclick="localStorage.setItem('marker', 'saved'); document.cookie = 'fixture=cookie-value; path=/'; render()">Save marker</button>
          <output id="marker" aria-label="Marker"></output>
          <script>
            function render() {
              document.getElementById('marker').textContent = localStorage.getItem('marker') ?? 'empty';
            }
            render();
          </script>
        </body>
        </html>
        """;

    // Two fields named by nothing: the label sits in a sibling cell, so each is an unnamed textbox told from the other only by its
    // position. The row text beside them is a plain word that a model may well type into them, and the status echoes what was typed.
    private const string RowFields = """
        <!doctype html>
        <html>
        <head><title>Row fields</title></head>
        <body>
          <output id="filled" role="status" aria-label="Filled">nothing yet</output>
          <table>
            <tr><td>Row one</td><td><input type="text" oninput="render()" /></td></tr>
            <tr><td>Row two</td><td><input type="text" oninput="render()" /></td></tr>
          </table>
          <script>
            function render() {
              const values = [...document.querySelectorAll('input')].map((input) => input.value);
              document.getElementById('filled').textContent = values.every((value) => value !== '') ? values.join('+') : 'nothing yet';
            }
          </script>
        </body>
        </html>
        """;

    private readonly TinySite _site;
    private readonly List<string> _todos = [];

    private TraceCacheStatePages(TinySite site) => _site = site;

    public string Url => _site.Url;

    public static async Task<TraceCacheStatePages> StartAsync()
    {
        TraceCacheStatePages? pages = null;
        var site = await TinySite.StartAsync(context => context.Request.Url!.AbsolutePath switch
        {
            "/storage" => TinySite.RespondAsync(context, Storage),
            "/row-fields" => TinySite.RespondAsync(context, RowFields),
            "/todos" => TinySite.RespondAsync(context, pages!.RenderTodos()),
            "/api/todos" when context.Request.HttpMethod == "POST" => pages!.AddTodoAsync(context),
            _ => NotFoundAsync(context),
        });
        pages = new TraceCacheStatePages(site);
        return pages;
    }

    /// <summary>Keeps <paramref name="todo"/> as a POST to <c>/api/todos</c> does, so a later page load lists it.</summary>
    public async Task SeedTodoAsync(string todo)
    {
        using var client = new HttpClient();
        using var response = await client.PostAsync(new Uri(new Uri(Url), "/api/todos"), new StringContent(todo, Encoding.UTF8));
        response.EnsureSuccessStatusCode();
    }

    private string RenderTodos()
    {
        string items;
        lock (_todos)
        {
            items = string.Join("\n", _todos.Select(todo => $"    <li>{HttpUtility.HtmlEncode(todo)}</li>"));
        }

        return $$"""
            <!doctype html>
            <html>
            <head><title>Todos</title></head>
            <body>
              <h1>Todos</h1>
              <label for="todo">New todo</label>
              <input id="todo" />
              <button id="add">Add</button>
              <output id="result" role="status" aria-label="Result"></output>
              <ul id="todos" aria-label="Todos">
            {{items}}
              </ul>
              <script>
                document.getElementById('add').addEventListener('click', async () => {
                  const response = await fetch('/api/todos', { method: 'POST', body: document.getElementById('todo').value });
                  const todos = await response.json();
                  document.getElementById('todos').replaceChildren(
                    ...todos.map((todo) => Object.assign(document.createElement('li'), { textContent: todo })),
                  );
                  document.getElementById('result').textContent = 'added';
                });
              </script>
            </body>
            </html>
            """;
    }

    private async Task AddTodoAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var todo = (await reader.ReadToEndAsync()).Trim();
        string json;
        lock (_todos)
        {
            if (todo != "" && !_todos.Contains(todo))
            {
                _todos.Add(todo);
            }

            json = JsonSerializer.Serialize(_todos);
        }

        var body = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private static Task NotFoundAsync(HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        context.Response.Close();
        return Task.CompletedTask;
    }

    public void Dispose() => _site.Dispose();
}
