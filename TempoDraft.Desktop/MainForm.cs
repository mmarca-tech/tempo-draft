using TempoDraft.Core;
using TempoDraft.Persistence;
using TempoDraft.Sources.Git;
using TempoDraft.Sources.Outlook;
using TempoDraft.Sinks.Tempo;
using System.Drawing;

namespace TempoDraft.Desktop;

public partial class MainForm : Form
{
    private readonly SqliteSettingsStore _settingsStore;
    private readonly SqliteDraftOverrideStore _draftOverrideStore;
    private List<WorkLogDraft> _currentDrafts = new();
    private DateOnly? _currentDate;
    private string _currentTimeZoneId = "Europe/Tallinn";

    public MainForm()
    {
        InitializeComponent();
        _settingsStore = new SqliteSettingsStore(Path.Combine(AppContext.BaseDirectory, "export", "cache.db"));
        _draftOverrideStore = new SqliteDraftOverrideStore(Path.Combine(AppContext.BaseDirectory, "export", "cache.db"));
        try
        {
            LoadSettingsIntoUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "TempoDraft Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        datePicker.MaxDate = DateTime.Today;
        datePicker.Value = DateTime.Today;
        uploadButton.Enabled = false;
        outputGrid.CellEndEdit += OutputGrid_CellEndEdit;
        datePicker.ValueChanged += DatePicker_ValueChanged;
        AppendLog("Ready.");
    }

    private void appSettingsSaveButton_Click(object sender, EventArgs e)
    {
        var appSettings = BuildAppSettingsFromUi();
        _settingsStore.SaveAppSettingsAsync(appSettings).GetAwaiter().GetResult();
        AppendLog("Saved app settings to cache.db.");
    }

    private void mappingsSaveButton_Click(object sender, EventArgs e)
    {
        var mappings = BuildMappingSettingsFromUi();
        _settingsStore.SaveMappingsAsync(mappings).GetAwaiter().GetResult();
        AppendLog("Saved mappings to cache.db.");
    }

    private async void processButton_Click(object sender, EventArgs e)
    {
        SaveAllSettings();
        processButton.Enabled = false;
        uploadButton.Enabled = false;
        AppendLog("Processing...");

        try
        {
            var (appSettings, mappingSettings) = LoadSettings();
            var date = DateOnly.FromDateTime(datePicker.Value);
            var issueExtractor = new IssueKeyExtractor();
            var mapper = new WorkMapper(mappingSettings, issueExtractor);
            var aggregator = new WorkAggregator(appSettings, mapper);
            var fingerprintStore = new SqliteFingerprintStore(Path.Combine(appSettings.OutputFolder, "cache.db"));
            var exporter = new ExportService(appSettings, fingerprintStore);
            _currentTimeZoneId = appSettings.TimeZoneId;

            var workEvents = new List<WorkEvent>();

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    AppendLog("Outlook COM is only supported on Windows.");
                }
                else
                {
                    var collector = new ComOutlookCollector(appSettings);
                    workEvents.AddRange(await collector.GetEventsAsync(date));
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Outlook collection failed: {ex.Message}");
            }

            if (appSettings.GitRepoPaths.Count == 0 && string.IsNullOrWhiteSpace(appSettings.GitRepoBasePath))
            {
                AppendLog("GitRepoPaths is empty and GitRepoBasePath is not set. Configure repositories to enable Git collection.");
            }
            else
            {
                var collector = new GitCollector(appSettings, message => AppendLog(message));
                workEvents.AddRange(await collector.GetEventsAsync(date));
            }

            var drafts = aggregator.BuildDrafts(date, workEvents);
            var overrides = await _draftOverrideStore.LoadOverridesAsync(date);
            var mergedDrafts = ApplyOverrides(drafts, overrides);
            _currentDrafts = mergedDrafts.ToList();
            _currentDate = date;
            await RenderDraftsAsync(_currentDrafts);

            if (dryRunCheckBox.Checked)
            {
                AppendLog("Dry run enabled. No export performed.");
            }
            else
            {
                var result = await exporter.ExportAsync(date, drafts, forceUploadCheckBox.Checked);
                AppendLog($"Exported {result.Exported.Count} entries. Skipped {result.Skipped.Count} duplicates.");
            }

            uploadButton.Enabled = _currentDrafts.Count > 0;
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
        }
        finally
        {
            processButton.Enabled = true;
        }
    }

