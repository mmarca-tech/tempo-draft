using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using TempoDraft.Core;
using TempoDraft.Persistence;
using TempoDraft.Sinks.Tempo;
using TempoDraft.Sources.Git;
using TempoDraft.Sources.Outlook;

namespace TempoDraft.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("tempo-draft");
            config.AddCommand<GenerateCommand>("generate")
                .WithDescription("Generate a tempo draft for a given date.");
        });

        return app.Run(args);
    }
}

public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        var (appSettings, mappingSettings) = await LoadSettingsAsync();
        var date = ResolveDate(settings, appSettings.TimeZoneId);

        if (date == null)
        {
            AnsiConsole.MarkupLine("[red]Specify --date, --today, or --yesterday.[/]");
            return -1;
        }

        var issueExtractor = new IssueKeyExtractor();
        var mapper = new WorkMapper(mappingSettings, issueExtractor);
        var aggregator = new WorkAggregator(appSettings, mapper);
        var fingerprintStore = new SqliteFingerprintStore(Path.Combine(appSettings.OutputFolder, "cache.db"));
        var exporter = new ExportService(appSettings, fingerprintStore);

        var workEvents = new List<WorkEvent>();

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                AnsiConsole.MarkupLine("[yellow]Outlook COM is only supported on Windows.[/]");
            }
            else
            {
                var collector = new ComOutlookCollector(appSettings);
                var outlookEvents = await collector.GetEventsAsync(date.Value);
                workEvents.AddRange(outlookEvents);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Outlook collection failed: {ex.Message}[/]");
        }

        if (appSettings.GitRepoPaths.Count == 0 && string.IsNullOrWhiteSpace(appSettings.GitRepoBasePath))
        {
            AnsiConsole.MarkupLine("[yellow]GitRepoPaths is empty and GitRepoBasePath is not set. Configure repositories to enable Git collection.[/]");
        }
        else
        {
            var collector = new GitCollector(appSettings, message => AnsiConsole.MarkupLine($"[yellow]{message}[/]"));
            var gitEvents = await collector.GetEventsAsync(date.Value);
            workEvents.AddRange(gitEvents);
        }

        var drafts = aggregator.BuildDrafts(date.Value, workEvents);

        RenderDrafts(drafts);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run enabled. No export performed.[/]");
            return 0;
        }

        var result = await exporter.ExportAsync(date.Value, drafts, settings.ForceUpload);
        AnsiConsole.MarkupLine($"[green]Exported {result.Exported.Count} entries. Skipped {result.Skipped.Count} duplicates.[/]");

        if (settings.Upload)
        {
            if (appSettings.Jira == null)
            {
                AnsiConsole.MarkupLine("[yellow]Jira settings missing. Configure BaseUrl/Email/ApiToken in the settings database.[/]");
            }
            else
            {
                var uploader = new JiraWorklogUploader(appSettings.Jira);
                await uploader.UploadAsync(date.Value, result.Exported, appSettings, message => AnsiConsole.MarkupLine($"[grey]{message}[/]"));
                AnsiConsole.MarkupLine($"[green]Uploaded {result.Exported.Count} worklogs to Jira.[/]");
            }
        }

        return 0;
    }

    private static void RenderDrafts(IReadOnlyList<WorkLogDraft> drafts)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Issue");
        table.AddColumn("Minutes");
        table.AddColumn("Description");
        table.AddColumn("Source");
        table.AddColumn("Evidence");

        foreach (var draft in drafts.OrderBy(d => d.IssueKey).ThenBy(d => d.Description))
        {
            table.AddRow(draft.IssueKey, draft.Minutes.ToString(), draft.Description, draft.Source, draft.Evidence);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total minutes: {drafts.Sum(d => d.Minutes)}[/]");
    }

    private static async Task<(AppSettings AppSettings, MappingSettings MappingSettings)> LoadSettingsAsync()
    {
        var basePath = Directory.GetCurrentDirectory();
        var settingsStore = new SqliteSettingsStore(Path.Combine(basePath, "export", "cache.db"));

        var appSettings = await settingsStore.LoadAppSettingsAsync();
        var mappingSettings = await settingsStore.LoadMappingsAsync();

        return (appSettings, mappingSettings);
    }

    private static DateOnly? ResolveDate(GenerateSettings settings, string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(settings.Date))
        {
            if (DateOnly.TryParse(settings.Date, out var date))
            {
                return date;
            }

            return null;
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz);
        if (settings.Today)
        {
            return DateOnly.FromDateTime(now.DateTime);
        }

        if (settings.Yesterday)
        {
            return DateOnly.FromDateTime(now.DateTime.AddDays(-1));
        }

        return null;
    }
}

public sealed class GenerateSettings : CommandSettings
{
    [CommandOption("--date")]
    public string? Date { get; init; }

    [CommandOption("--today")]
    public bool Today { get; init; }

    [CommandOption("--yesterday")]
    public bool Yesterday { get; init; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [CommandOption("--upload")]
    public bool Upload { get; init; }

    [CommandOption("--force-upload")]
    public bool ForceUpload { get; init; }

    public override ValidationResult Validate()
    {
        var flags = new[] { !string.IsNullOrWhiteSpace(Date), Today, Yesterday };
        if (flags.Count(f => f) != 1)
        {
            return ValidationResult.Error("Specify exactly one of --date, --today, or --yesterday.");
        }

        return ValidationResult.Success();
    }
}
