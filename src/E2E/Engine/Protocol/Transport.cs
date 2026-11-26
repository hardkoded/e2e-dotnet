// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace E2E.Engine.Protocol;

/// <summary>What a transport knows when it opens a connection. Upstream's <c>TransportContext</c> (<c>engine/protocol/transport.ts</c>).</summary>
public sealed record TransportContext
{
    /// <summary>The connection's role; see <see cref="InitializeParams.Role"/>.</summary>
    public required string Role { get; init; }

    /// <summary>The environment the harness would start a worker with.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>The project root, where relative paths resolve.</summary>
    public required string Cwd { get; init; }

    /// <summary>
    /// The app the target declares, as <c>prepare</c>, <c>init</c>, and <c>doctor</c> receive it: what a transport that provisions a machine
    /// installs there (a build at <see cref="EngineAppInfo.AppPath"/>, relative to <see cref="Cwd"/>), since a server elsewhere cannot read the
    /// project's files. Null for a connection opened outside those hooks.
    /// </summary>
    public EngineAppInfo? App { get; init; }

    /// <summary>Cancels when opening must stop; it never cancels once <see cref="IEngineTransport.OpenAsync"/> has returned.</summary>
    public CancellationToken Signal { get; init; }
}

/// <summary>One open connection's streams. Upstream's <c>TransportConnection</c>.</summary>
public interface ITransportConnection
{
    /// <summary>The stream the server's messages arrive on.</summary>
    Stream Input { get; }

    /// <summary>The stream the client's messages go out on.</summary>
    Stream Output { get; }

    /// <summary>Releases what the transport holds (a process, a socket), within <paramref name="timeout"/>.</summary>
    Task CloseAsync(TimeSpan timeout);

    /// <summary>What the transport saw go wrong (a server's last stderr lines), for an error message. Upstream's optional <c>diagnostics</c>.</summary>
    string Diagnostics() => "";
}

/// <summary>Opens connections to an engine server. Upstream's <c>EngineTransport</c>.</summary>
public interface IEngineTransport
{
    /// <summary>A short description for error messages: the command, or the address.</summary>
    string Label { get; }

    Task<ITransportConnection> OpenAsync(TransportContext context);
}

/// <summary>Options of <see cref="SpawnTransport"/>. Upstream's <c>SpawnTransportOptions</c>.</summary>
public sealed record SpawnTransportOptions
{
    /// <summary>The server executable.</summary>
    public required string Command { get; init; }

    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Variables added to the worker's environment for the server.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>Working directory; defaults to the project root.</summary>
    public string? Cwd { get; init; }
}

/// <summary>
/// Starts the server as a child process per connection and speaks over its stdin and stdout, the way an editor starts a language server. Its
/// stderr is kept for the message of a failure. Upstream's <c>spawnTransport</c>.
/// </summary>
public sealed class SpawnTransport(SpawnTransportOptions options) : IEngineTransport
{
    /// <summary>Characters of a server's stderr kept for the message of a failure. Upstream's <c>STDERR_TAIL_BYTES</c>.</summary>
    private const int StderrTailBytes = 4096;

    /// <summary>The <c>X_OK</c> mode of <c>access(2)</c>.</summary>
    private const int ExecuteOk = 1;

    /// <summary>
    /// How long the end of a server's stdout waits for the server to exit and its stderr to close, so a failure message can carry both.
    /// Upstream's <c>EXIT_AFTER_STDOUT_MS</c>.
    /// </summary>
    private static readonly TimeSpan ExitAfterStdout = TimeSpan.FromSeconds(2);

    private readonly SpawnTransportOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public string Label => string.Join(' ', [_options.Command, .. _options.Args ?? []]);

