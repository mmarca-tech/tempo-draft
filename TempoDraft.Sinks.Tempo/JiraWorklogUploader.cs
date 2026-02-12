using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TempoDraft.Core;

namespace TempoDraft.Sinks.Tempo;

public sealed class JiraWorklogUploader
{
    private readonly JiraSettings _settings;
    private readonly HttpClient _http;

    public JiraWorklogUploader(JiraSettings settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        _http = httpClient ?? new HttpClient();
    }

    public async Task UploadAsync(DateOnly date, IReadOnlyList<WorkLogDraft> drafts, AppSettings appSettings, Action<string>? debugLog = null, CancellationToken cancellationToken = default)
    {
        _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Email}:{_settings.ApiToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var draft in drafts)
        {
            if (string.Equals(draft.IssueKey, "UNASSIGNED", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var started = draft.Start ?? DefaultStart(date, appSettings);
            var startedValue = FormatJiraDateTime(started, appSettings.UploadTimeZoneId ?? appSettings.TimeZoneId);
            debugLog?.Invoke($"Uploading worklog: issue={draft.IssueKey}, minutes={draft.Minutes}, started={startedValue}");
            var payload = new
            {
                timeSpentSeconds = draft.Minutes * 60,
                started = startedValue,
                comment = string.IsNullOrWhiteSpace(draft.Description)
                    ? null
                    : new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph",
                                content = new[] { new { type = "text", text = draft.Description } }
                            }
                        }
                    }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"rest/api/3/issue/{draft.IssueKey}/worklog", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Jira worklog upload failed for {draft.IssueKey}: {(int)response.StatusCode} {response.ReasonPhrase} - {body}");
            }
        }
    }

    private static DateTimeOffset DefaultStart(DateOnly date, AppSettings appSettings)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(appSettings.TimeZoneId);
        var parts = appSettings.WorkdayStart.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var hour = int.Parse(parts[0]);
        var minute = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, tz.GetUtcOffset(local));
    }

    private static string FormatJiraDateTime(DateTimeOffset value, string? timeZoneId)
    {
        var adjusted = value;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            adjusted = TimeZoneInfo.ConvertTime(value, tz);
        }

        var formatted = adjusted.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        return StripColonFromOffset(formatted);
    }

    private static string StripColonFromOffset(string value)
    {
        if (value.Length < 5)
        {
            return value;
        }

        var signIndex = value.Length - 6;
        if (signIndex < 0)
        {
            return value;
        }

        var sign = value[signIndex];
        if (sign != '+' && sign != '-')
        {
            return value;
        }

        if (value[signIndex + 3] != ':')
        {
            return value;
        }

        return value.Remove(signIndex + 3, 1);
    }
}
