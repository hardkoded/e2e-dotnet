// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E.NUnit;

namespace E2E.NUnit.Tests.Testbed;

/// <summary>
/// Opens the control inventory, the port of upstream's web-benchmark example, before each
/// test, at the path upstream's benchmark serves it from.
/// </summary>
public abstract class ControlInventoryTest : E2ETest
{
    protected override string? BaseUrl => ControlInventorySite.Url.Value;

    [SetUp]
    public Task OpenControlInventoryAsync() => App.OpenAsync("/e/control-inventory");
}

/// <summary>Serves <c>control-inventory.html</c> on a loopback port for every path, for the whole test run.</summary>
internal static class ControlInventorySite
{
    public static readonly Lazy<string> Url = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string Start()
    {
        var page = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Testbed", "control-inventory.html"));
        // HttpListener cannot bind port 0: take a free port, and try again when another test takes it first.
        string url;
        HttpListener listener;
        for (var attempt = 0; ; attempt++)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            url = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            try
            {
                listener.Start();
                break;
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
        }

        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = page.Length;
                await context.Response.OutputStream.WriteAsync(page);
                context.Response.Close();
            }
        });
        return url;
    }
}