    public Task<ITransportConnection> OpenAsync(TransportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cwd = _options.Cwd ?? context.Cwd;
        var start = new ProcessStartInfo(_options.Command)
        {
            WorkingDirectory = cwd,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        foreach (var argument in _options.Args ?? [])
        {
            start.ArgumentList.Add(argument);
        }

        // The environment is exactly the worker's, not the runner's own.
        start.Environment.Clear();
        foreach (var (name, value) in context.Env)
        {
            if (value is not null)
            {
                start.Environment[name] = value;
            }
        }

        foreach (var (name, value) in _options.Env ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        start.FileName = ResolveCommand(_options.Command, cwd, start.Environment);
        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        try
        {
            context.Signal.ThrowIfCancellationRequested();
            process.Start();
            if (context.Signal.IsCancellationRequested)
            {
                // Disposing a process does not stop the child, so a canceled open ends it before it throws.
                _ = TryStop(process);
                context.Signal.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            process.Dispose();
            throw;
        }

        var tail = new StderrTail();
        var stderrClosed = Task.Run(() => tail.ReadAsync(process.StandardError));
        return Task.FromResult<ITransportConnection>(new SpawnConnection(process, tail, stderrClosed, ExitAfterStdout));
    }

    /// <summary>
    /// Ends a child's process tree, for a canceled open: whatever stops the kill (a child that exited, or a tree .NET cannot end whole) must not
    /// replace the cancellation the caller asked for. False when it did not end.
    /// </summary>
    private static bool TryStop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception cause) when (cause is InvalidOperationException or AggregateException or Win32Exception)
        {
            Console.Error.WriteLine(
                $"the server process could not be stopped after the open was canceled: {cause.Message}"
            );
            return false;
        }
    }

    private static string DefaultPath =>
        OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("PATH") ?? "" : "/usr/bin:/bin";

    /// <summary>
    /// A file the system could start by this user, so a search goes on past one it could not, as the kernel does for Node's <c>spawn</c>: on
    /// Unix <c>access(X_OK)</c>, which weighs the owner, group and other execute bits against the current user.
    /// </summary>
    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return OperatingSystem.IsWindows() || Access(path, ExecuteOk) == 0;
    }

    [DllImport("libc", EntryPoint = "access", SetLastError = true)]
    private static extern int Access([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    /// <summary>
    /// The executable to start. .NET looks a command up against the parent's directory and <c>PATH</c>, where upstream's <c>spawn</c> applies the
    /// child's <c>cwd</c> and <c>env.PATH</c>: a relative path resolves against <paramref name="cwd"/>, and a bare name against the child's
    /// <c>PATH</c>. A name found in no directory of an explicit child <c>PATH</c> is a <see cref="FileNotFoundException"/>, as upstream's <c>spawn</c> reports <c>ENOENT</c>.
    /// </summary>
    internal static string ResolveCommand(string command, string cwd, IDictionary<string, string?> environment)
    {
        if (Path.IsPathRooted(command))
        {
            return command;
        }

        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return Path.GetFullPath(command, Path.GetFullPath(cwd));
        }

        // A child with no PATH searches the system's default, as Node's spawn does.
        var path = environment.TryGetValue("PATH", out var value) && value is not null ? value : DefaultPath;
        string[] extensions = OperatingSystem.IsWindows() ? ["", ".exe", ".cmd", ".bat"] : [""];
        // An empty entry is the working directory, and a relative one is relative to it, as in upstream's `spawn`.
        foreach (var entry in path.Split(Path.PathSeparator))
        {
            var directory = Path.GetFullPath(entry, Path.GetFullPath(cwd));
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (IsExecutable(candidate))
                {
                    return candidate;
                }
            }
        }

        // An explicit PATH that has no match is a command that does not exist: the parent's PATH must not launch another one.
        throw new FileNotFoundException(
            $"the server command \"{command}\" was not found in the PATH the server is started with"
        );
    }

    /// <summary>The last <see cref="StderrTailBytes"/> characters a server wrote to stderr.</summary>
    private sealed class StderrTail
    {
        private readonly object _gate = new();
        private string _text = "";

