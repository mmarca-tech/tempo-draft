using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using TempoDraft.Core;

namespace TempoDraft.Sources.Outlook;

[SupportedOSPlatform("windows")]
public sealed class ComOutlookCollector
{
    private readonly AppSettings _appSettings;
    private const int OlFolderCalendar = 9;

    public ComOutlookCollector(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public Task<IReadOnlyList<WorkEvent>> GetEventsAsync(DateOnly date)
    {
        var results = new List<WorkEvent>();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_appSettings.TimeZoneId);
        var dayStart = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var dayEnd = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Unspecified);

        object? outlook = null;
        object? session = null;
        object? calendar = null;
        object? items = null;

        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application")
                ?? throw new InvalidOperationException("Outlook.Application COM ProgID not found. Is Outlook installed?");
            outlook = Activator.CreateInstance(outlookType);
            session = outlookType.InvokeMember("GetNamespace", System.Reflection.BindingFlags.InvokeMethod, null, outlook, new object[] { "MAPI" });
            calendar = session!.GetType().InvokeMember("GetDefaultFolder", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { OlFolderCalendar });
            items = calendar!.GetType().InvokeMember("Items", System.Reflection.BindingFlags.GetProperty, null, calendar, Array.Empty<object>());
            items!.GetType().InvokeMember("IncludeRecurrences", System.Reflection.BindingFlags.SetProperty, null, items, new object[] { true });
            items.GetType().InvokeMember("Sort", System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { "[Start]" });

            var filter = $"[Start] >= '{dayStart:MM/dd/yyyy HH:mm}' AND [End] <= '{dayEnd:MM/dd/yyyy HH:mm}'";
            var restricted = items.GetType().InvokeMember("Restrict", System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { filter });

            foreach (var appt in (System.Collections.IEnumerable)restricted!)
            {
                var meetingStatusObj = appt!.GetType().InvokeMember("MeetingStatus", System.Reflection.BindingFlags.GetProperty, null, appt, Array.Empty<object>());
                if (meetingStatusObj is int meetingStatus && meetingStatus == 5)
                {
                    continue;
                }

                var subject = appt!.GetType().InvokeMember("Subject", System.Reflection.BindingFlags.GetProperty, null, appt, Array.Empty<object>()) as string ?? "(no subject)";
                var ignore = _appSettings.IgnoreCalendarSubjects ?? new List<string>();
                if (ignore.Exists(s => subject.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var startObj = appt.GetType().InvokeMember("Start", System.Reflection.BindingFlags.GetProperty, null, appt, Array.Empty<object>());
                var endObj = appt.GetType().InvokeMember("End", System.Reflection.BindingFlags.GetProperty, null, appt, Array.Empty<object>());
                if (startObj is not DateTime startTime || endObj is not DateTime endTime)
                {
                    continue;
                }

                var start = new DateTimeOffset(startTime, tz.GetUtcOffset(startTime));
                var end = new DateTimeOffset(endTime, tz.GetUtcOffset(endTime));
                var entryId = appt.GetType().InvokeMember("EntryID", System.Reflection.BindingFlags.GetProperty, null, appt, Array.Empty<object>()) as string ?? "unknown";
                var evidence = $"outlookcom:{entryId}";

                results.Add(new WorkEvent
                {
                    Start = start,
                    End = end,
                    Source = "outlook",
                    Title = subject,
                    RawText = subject,
                    Evidence = evidence
                });
            }
        }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(calendar);
            ReleaseComObject(session);
            ReleaseComObject(outlook);
        }

        return Task.FromResult<IReadOnlyList<WorkEvent>>(results);
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject != null && System.Runtime.InteropServices.Marshal.IsComObject(comObject))
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
        }
    }
}
