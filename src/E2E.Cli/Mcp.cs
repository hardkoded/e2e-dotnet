// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.Cli.Mcp;
using E2E.Internal;

namespace E2E.Cli;

/// <summary>The flags of <c>e2e mcp</c>.</summary>
internal sealed class McpCommandOptions
{
    public string? Config { get; init; }

    public string? Target { get; init; }

    public bool Headed { get; init; }

    public int MaxSessions { get; init; } = SessionHost.SessionBounds.Default;
}

/// <summary>
/// The <c>e2e mcp</c> command: claims stdout for the protocol, then serves. Over stdio, stdout is the JSON-RPC
/// stream, so anything else that writes there would corrupt the handshake. The transport keeps the process's
/// own stdout stream, and everything else that reaches <see cref="Console.Out"/> or <see cref="Console.Error"/>
/// goes to stderr, redacted, for the whole process: user code runs here and may print a secret.
/// </summary>
internal static class McpCommand
{
    /// <summary>Runs the server until the client disconnects or a signal arrives; returns the exit code.</summary>
    public static async Task<int> RunAsync(McpCommandOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var protocol = Console.OpenStandardOutput();
        var stdin = Console.OpenStandardInput();
        var stderr = Console.Error;
        var ledger = new SecretLedger();
        var output = new McpOutput(text => stderr.Write(text), ledger);
        Console.SetOut(new McpOutputWriter(output, OutputStream.Stdout));
        Console.SetError(new McpOutputWriter(output, OutputStream.Stderr));

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });

        // An error nobody caught (an unobserved task of user code) is logged redacted, and held like user output
        // while a config loads, and the server shuts down as on a signal.
        var crashed = false;
        void OnUncaught(object? cause)
        {
            crashed = true;
            ReportUncaught(output, cause);
            stop.Cancel();
        }

        // A task nobody awaited (a cancelled engine call) is logged, redacted; it does not end the server.
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            args.SetObserved();
            ReportUncaught(output, args.Exception);
        }

        void OnUnhandled(object sender, UnhandledExceptionEventArgs args) => OnUncaught(args.ExceptionObject);

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        try
        {
            var code = await Server.ServeAsync(
                new ServeOptions
                {
                    Cwd = Environment.CurrentDirectory,
                    ConfigPath = options.Config,
                    Target = options.Target,
                    Headed = options.Headed,
                    MaxSessions = options.MaxSessions,
                    Version = ModelHttp.PackageVersion(),
                    Stdin = stdin,
                    Stdout = protocol,
                    Log = line => output.Log("e2e mcp: " + line),
                    Output = output,
                    Secrets = ledger,
                },
                stop.Token).ConfigureAwait(false);
            return crashed ? 1 : code;
        }
#pragma warning disable CA1031 // An uncaught error is reported on stderr and ends the server with exit code 1.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            output.Report("e2e mcp: [error] uncaught: " + Tools.CodedMessage(ex));
            return 1;
        }
        finally
        {
            output.End();
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
        }
    }

    /// <summary>Logs an error nobody caught, redacted and held like what user code prints. Upstream's <c>onUncaught</c>.</summary>
    internal static void ReportUncaught(McpOutput output, object? cause)
    {
        ArgumentNullException.ThrowIfNull(output);
        var text = cause is AggregateException aggregate ? aggregate.Flatten().ToString() : cause?.ToString();
        output.Report("e2e mcp: [error] uncaught: " + text);
    }
}
