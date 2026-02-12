using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TempoDraft.Core;

namespace TempoDraft.Sources.Git;

public sealed class GitCollector
{
    private readonly AppSettings _settings;
    private readonly Action<string>? _warningCallback;

    public GitCollector(AppSettings settings, Action<string>? warningCallback = null)
    {
        _settings = settings;
        _warningCallback = warningCallback;
    }

    public async Task<IReadOnlyList<WorkEvent>> GetEventsAsync(DateOnly date)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
        var dayStart = new DateTimeOffset(new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified), tz.GetUtcOffset(new DateTime(date.Year, date.Month, date.Day)));
        var dayEnd = new DateTimeOffset(new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Unspecified), tz.GetUtcOffset(new DateTime(date.Year, date.Month, date.Day)));

        var results = new List<WorkEvent>();
        var repoPaths = ExpandRepoPaths();
        foreach (var repoPath in repoPaths)
        {
            if (!Directory.Exists(repoPath) || !Directory.Exists(Path.Combine(repoPath, ".git")))
            {
                _warningCallback?.Invoke($"Not a git repo: {repoPath}");
                continue;
            }

            List<GitCommit> commits;
            try
            {
                commits = await GetCommitsAsync(repoPath, dayStart, dayEnd);
            }
            catch (Exception ex)
            {
                _warningCallback?.Invoke($"Git log failed for {repoPath}: {ex.Message}");
                continue;
            }
            if (commits.Count == 0)
            {
                continue;
            }

            var clusters = ClusterCommits(commits, TimeSpan.FromMinutes(_settings.CommitClusterGapMinutes));
            var repoName = new DirectoryInfo(repoPath).Name;

            foreach (var cluster in clusters)
            {
                var start = cluster.First().Timestamp;
                var end = cluster.Last().Timestamp;
                if ((end - start).TotalMinutes < 10)
                {
                    end = start.AddMinutes(_settings.DefaultCodingBlockMinutes);
                }

                var evidence = $"git:{repoName}:{string.Join(',', cluster.Select(c => c.Sha))}";
                var raw = string.Join("\n", cluster.Select(c => c.Message));

                results.Add(new WorkEvent
                {
                    Start = start,
                    End = end,
                    Source = "git",
                    Title = $"Coding ({repoName})",
                    RawText = raw,
                    Evidence = evidence
                });
            }
        }

        return results;
    }

    private IReadOnlyList<string> ExpandRepoPaths()
    {
        if (_settings.GitRepoPaths.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(_settings.GitRepoBasePath))
            {
                return Array.Empty<string>();
            }

            var basePath = _settings.GitRepoBasePath;
            var repos = new List<string>();

            if (Directory.Exists(Path.Combine(basePath, ".git")))
            {
                repos.Add(basePath);
            }

            repos.AddRange(Directory.EnumerateDirectories(basePath)
                .Where(dir => Directory.Exists(Path.Combine(dir, ".git"))));

            return repos;
        }

        if (string.IsNullOrWhiteSpace(_settings.GitRepoBasePath))
        {
            return _settings.GitRepoPaths;
        }

        return _settings.GitRepoPaths
            .Select(path => Path.IsPathRooted(path) ? path : Path.Combine(_settings.GitRepoBasePath, path))
            .ToList();
    }

    private async Task<List<GitCommit>> GetCommitsAsync(string repoPath, DateTimeOffset start, DateTimeOffset end)
    {
        var args = $"log --since=\"{start.UtcDateTime:o}\" --until=\"{end.UtcDateTime:o}\" --pretty=format:%H|%cI|%s --no-merges";
        var output = await RunGitAsync(repoPath, args);
        var commits = new List<GitCommit>();

        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 3);
            if (parts.Length < 3)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(parts[1], out var timestamp))
            {
                continue;
            }

            commits.Add(new GitCommit
            {
                Sha = parts[0],
                Timestamp = timestamp,
                Message = parts[2]
            });
        }

        return commits.OrderBy(c => c.Timestamp).ToList();
    }

    public static List<List<GitCommit>> ClusterCommits(List<GitCommit> commits, TimeSpan gap)
    {
        var clusters = new List<List<GitCommit>>();
        if (commits.Count == 0)
        {
            return clusters;
        }

        var current = new List<GitCommit> { commits[0] };
        for (int i = 1; i < commits.Count; i++)
        {
            var prev = commits[i - 1];
            var next = commits[i];
            if (next.Timestamp - prev.Timestamp <= gap)
            {
                current.Add(next);
            }
            else
            {
                clusters.Add(current);
                current = new List<GitCommit> { next };
            }
        }

        clusters.Add(current);
        return clusters;
    }

    private static async Task<string> RunGitAsync(string repoPath, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git failed: {error}");
        }

        return output.ToString();
    }

    public sealed class GitCommit
    {
        public required string Sha { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public required string Message { get; init; }
    }
}
