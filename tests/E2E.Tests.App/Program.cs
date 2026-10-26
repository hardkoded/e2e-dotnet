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
//   idle                           stays up and answers nothing
//   exit <code>                    exits with the code
//   chatter <code>                 prints a line to stdout and one to stderr, then exits with the code
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
    case "idle":
        await Task.Delay(Timeout.Infinite);
        return 0;
    case "exit":
        return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
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
