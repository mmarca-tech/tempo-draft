using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TempoDraft.Core;

public sealed class ExportService
{
    private readonly AppSettings _settings;
    private readonly IFingerprintStore _store;

    public ExportService(AppSettings settings, IFingerprintStore store)
    {
        _settings = settings;
        _store = store;
    }

    public async Task<ExportResult> ExportAsync(DateOnly date, IReadOnlyList<WorkLogDraft> drafts, bool includeDuplicates = false)
    {
        await _store.EnsureCreatedAsync();

        var exported = new List<WorkLogDraft>();
        var skipped = new List<WorkLogDraft>();

        foreach (var draft in drafts)
        {
            var fingerprint = ComputeFingerprint(draft);
            if (!includeDuplicates && await _store.ExistsAsync(fingerprint))
            {
                skipped.Add(draft);
                continue;
            }

            exported.Add(draft);
            await _store.AddAsync(fingerprint, date.ToString("yyyy-MM-dd"));
        }

        var outputFolder = _settings.OutputFolder;
        Directory.CreateDirectory(outputFolder);

        var baseName = $"tempo_draft_{date:yyyy-MM-dd}";
        var csvPath = Path.Combine(outputFolder, $"{baseName}.csv");
        var mdPath = Path.Combine(outputFolder, $"{baseName}.md");

        await WriteCsvAsync(csvPath, exported);
        await WriteMarkdownAsync(mdPath, date, exported);

        return new ExportResult
        {
            Exported = exported,
            Skipped = skipped
        };
    }

    private static async Task WriteCsvAsync(string path, IReadOnlyList<WorkLogDraft> drafts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("date,issue_key,minutes,description,source,evidence");
        foreach (var draft in drafts)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                draft.Date.ToString("yyyy-MM-dd"),
                EscapeCsv(draft.IssueKey),
                draft.Minutes.ToString(),
                EscapeCsv(draft.Description),
                EscapeCsv(draft.Source),
                EscapeCsv(draft.Evidence)
            }));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task WriteMarkdownAsync(string path, DateOnly date, IReadOnlyList<WorkLogDraft> drafts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Tempo Draft {date:yyyy-MM-dd}");
        sb.AppendLine();

        if (drafts.Count == 0)
        {
            sb.AppendLine("No new entries to export.");
            await File.WriteAllTextAsync(path, sb.ToString());
            return;
        }

        sb.AppendLine($"Total minutes: {drafts.Sum(d => d.Minutes)}");
        sb.AppendLine();

        foreach (var group in drafts.GroupBy(d => d.IssueKey))
        {
            sb.AppendLine($"## {group.Key} ({group.Sum(d => d.Minutes)} min)");
            foreach (var item in group)
            {
                var timeRange = item.Start != null && item.End != null
                    ? $"{item.Start:HH:mm}-{item.End:HH:mm} "
                    : string.Empty;
                sb.AppendLine($"- {timeRange}{item.Minutes} min - {item.Description} [{item.Source}] ({item.Evidence})");
            }

            sb.AppendLine();
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    public static string ComputeFingerprint(WorkLogDraft draft)
    {
        var raw = $"{draft.Date:yyyy-MM-dd}|{draft.IssueKey}|{draft.Minutes}|{draft.Source}|{draft.Evidence}|{draft.Description}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
