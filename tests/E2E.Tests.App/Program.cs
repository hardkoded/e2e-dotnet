// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

// The program the app-command tests start, standing in for the `node -e` scripts of upstream's managed-process tests.
//   serve <port> [ignore-sigterm]  answers every request with 200 on 127.0.0.1 until it is stopped
//   serve-for <port> <ms>          serves for that long, then exits
//   spawn <port>                   starts `serve <port>` as a child, then exits at once
//   serve-logged <port>            appends `app` to startup.log in the working directory, then serves like `serve`
//   serve-graceful <port> <file>   serves; on SIGTERM it writes `finished` to the file 50 ms later and exits 0
//   serve-hold <port>              serves; on SIGTERM it writes `stopping` to the working directory, then exits 0 once `release-stop` exists there
//   serve-receipt <port> <file> <dir>  serves; on SIGTERM it writes {"projectExists":<whether dir exists>} to the file 100 ms later and exits 0
//   wrap <pidFile> <mode> <arg>... starts `<mode> <arg>...` as a child, writes its pid to the file, and exits 0 on SIGTERM, staying up until then
//   idle                           stays up and answers nothing
//   exit <code>                    exits with the code
//   chatter <code>                 prints a line to stdout and one to stderr, then exits with the code
//   leak <token>                   prints the token to stderr three ways (whole, split across two writes, no newline), then "printed" to stdout
//   print <code|stay> <line>...    prints each line to stdout, then exits with the code or stays up
//   print-template <template>      prints the template with ${NAME} replaced by that environment variable, then stays up
//   write-env <file>               writes the environment, one NAME=VALUE per line, to the file, then exits
if (args.Length == 0)
{
    return 2;
}

switch (args[0])
{
    case "serve":
        return await Serve(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args.Length > 2 && args[2] == "ignore-sigterm");
    case "serve-for":
        var port = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
        var stop = Task.Delay(int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
        await Task.WhenAny(Serve(port, false), stop);
        return 0;
    case "spawn":
        var child = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        child.ArgumentList.Add("serve");
        child.ArgumentList.Add(args[1]);
        System.Diagnostics.Process.Start(child);
        return 0;
    case "serve-logged":
        File.AppendAllText("startup.log", "app\n");
        return await Serve(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), false);
    case "serve-graceful":
        return await ServeGraceful(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2]);
    case "serve-hold":
        return await ServeHold(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture));
    case "serve-receipt":
        return await ServeReceipt(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2], args[3]);
    case "wrap":
        return await Wrap(args[1], args.Skip(2).ToArray());
    case "idle":
        await Task.Delay(Timeout.Infinite);
        return 0;
    case "exit":
        return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    case "leak":
        // The tool of the mcp-user-output port: the token on stderr whole, split across two writes, and with no newline, then "printed" on stdout.
        Console.Error.WriteLine("tool stderr: " + args[1]);
        Console.Error.Write("tool stderr split: " + args[1][..9]);
        Console.Error.Flush();
        Console.Error.WriteLine(args[1][9..]);
        Console.Error.Write("tool stderr no newline: " + args[1]);
        Console.Out.WriteLine("printed");
        return 0;
    case "chatter":
        Console.Out.WriteLine("to stdout");
        Console.Error.WriteLine("to stderr");
        return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    case "print":
        foreach (var line in args.Skip(2))
        {
            Console.Out.WriteLine(line);
        }

        Console.Out.Flush();
        if (args[1] == "stay")
        {
            await Task.Delay(Timeout.Infinite);
        }

        return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    case "print-template":
        var text = args[1];
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            text = text.Replace("${" + entry.Key + "}", (string?)entry.Value, StringComparison.Ordinal);
        }

        Console.Out.WriteLine(text);
        Console.Out.Flush();
        await Task.Delay(Timeout.Infinite);
        return 0;
    case "write-env":
        var lines = new List<string>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            lines.Add(entry.Key + "=" + entry.Value);
        }

        File.WriteAllLines(args[1], lines);
        return 0;
    default:
        return 2;
}

static async Task<int> Serve(int port, bool ignoreSigterm)
{
    // Ignoring SIGTERM proves the runner escalates to a kill.
    using var registration = ignoreSigterm
        ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => context.Cancel = true)
        : null;
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var seen = new StringBuilder();
                while (!seen.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0)
                    {
                        return;
                    }

                    seen.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"));
            }
        });
    }
}

static async Task<int> ServeGraceful(int port, string finished)
{
    using var registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        Thread.Sleep(50);
        File.WriteAllText(finished, "finished");
        Environment.Exit(0);
    });
    return await Serve(port, false);
}

static async Task<int> ServeHold(int port)
{
    using var registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        File.WriteAllText("stopping", "started");
        // A test that died before it released the stop must not leave this process holding its port.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists("release-stop") && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Environment.Exit(0);
    });
    return await Serve(port, false);
}

static async Task<int> ServeReceipt(int port, string receipt, string project)
{
    using var registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        Thread.Sleep(100);
        File.WriteAllText(receipt, "{\"projectExists\":" + (Directory.Exists(project) ? "true" : "false") + "}");
        Environment.Exit(0);
    });
    return await Serve(port, false);
}

static async Task<int> Wrap(string pidFile, string[] childArgs)
{
    var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var arg in childArgs)
    {
        info.ArgumentList.Add(arg);
    }

    var child = System.Diagnostics.Process.Start(info)!;
    File.WriteAllText(pidFile, child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    using var registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        Environment.Exit(0);
    });
    await Task.Delay(Timeout.Infinite);
    return 0;
}