        public override string ToString()
        {
            lock (_gate)
            {
                return _text;
            }
        }

        public async Task ReadAsync(StreamReader reader)
        {
            var buffer = new char[1024];
            try
            {
                int read;
                while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
                {
                    lock (_gate)
                    {
                        var joined = _text + new string(buffer, 0, read);
                        _text = joined.Length > StderrTailBytes ? joined[^StderrTailBytes..] : joined;
                    }
                }
            }
            catch (Exception cause) when (cause is IOException or ObjectDisposedException)
            {
                // The process is gone and its pipe with it: what was read is the tail.
                return;
            }
        }
    }

    private sealed class SpawnConnection : ITransportConnection
    {
        private const long NotExited = long.MinValue;

        private readonly Process _process;
        private readonly StderrTail _stderr;
        private readonly Task _stderrClosed;
        private readonly Task _exited;
        private long _exit = NotExited;

        public SpawnConnection(Process process, StderrTail stderr, Task stderrClosed, TimeSpan exitAfterStdout)
        {
            _process = process;
            _stderr = stderr;
            _stderrClosed = stderrClosed;
            // Closing waits for the exit, not for the pipes: a grandchild that inherited them keeps them open after the server is gone.
            _exited = Task.Run(async () =>
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                Interlocked.Exchange(ref _exit, process.ExitCode);
            });
            Input = new UntilClosedStream(
                process.StandardOutput.BaseStream,
                Task.WhenAll(_exited, stderrClosed),
                exitAfterStdout
            );
            Output = process.StandardInput.BaseStream;
        }

        public Stream Input { get; }

        public Stream Output { get; }

        public string Diagnostics()
        {
            var tail = _stderr.ToString().Trim();
            var exit = Interlocked.Read(ref _exit);
            var status =
                exit == NotExited ? ""
                : SignalOf(exit) is { } signal ? $"killed by {signal}"
                : $"exited with code {exit}";
            return string.Join(
                "; ",
                new[] { status, tail.Length == 0 ? "" : $"stderr:\n{tail}" }.Where(part => part.Length > 0)
            );
        }

        /// <summary>
        /// The signal that stopped a server, from the exit code .NET reports for a signaled child on Unix (128 plus the signal number); null
        /// for a plain exit. Upstream reads <c>child.signalCode</c>, which .NET has no equivalent of.
        /// </summary>
        private static string? SignalOf(long exit) =>
            !OperatingSystem.IsWindows() && exit > 128
                ? (exit - 128) switch
                {
                    1 => "SIGHUP",
                    2 => "SIGINT",
                    3 => "SIGQUIT",
                    6 => "SIGABRT",
                    9 => "SIGKILL",
                    11 => "SIGSEGV",
                    13 => "SIGPIPE",
                    14 => "SIGALRM",
                    15 => "SIGTERM",
                    _ => null,
                }
                : null;