    private async void DatePicker_ValueChanged(object? sender, EventArgs e)
    {
        var date = DateOnly.FromDateTime(datePicker.Value);
        _currentDate = date;
        var overrides = await _draftOverrideStore.LoadOverridesAsync(date);
        _currentDrafts = overrides.Values.ToList();
        await RenderDraftsAsync(_currentDrafts);
        uploadButton.Enabled = _currentDrafts.Count > 0;
    }

    private async void uploadButton_Click(object sender, EventArgs e)
    {
        if (_currentDate == null || _currentDrafts.Count == 0)
        {
            AppendLog("Nothing to upload. Click Process first.");
            return;
        }

        var editedDrafts = ReadDraftsFromGrid();
        var totalMinutes = editedDrafts.Sum(d => d.Minutes);
        var confirm = MessageBox.Show(
            $"Upload {editedDrafts.Count} worklogs ({totalMinutes} minutes) to Jira?",
            "Confirm Upload",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            AppendLog("Upload cancelled.");
            return;
        }

        uploadButton.Enabled = false;
        try
        {
            var (appSettings, _) = LoadSettings();
            var fingerprintStore = new SqliteFingerprintStore(Path.Combine(appSettings.OutputFolder, "cache.db"));
            var exporter = new ExportService(appSettings, fingerprintStore);
            var exportResult = await exporter.ExportAsync(_currentDate.Value, editedDrafts, forceUploadCheckBox.Checked);
            AppendLog($"Exported {exportResult.Exported.Count} entries. Skipped {exportResult.Skipped.Count} duplicates.");

            if (appSettings.Jira == null)
            {
                AppendLog("Jira settings missing. Configure BaseUrl/Email/ApiToken in App Settings.");
                return;
            }

            var uploader = new JiraWorklogUploader(appSettings.Jira);
            await uploader.UploadAsync(_currentDate.Value, exportResult.Exported, appSettings, message => AppendLog(message));
            AppendLog($"Uploaded {exportResult.Exported.Count} worklogs to Jira.");
            MarkUploadedRows(exportResult.Exported);
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
        }
        finally
        {
            uploadButton.Enabled = _currentDrafts.Count > 0;
        }
    }

    private async Task RenderDraftsAsync(IReadOnlyList<WorkLogDraft> drafts)
    {
        outputGrid.Rows.Clear();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_currentTimeZoneId);
        var (appSettings, _) = LoadSettings();
        var fingerprintStore = new SqliteFingerprintStore(Path.Combine(appSettings.OutputFolder, "cache.db"));
        await fingerprintStore.EnsureCreatedAsync();

