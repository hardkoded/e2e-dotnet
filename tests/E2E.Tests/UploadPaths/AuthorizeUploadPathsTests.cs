// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E.Tests.UploadPaths;

/// <summary>
/// The upload policy, ported from upstream <c>upload-paths.test.ts</c>: a model-named path reaches the engine only when it is a regular
/// file inside the project root and not hidden. Everything else fails closed, with the code that says whether the path was refused or
/// merely wrong, and every refusal and the final allow is recorded on the step.
/// </summary>
public sealed class AuthorizeUploadPathsTests : IDisposable
{
    private readonly string _baseDirectory = Directory.CreateTempSubdirectory("e2e-upload-").FullName;
    private readonly string _root;
    private readonly string _outside;
    private readonly bool _linked;

    public AuthorizeUploadPathsTests()
    {
        _root = Path.Combine(_baseDirectory, "project");
        _outside = Path.Combine(_baseDirectory, "outside.txt");
        Directory.CreateDirectory(Path.Combine(_root, "fixtures", ".hidden"));
        File.WriteAllText(Path.Combine(_root, "fixtures", "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "fixtures", ".hidden", "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, ".env"), "SECRET=1");
        File.WriteAllText(_outside, "outside");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_root, "fixtures", "link.txt"), _outside);
            _linked = true;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            // Windows without the privilege to link: the symlink entry is left out of the refusal test.
        }
    }

    public void Dispose() => Directory.Delete(_baseDirectory, recursive: true);

    [Fact]
    public void Resolves_project_relative_paths_to_regular_files_inside_the_root_keeps_them_as_given_and_records_the_allow()
    {
        var host = new RecordingHost();

        var authorized = E2E.UploadPaths.AuthorizeUploadPaths(host, ["fixtures/a.txt"], _root);

        Assert.Equal(["fixtures/a.txt"], authorized.Given);
        Assert.Equal([Path.Combine(_root, "fixtures", "a.txt")], authorized.Resolved);
        Assert.Equal(["upload.path allowed"], host.Recorded);
    }

    [Fact]
    public void Refuses_a_path_that_leaves_the_project_by_dots_or_by_a_symlink_and_records_the_refusal()
    {
        string[] given = _linked ? ["../outside.txt", _outside, "fixtures/link.txt"] : ["../outside.txt", _outside];
        foreach (var path in given)
        {
            var (code, recorded) = Authorize([path]);

            Assert.Equal("POLICY_DENIED", code);
            Assert.Equal(["upload.path denied POLICY_DENIED"], recorded);
        }
    }

    [Fact]
    public void Refuses_hidden_files_and_anything_under_a_hidden_directory()
    {
        Assert.Equal("POLICY_DENIED", Authorize([".env"]).Code);
        Assert.Equal("POLICY_DENIED", Authorize(["fixtures/.hidden/b.txt"]).Code);
    }

    [Fact]
    public void Reports_a_missing_file_or_a_directory_as_a_wrong_argument_not_a_policy_refusal_recording_nothing()
    {
        foreach (var path in new[] { "fixtures/missing.txt", "fixtures" })
        {
            var (code, recorded) = Authorize([path]);

            Assert.Equal("INVALID_ARGUMENT", code);
            Assert.Empty(recorded);
        }
    }

    /// <summary>Upstream also passes a bare string where the list belongs; the typed port has no such input, so a missing list stands in for it.</summary>
    [Fact]
    public void Partial_Requires_a_non_empty_list_of_non_empty_strings_bounded()
    {
        Assert.Equal("INVALID_ARGUMENT", Authorize([]).Code);
        Assert.Equal("INVALID_ARGUMENT", Authorize(["fixtures/a.txt", ""]).Code);
        Assert.Equal("INVALID_ARGUMENT", Authorize(null).Code);
        Assert.Equal("INVALID_ARGUMENT", Authorize([.. Enumerable.Repeat("fixtures/a.txt", 17)]).Code);
    }

    [Fact]
    public void Checks_every_path_before_any_is_accepted()
    {
        var (code, recorded) = Authorize(["fixtures/a.txt", ".env"]);

        Assert.Equal("POLICY_DENIED", code);
        Assert.Equal(["upload.path denied POLICY_DENIED"], recorded);
    }

    // Runs the policy against a recording host; returns the code it threw and what it recorded.
    private (string? Code, string[] Recorded) Authorize(IReadOnlyList<string?>? paths)
    {
        var host = new RecordingHost();
        try
        {
            E2E.UploadPaths.AuthorizeUploadPaths(host, paths, _root);
            return (null, [.. host.Recorded]);
        }
        catch (E2EException error)
        {
            return (error.Code, [.. host.Recorded]);
        }
    }

    private sealed class RecordingHost : IUploadPolicyHost
    {
        public List<string> Recorded { get; } = [];

        public void RecordPolicy(string name, PolicyDecision decision, string? code = null) =>
            Recorded.Add(string.Join(' ', new[] { name, decision.ToString().ToLowerInvariant(), code }.Where(part => part is not null)));
    }
}
