// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebBenchmark.Components;

namespace WebBenchmark;

/// <summary>
/// The benchmark app's host: a Blazor Web App that serves the home page and one component per scenario at
/// <c>/e/&lt;slug&gt;</c>. It replaces upstream's Next.js project under <c>apps/web-benchmark/app</c>
/// (.NET-only: the port hosts the same pages from ASP.NET Core).
/// </summary>
public static class BenchmarkApp
{
    /// <summary>Upstream's <c>next start app --port 4280</c>.</summary>
    public const string DefaultUrl = "http://127.0.0.1:4280";

    /// <summary>
    /// Builds the app, not yet started. A test host passes <c>--urls http://127.0.0.1:0</c> and reads the port it got from
    /// <see cref="WebApplication.Urls"/> once the app has started.
    /// </summary>
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            // The static web assets manifests are named for the app, not for the entry assembly: a test host is the entry assembly of its own run.
            ApplicationName = typeof(BenchmarkApp).Assembly.GetName().Name,
        });
        // blazor.web.js comes from the framework through the manifest; outside Development the manifest is read only when asked for.
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        // The app has no wwwroot: its one asset is blazor.web.js.
        builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Error);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddScoped<Portal>();

        var app = builder.Build();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
