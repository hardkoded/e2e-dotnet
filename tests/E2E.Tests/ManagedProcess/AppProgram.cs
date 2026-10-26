// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace E2E.Tests.ManagedProcess;

/// <summary>
/// The helpers of upstream's <c>tests/helpers/free-port.ts</c> and the <c>node -e</c> scripts of its managed-process tests: the program the
/// tests start (<c>E2E.Tests.App</c>, beside the tests), a free port, and in-process servers that stand in for a dev server.
/// </summary>
internal static class AppProgram
{
    /// <summary>The executable of <c>E2E.Tests.App</c>, which stands where upstream's tests use <c>process.execPath</c>.</summary>
    public static string Executable { get; } = Path.Combine(AppContext.BaseDirectory, "E2E.Tests.App" + (OperatingSystem.IsWindows() ? ".exe" : ""));

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static CommandConfig Command(string[] args, TimeSpan? startupTimeout = null, TimeSpan? shutdownTimeout = null, string? log = null, bool? reuseExisting = null, string? cwd = null,
        IReadOnlyDictionary<string, string>? env = null, IReadOnlyList<string>? inheritEnv = null) => new()
        {
            Executable = Executable,
            Args = args,
            StartupTimeout = startupTimeout,
            ShutdownTimeout = shutdownTimeout,
            Log = log,
            ReuseExisting = reuseExisting,
            Cwd = cwd,
            Env = env,
            // The apphost finds the runtime through DOTNET_ROOT where .NET is not in a default place (a dotnet-install directory).
            InheritEnv = [.. inheritEnv ?? [], "DOTNET_ROOT"],
        };

    public static string[] Serve(int port, string? extra = null) => extra is null ? ["serve", port.ToString(System.Globalization.CultureInfo.InvariantCulture)] : ["serve", port.ToString(System.Globalization.CultureInfo.InvariantCulture), extra];

    /// <summary>A port nothing listens on at the moment.</summary>
    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static async Task<bool> IsReachable(string url)
    {
        try
        {
            using var response = await Client.GetAsync(url);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Resolves once the file holds <paramref name="text"/>.</summary>
    public static async Task Logged(string file, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                if ((await reader.ReadToEndAsync()).Contains(text, StringComparison.Ordinal))
                {
                    return;
                }
            }
            catch (IOException)
            {
                // not created yet
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("the log never held " + text);
            }

            await Task.Delay(20);
        }
    }
}

/// <summary>
/// An in-process HTTP server on a free loopback port. <c>respond</c> gets the number of the request (from 1) and answers with a status code,
/// or with null to leave the request pending until the server is disposed.
/// </summary>
internal sealed class TestServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentBag<TcpClient> _clients = [];
    private readonly Func<int, Task<int?>> _respond;
    private readonly Task _accepting;
    private int _requests;

    public TestServer(Func<int, Task<int?>> respond)
    {
        _respond = respond;
        _listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
        _accepting = Task.Run(AcceptAsync);
    }

    public string Url { get; }

    public static TestServer Answering() => new(_ => Task.FromResult<int?>(200));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _accepting.WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { }, TaskScheduler.Default);
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _clients.Add(client);
                _ = Task.Run(() => ServeAsync(client));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // stopped
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var seen = new StringBuilder();
            while (!seen.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, _stop.Token);
                if (read == 0)
                {
                    return;
                }

                seen.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            var status = await _respond(Interlocked.Increment(ref _requests));
            if (status is { } code)
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + " X\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"), _stop.Token);
            }
            else
            {
                await Task.Delay(Timeout.Infinite, _stop.Token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException or SocketException)
        {
            // the client or the server went away
        }
    }
}