        foreach (var draft in drafts)
        {
            var start = FormatTimeCell(draft.Start, tz);
            var end = FormatTimeCell(draft.End, tz);
            var minutes = ComputeMinutes(draft.Start, draft.End, draft.Minutes).ToString();
            var fingerprint = ExportService.ComputeFingerprint(draft);
            var alreadyUploaded = await fingerprintStore.ExistsAsync(fingerprint);
            var rowIndex = outputGrid.Rows.Add(true, draft.IssueKey, minutes, draft.Description, start, end, draft.Source, draft.Evidence);
            var row = outputGrid.Rows[rowIndex];
            row.Tag = draft;
            ApplyUploadState(row, alreadyUploaded);
        }
        UpdateTotalMinutes();
    }

    private List<WorkLogDraft> ReadDraftsFromGrid()
    {
        var result = new List<WorkLogDraft>();
        foreach (DataGridViewRow row in outputGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var original = row.Tag as WorkLogDraft;
            if (original == null)
            {
                continue;
            }

            if (IsRowUploaded(row))
            {
                continue;
            }

            var include = row.Cells[0].Value as bool? ?? false;
            if (!include)
            {
                continue;
            }

            var issueKey = row.Cells[1].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(issueKey))
            {
                issueKey = "UNASSIGNED";
            }


            var description = row.Cells[3].Value?.ToString() ?? original.Description;
            var start = ParseTimeCell(row.Cells[4].Value?.ToString(), original.Date, original.Start);
            var end = ParseTimeCell(row.Cells[5].Value?.ToString(), original.Date, original.End);
            var minutes = ComputeMinutes(start, end, original.Minutes);

            result.Add(new WorkLogDraft
            {
                Date = original.Date,
                IssueKey = issueKey,
                Minutes = minutes,
                Description = description,
                Source = original.Source,
                Evidence = original.Evidence,
                Start = start,
                End = end
            });
        }
        return result;
    }

    private async void OutputGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_currentDate == null)
        {
            return;
        }

        if (e.RowIndex < 0 || e.RowIndex >= outputGrid.Rows.Count)
        {
            return;
        }

        var row = outputGrid.Rows[e.RowIndex];
        if (e.ColumnIndex == 0)
        {
            UpdateTotalMinutes();
            return;
        }

        var updated = BuildDraftFromRow(row);
        if (updated == null)
        {
            return;
        }

        await _draftOverrideStore.SaveOverrideAsync(updated);
        row.Tag = updated;
        row.Cells[2].Value = updated.Minutes.ToString();
        UpdateTotalMinutes();
    }

    private WorkLogDraft? BuildDraftFromRow(DataGridViewRow row)
    {
        var original = row.Tag as WorkLogDraft;
        if (original == null)
        {
            return null;
        }

        if (IsRowUploaded(row))
        {
            return null;
        }

        var issueKey = row.Cells[1].Value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            issueKey = "UNASSIGNED";
        }


        var description = row.Cells[3].Value?.ToString() ?? original.Description;
        var start = ParseTimeCell(row.Cells[4].Value?.ToString(), original.Date, original.Start);
        var end = ParseTimeCell(row.Cells[5].Value?.ToString(), original.Date, original.End);
        var minutes = ComputeMinutes(start, end, original.Minutes);

        return new WorkLogDraft
        {
            Date = original.Date,
            IssueKey = issueKey,
            Minutes = minutes,
            Description = description,
            Source = original.Source,
            Evidence = original.Evidence,
            Start = start,
            End = end
        };
    }

    private DateTimeOffset? ParseTimeCell(string? value, DateOnly date, DateTimeOffset? fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (TimeOnly.TryParse(value, out var time))
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_currentTimeZoneId);
            var local = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, tz.GetUtcOffset(local));
        }

        AppendLog($"Invalid time '{value}'. Use HH:mm or full date/time.");
        return fallback;
    }

    private static string FormatTimeCell(DateTimeOffset? value, TimeZoneInfo tz)
    {
        if (value == null)
        {
            return "";
        }

        var local = TimeZoneInfo.ConvertTime(value.Value, tz);
        return local.ToString("HH:mm");
    }

    private static int ComputeMinutes(DateTimeOffset? start, DateTimeOffset? end, int fallback)
    {
        if (start == null || end == null)
        {
            return fallback;
        }

        var minutes = (int)Math.Round((end.Value - start.Value).TotalMinutes);
        return Math.Max(0, minutes);
    }

    private void UpdateTotalMinutes()
    {
        var drafts = ReadDraftsFromGrid();
        AppendLog($"Total minutes: {drafts.Sum(d => d.Minutes)}");
    }

    private void MarkUploadedRows(IReadOnlyList<WorkLogDraft> uploadedDrafts)
    {
        if (uploadedDrafts.Count == 0)
        {
            return;
        }

        var uploadedFingerprints = new HashSet<string>(
            uploadedDrafts.Select(ExportService.ComputeFingerprint),
            StringComparer.OrdinalIgnoreCase);

        foreach (DataGridViewRow row in outputGrid.Rows)
        {
            if (row.IsNewRow || row.Tag is not WorkLogDraft draft)
            {
                continue;
            }

            var fingerprint = ExportService.ComputeFingerprint(draft);
            if (!uploadedFingerprints.Contains(fingerprint))
            {
                continue;
            }

            row.Cells[0].Value = true;
            ApplyUploadState(row, true);
        }

        UpdateTotalMinutes();
    }

    private static bool IsRowUploaded(DataGridViewRow row)
    {
        return row.Cells[0].Tag as bool? ?? false;
    }

    private static void ApplyUploadState(DataGridViewRow row, bool alreadyUploaded)
    {
        row.Cells[0].Tag = alreadyUploaded;
        if (!alreadyUploaded)
        {
            return;
        }

        row.ReadOnly = true;
        row.DefaultCellStyle.ForeColor = Color.DimGray;
        row.DefaultCellStyle.BackColor = Color.Gainsboro;
    }

    private static IReadOnlyList<WorkLogDraft> ApplyOverrides(IReadOnlyList<WorkLogDraft> drafts, Dictionary<string, WorkLogDraft> overrides)
    {
        if (overrides.Count == 0)
        {
            return drafts;
        }

        return drafts.Select(draft =>
        {
            var key = SqliteDraftOverrideStore.BuildKey(draft.Source, draft.Evidence);
            if (!overrides.TryGetValue(key, out var overrideDraft))
            {
                return draft;
            }

            return new WorkLogDraft
            {
                Date = draft.Date,
                IssueKey = overrideDraft.IssueKey,
                Minutes = overrideDraft.Minutes,
                Description = overrideDraft.Description,
                Source = draft.Source,
                Evidence = draft.Evidence,
                Start = overrideDraft.Start ?? draft.Start,
                End = overrideDraft.End ?? draft.End
            };
        }).ToList();
    }

    private void AppendLog(string message)
    {
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private (AppSettings AppSettings, MappingSettings MappingSettings) LoadSettings()
    {
        var appSettings = _settingsStore.LoadAppSettingsAsync().GetAwaiter().GetResult();
        var mappingSettings = _settingsStore.LoadMappingsAsync().GetAwaiter().GetResult();
        return (appSettings, mappingSettings);
    }

    private void LoadSettingsIntoUi()
    {
        var (appSettings, mappingSettings) = LoadSettings();

        timeZoneIdTextBox.Text = appSettings.TimeZoneId;
        workdayStartTextBox.Text = appSettings.WorkdayStart;
        workdayEndTextBox.Text = appSettings.WorkdayEnd;
        roundingMinutesUpDown.Value = appSettings.RoundingMinutes;
        minBlockMinutesUpDown.Value = appSettings.MinBlockMinutes;
        mergeGapMinutesUpDown.Value = appSettings.MergeGapMinutes;
        defaultCodingMinutesUpDown.Value = appSettings.DefaultCodingBlockMinutes;
        commitClusterGapUpDown.Value = appSettings.CommitClusterGapMinutes;
        outputFolderTextBox.Text = appSettings.OutputFolder;
        gitRepoBasePathTextBox.Text = appSettings.GitRepoBasePath ?? "";

        gitRepoPathsTextBox.Text = string.Join(Environment.NewLine, appSettings.GitRepoPaths ?? new List<string>());
        ignoreSubjectsTextBox.Text = string.Join(Environment.NewLine, appSettings.IgnoreCalendarSubjects ?? new List<string>());

        uploadTimeZoneTextBox.Text = appSettings.UploadTimeZoneId ?? appSettings.TimeZoneId;
        meetingRoundUpMinutesUpDown.Value = appSettings.MeetingRoundUpToMinutes ?? 0;
        jiraBaseUrlTextBox.Text = appSettings.Jira?.BaseUrl ?? "";
        jiraEmailTextBox.Text = appSettings.Jira?.Email ?? "";
        jiraApiTokenTextBox.Text = appSettings.Jira?.ApiToken ?? "";

        FillGrid(keywordGrid, mappingSettings.KeywordToIssueKey);
        FillGrid(subjectGrid, mappingSettings.SubjectContainsToIssueKey);
        FillGrid(repoDefaultGrid, mappingSettings.DefaultIssueKeyByRepo);
    }

    private MappingSettings BuildMappingSettingsFromUi()
    {
        return new MappingSettings
        {
            KeywordToIssueKey = ReadGrid(keywordGrid),
            SubjectContainsToIssueKey = ReadGrid(subjectGrid),
            DefaultIssueKeyByRepo = ReadGrid(repoDefaultGrid)
        };
    }

    private static List<string> SplitLines(string text)
    {
        return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static void FillGrid(DataGridView grid, Dictionary<string, string> data)
    {
        grid.Rows.Clear();
        foreach (var kvp in data)
        {
            grid.Rows.Add(kvp.Key, kvp.Value);
        }
    }

    private static Dictionary<string, string> ReadGrid(DataGridView grid)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var key = row.Cells[0].Value?.ToString()?.Trim();
            var value = row.Cells[1].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
            dict[key] = value;
        }
        return dict;
    }

    private void SaveAllSettings()
    {
        var appSettings = BuildAppSettingsFromUi();
        var mappings = BuildMappingSettingsFromUi();
        _settingsStore.SaveAppSettingsAsync(appSettings).GetAwaiter().GetResult();
        _settingsStore.SaveMappingsAsync(mappings).GetAwaiter().GetResult();
        AppendLog("Saved settings to cache.db.");
    }

    private AppSettings BuildAppSettingsFromUi()
    {
        var gitRepos = SplitLines(gitRepoPathsTextBox.Text);
        var ignoreSubjects = SplitLines(ignoreSubjectsTextBox.Text);

        return new AppSettings
        {
            TimeZoneId = timeZoneIdTextBox.Text.Trim(),
            WorkdayStart = workdayStartTextBox.Text.Trim(),
            WorkdayEnd = workdayEndTextBox.Text.Trim(),
            RoundingMinutes = (int)roundingMinutesUpDown.Value,
            MinBlockMinutes = (int)minBlockMinutesUpDown.Value,
            MergeGapMinutes = (int)mergeGapMinutesUpDown.Value,
            IgnoreCalendarSubjects = ignoreSubjects,
            GitRepoPaths = gitRepos,
            OutputFolder = outputFolderTextBox.Text.Trim(),
            DefaultCodingBlockMinutes = (int)defaultCodingMinutesUpDown.Value,
            CommitClusterGapMinutes = (int)commitClusterGapUpDown.Value,
            GitRepoBasePath = string.IsNullOrWhiteSpace(gitRepoBasePathTextBox.Text) ? null : gitRepoBasePathTextBox.Text.Trim(),
            UploadTimeZoneId = string.IsNullOrWhiteSpace(uploadTimeZoneTextBox.Text) ? null : uploadTimeZoneTextBox.Text.Trim(),
            MeetingRoundUpToMinutes = meetingRoundUpMinutesUpDown.Value <= 0 ? null : (int)meetingRoundUpMinutesUpDown.Value,
            Jira = new JiraSettings
            {
                BaseUrl = jiraBaseUrlTextBox.Text.Trim(),
                Email = jiraEmailTextBox.Text.Trim(),
                ApiToken = jiraApiTokenTextBox.Text.Trim()
            }
        };
    }
}










