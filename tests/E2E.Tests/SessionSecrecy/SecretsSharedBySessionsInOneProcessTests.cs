// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.Loader;
using E2E.Internal;

namespace E2E.Tests.SessionSecrecy;

/// <summary>Runs alone, after the parallel collections: the secrets the sessions of a process share are process-wide, so every test that opens a <c>launch()</c> host or reads what is shared beside a session joins it.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedSecretsCollection
{
    public const string Name = "Shared secrets";
}

/// <summary>
/// The <c>describe('secrets shared by the sessions in one process')</c> of upstream's <c>session-secrecy.test.ts</c>. A session is an
/// <see cref="AttemptScope"/>: its <c>Redactor</c> is upstream's <c>sessionLedger(...).redact</c>, <c>Entries()</c> its <c>entries()</c>, <c>Register</c> its <c>register</c>, and <c>ReleaseSessionLedger</c> its <c>releaseSessionLedger</c>. The other copy of the module is a second
/// load of the assembly in its own <see cref="AssemblyLoadContext"/>. Not ported: 'rebuilds a ledger only when a value appears or disappears' compares upstream's private <c>compiled</c> field; the port
/// checks the same rule through <c>Redactor</c> identity below.
/// </summary>
[Collection(SharedSecretsCollection.Name)]
public sealed class SecretsSharedBySessionsInOneProcessTests : IDisposable
{
    private readonly List<AttemptScope> _opened = [];

    public SecretsSharedBySessionsInOneProcessTests() => ClearShared();

    public void Dispose()
    {
        foreach (var scope in _opened)
        {
            scope.ReleaseSessionLedger();
        }

        ClearShared();
    }

    // The process starts every test with no shared value, as upstream's files each get a worker of their own.
    internal static void ClearShared()
    {
        AppDomain.CurrentDomain.SetData(Brands.SharedSecretsBrand, null);
    }

    /// <summary>
    /// A host open beside the sessions, as in every upstream test of the describe: learned values are visible beside a session only while
    /// one is (see <c>Secrecy.Beside</c>).
    /// </summary>
    internal static Action OpenHost() => Secrecy.ShareStaticSecrets([]);

    private AttemptScope NewSession(params (string Name, string Value)[] known)
    {
        var scope = Scope(known);
        _opened.Add(scope);
        return scope;
    }

    /// <summary>A session that knows <paramref name="known"/> from the start, as upstream's <c>sessionLedger(newSession(), secrets)</c>.</summary>
    internal static AttemptScope Scope(params (string Name, string Value)[] known)
    {
        var scope = new AttemptScope
        {
            SharesSecrets = true,
            Session = null!,
            Agents = new Dictionary<string, ResolvedAgent>(),
            Cache = null,
            CacheEnabled = false,
            TestTitle = "secrecy",
            EnginePlatform = "web",
        };
        foreach (var (name, value) in known)
        {
            scope.Register(name, value);
        }

        return scope;
    }

    [Fact]
    public void Redacts_a_hosts_values_in_every_session_while_they_are_shared_holds_them_in_none_and_stops_once_they_are_withdrawn()
    {
        const string value = "declared-by-a-host-4410";
        var before = NewSession();
        var withdraw = Secrecy.ShareStaticSecrets([Secret.Create("invite", value)]);
        var after = NewSession();
        foreach (var ledger in new[] { before, after })
        {
            Assert.Equal("saw <secret:invite>", ledger.Redactor.Redact($"saw {value}"));
            // What a session holds stays its own: a saved state carries none of the host's values.
            Assert.Empty(ledger.Entries());
            Assert.Empty(Secrecy.CarriedSecrets(ledger, new List<(string Name, string Value)>()));
            // A download of either session may hold the value, so it is rewritten.
            Assert.True(Secrecy.RedactsDownloads(ledger.Redactor));
        }

        Assert.Equal("label <secret:invite>", Secrecy.RedactForSession(null, new List<Secret>(), $"label {value}"));
        withdraw();
        Assert.Equal($"saw {value}", before.Redactor.Redact($"saw {value}"));
        Assert.Equal($"saw {value}", after.Redactor.Redact($"saw {value}"));
        // Text the process emits stays covered: the process ledger keeps every value it met.
        Assert.Equal("log <secret:invite>", global::E2E.OpenSession.Redact($"log {value}"));
    }

    [Fact]
    public void Shares_a_runs_config_values_with_every_session_for_the_life_of_the_process()
    {
        const string value = "declared-by-the-run-9902";
        var before = NewSession();
        global::E2E.OpenSession.RegisterStaticSecrets([Secret.Create("run", value)]);
        Assert.Equal("<secret:run>", before.Redactor.Redact(value));
        Assert.Equal("<secret:run>", NewSession().Redactor.Redact(value));
    }