        /// <summary>Closes stdin; false when the server already closed its end.</summary>
        private bool TryCloseOutput()
        {
            try
            {
                Output.Close();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>Kills the server's process tree; false when it exited between the check and the kill.</summary>
        private bool TryKill()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public async Task CloseAsync(TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            try
            {
                _ = TryCloseOutput();

                if (!_process.HasExited)
                {
                    // .NET has no portable SIGTERM: closing stdin is the polite stop, the kill at the budget the forceful one.
                    var budget =
                        timeout == Timeout.InfiniteTimeSpan ? Timeout.InfiniteTimeSpan
                        : timeout < TimeSpan.Zero ? TimeSpan.Zero
                        : timeout;
                    using var timer = new CancellationTokenSource(budget);
                    try
                    {
                        await _process.WaitForExitAsync(timer.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _ = TryKill();
                        await _exited.ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                // The exit status and the last stderr lines are what a failure message reads: take them before the process is disposed.
                if (_process.HasExited)
                {
                    await _exited.ConfigureAwait(false);
                    // One budget for the shutdown and the stderr drain: past it, the tail already read is what a message gets.
                    var left = timeout == Timeout.InfiniteTimeSpan ? ExitAfterStdout : timeout - deadline.Elapsed;
                    var wait = left < ExitAfterStdout ? left : ExitAfterStdout;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.WhenAny(_stderrClosed, Task.Delay(wait)).ConfigureAwait(false);
                    }
                }

                Input.Dispose();
                _process.StandardError.Dispose();
                _process.Dispose();
            }
        }
    }

    /// <summary>
    /// A server's stdout that ends once the server has closed, or <c>ExitAfterStdout</c> after the stdout itself ended. Its stdout can end before
    /// its exit and the rest of its stderr arrive, and the connection reads both for its message when it closes. Upstream's <c>untilClosed</c>.
    /// </summary>
    private sealed class UntilClosedStream(Stream inner, Task closed, TimeSpan wait) : Stream
    {
        private bool _ended;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0 && buffer.Length > 0 && !_ended)
            {
                _ended = true;
                await Task.WhenAny(closed, Task.Delay(wait, cancellationToken)).ConfigureAwait(false);
            }

            return read;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>Options of <see cref="SocketTransport"/>. Upstream's <c>SocketTransportOptions</c>.</summary>
public sealed record SocketTransportOptions(string Host, int Port);

/// <summary>
/// Connects to a server already listening on a TCP port: a server on another machine, or one a launcher forwarded from a device. The server
/// outlives the connection; closing ends the socket only. Upstream's <c>socketTransport</c>.
/// </summary>
public sealed class SocketTransport(SocketTransportOptions options) : IEngineTransport
{
    private readonly SocketTransportOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public string Label => $"{_options.Host}:{_options.Port}";

    public async Task<ITransportConnection> OpenAsync(TransportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, context.Signal).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new SocketConnection(client);
    }

    private sealed class SocketConnection(TcpClient client) : ITransportConnection
    {
        private readonly NetworkStream _stream = client.GetStream();

        public Stream Input => _stream;

        public Stream Output => _stream;

        public Task CloseAsync(TimeSpan timeout)
        {
            client.Dispose();
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Serves an engine in this process over in-memory streams. Every call still crosses the protocol, serialized as on any other transport, so a
/// suite run through it proves an engine and the harness need nothing the protocol cannot carry. Upstream's <c>loopbackTransport</c>.
/// </summary>
public sealed class LoopbackTransport(EngineHandle engine) : IEngineTransport
{
    private readonly EngineHandle _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public string Label => $"loopback {_engine.Name}";

    public Task<ITransportConnection> OpenAsync(TransportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Signal.ThrowIfCancellationRequested();
        var toServer = new Pipe();
        var toClient = new Pipe();
        _ = ServeAsync(toServer, toClient);
        return Task.FromResult<ITransportConnection>(new LoopbackConnection(toServer, toClient));
    }

    /// <summary>Serves the engine, then ends both pipes on the server's side, so a server that closed itself reads as a closed peer.</summary>
    private async Task ServeAsync(Pipe toServer, Pipe toClient)
    {
        try
        {
            await ProtocolServer
                .ServeEngineAsync(_engine, new MessageStreams(toServer.Reader.AsStream(), toClient.Writer.AsStream()))
                .ConfigureAwait(false);
        }
        finally
        {
            await toClient.Writer.CompleteAsync().ConfigureAwait(false);
            await toServer.Reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private sealed class LoopbackConnection(Pipe toServer, Pipe toClient) : ITransportConnection
    {
        public Stream Input { get; } = toClient.Reader.AsStream();

        public Stream Output { get; } = toServer.Writer.AsStream();

        public Task CloseAsync(TimeSpan timeout)
        {
            toServer.Writer.Complete();
            toClient.Reader.Complete();
            return Task.CompletedTask;
        }
    }
}
