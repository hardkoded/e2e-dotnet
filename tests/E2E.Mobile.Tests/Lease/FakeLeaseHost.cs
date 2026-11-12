// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile.Lease;

namespace E2E.Mobile.Tests.Lease;

/// <summary>A stand-in for the daemon, its heartbeat, and the lease directory. Stands for the mocks of <c>daemon.ts</c> and <c>heartbeat.ts</c> in upstream's <c>lease.test.ts</c>.</summary>
internal sealed class FakeLeaseHost : ILeaseHost, IDisposable
{
    private int _leases;

    public List<(DriverRequest Request, IReadOnlyDictionary<string, string?> Env, string Label)> Starts { get; } = [];

    public List<(string StateDirectory, IReadOnlyDictionary<string, string?> Env, string? LeaseId, string Label, CancellationToken Token)> Stops { get; } = [];

    public List<(JsonObject Scope, Action<string> Log)> Heartbeats { get; } = [];

    public int HeartbeatsStopped { get; private set; }

    public int Deletes { get; private set; }

    /// <summary>Answers the next start with this instead, once.</summary>
    public Queue<Func<DriverRequest, Task<DriverGrant>>> NextStarts { get; } = [];

    public Queue<Func<string?, int, Task>> NextStops { get; } = [];

    public Func<string?, int, Task>? Stop { get; set; }

    public Queue<Exception> NextDeleteFailures { get; } = [];

    public Task<DriverGrant> StartDriverAsync(DriverRequest request, IReadOnlyDictionary<string, string?> environment, string label, CancellationToken cancellationToken)
    {
        Starts.Add((request, environment, label));
        if (NextStarts.TryDequeue(out var next))
        {
            return next(request);
        }

        return Task.FromResult(new DriverGrant($"lease-{++_leases}", request.Allocate["tenant"]!.GetValue<string>(), request.Allocate["runId"]!.GetValue<string>(), true));
    }

    public Task StopDriverAsync(string stateDirectory, IReadOnlyDictionary<string, string?> environment, string? leaseId, string label, CancellationToken cancellationToken)
    {
        Stops.Add((stateDirectory, environment, leaseId, label, cancellationToken));
        if (NextStops.TryDequeue(out var next))
        {
            return next(leaseId, Stops.Count);
        }

        return Stop?.Invoke(leaseId, Stops.Count) ?? Task.CompletedTask;
    }

    public Task<Func<Task>> StartHeartbeatAsync(JsonObject scope, Action<string> log, CancellationToken cancellationToken)
    {
        Heartbeats.Add((scope, log));
        return Task.FromResult<Func<Task>>(() =>
        {
            HeartbeatsStopped++;
            return Task.CompletedTask;
        });
    }

    public void DeleteDirectory(string directory)
    {
        Deletes++;
        if (NextDeleteFailures.TryDequeue(out var failure))
        {
            throw failure;
        }

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>The lease directory of the start at <paramref name="index"/>: the parent of its state directory.</summary>
    public string DirectoryOf(int index = 0) => Path.GetDirectoryName(Starts[index].Request.StateDirectory)!;

    public void Dispose()
    {
        foreach (var start in Starts)
        {
            var directory = Path.GetDirectoryName(start.Request.StateDirectory)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