    [Fact]
    public void Shares_what_a_session_learned_with_the_sessions_beside_it_opened_before_or_after_until_the_session_ends()
    {
        const string value = "learned-mid-session-3317";
        var host = OpenHost();
        var learner = NewSession();
        var before = NewSession();
        Secrecy.AdoptSecrets(learner, [("code", value)]);
        var after = NewSession();
        Assert.Equal("<secret:code>", before.Redactor.Redact(value));
        Assert.Equal("<secret:code>", after.Redactor.Redact(value));
        Assert.Empty(before.Entries());
        learner.ReleaseSessionLedger();
        // A one-time code is fresh on every fill: it leaves with its session, which still redacts it in what is reported of it.
        Assert.Equal(value, before.Redactor.Redact(value));
        Assert.Equal("<secret:code>", learner.Redactor.Redact(value));
        host();
    }

    [Fact]
    public void Rebuilds_a_ledger_only_when_a_value_appears_or_disappears_not_when_one_more_owner_shares_a_value_already_there()
    {
        global::E2E.OpenSession.RegisterStaticSecrets([Secret.Create("token", "shared-by-every-session-4471")]);
        var watching = NewSession();
        _ = watching.Redactor.Redact("warm");
        var built = watching.Redactor;
        // A test's own session opens and ends with the run's config values, as every test does.
        var session = NewSession(("token", "shared-by-every-session-4471"));
        session.ReleaseSessionLedger();
        _ = watching.Redactor.Redact("again");
        Assert.Same(built, watching.Redactor);
        var withdraw = Secrecy.ShareStaticSecrets([Secret.Create("fresh", "a-value-nobody-shared-9920")]);
        Assert.Equal("<secret:fresh>", watching.Redactor.Redact("a-value-nobody-shared-9920"));
        Assert.NotSame(built, watching.Redactor);
        withdraw();
    }

    [Fact]
    public void Reaches_the_sessions_of_another_copy_of_the_module_as_a_project_with_two_installs_loads()
    {
        var mine = NewSession();
        var other = new CopyLoadContext();
        try
        {
            var assembly = other.LoadFromAssemblyPath(typeof(Secrecy).Assembly.Location);
            Assert.NotSame(typeof(Secrecy).Assembly, assembly);
            var copySecrecy = assembly.GetType("E2E.Secrecy")!;
            var copyOpenSession = assembly.GetType("E2E.OpenSession")!;
            var copySecret = assembly.GetType("E2E.Secret")!;
            Assert.NotSame(typeof(Secrecy), copySecrecy);

            const string fromCopy = "declared-in-the-other-copy-6120";
            var secret = copySecret
                .GetMethod("Create", [typeof(string), typeof(string), typeof(string)])!
                .Invoke(null, ["other", fromCopy, null]);
            var shareStatic = copySecrecy.GetMethod("ShareStaticSecrets", BindingFlags.Static | BindingFlags.Public)!;
            var secrets = Array.CreateInstance(copySecret, 1);
            secrets.SetValue(secret, 0);
            var withdraw = (Action)shareStatic.Invoke(null, [secrets])!;
            Assert.Equal("<secret:other>", mine.Redactor.Redact(fromCopy));
            Assert.Equal("<secret:other>", global::E2E.OpenSession.Redact(fromCopy));
            withdraw();
            Assert.Equal(fromCopy, mine.Redactor.Redact(fromCopy));

            // A session opened through the other copy, whose own redactor reads what is shared.
            var copyScope = assembly.GetType("E2E.AttemptScope")!;
            var theirs = Activator.CreateInstance(copyScope)!;
            copyScope.GetProperty("SharesSecrets")!.SetValue(theirs, true);
            const string fromHere = "declared-in-this-copy-7245";
            global::E2E.OpenSession.RegisterStaticSecrets([Secret.Create("here", fromHere)]);
            var copyRedact = copyOpenSession.GetMethod(
                "Redact",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            )!;
            Assert.Equal("<secret:here>", (string)copyRedact.Invoke(null, [fromHere])!);
            var theirRedactor = copyScope.GetProperty("Redactor")!.GetValue(theirs)!;
            Assert.Equal(
                "<secret:here>",
                (string)theirRedactor.GetType().GetMethod("Redact")!.Invoke(theirRedactor, [fromHere])!
            );
            // Each copy keeps its own ledgers: the other copy's session is ended through that copy.
            copyScope.GetMethod("ReleaseSessionLedger")!.Invoke(theirs, []);
        }
        finally
        {
            other.Unload();
        }
    }

    /// <summary>A load context that holds a second copy of the assembly under test; the framework and the other dependencies stay shared.</summary>
    private sealed class CopyLoadContext() : AssemblyLoadContext("second-install", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
