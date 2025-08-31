using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;

record GoogleOptions(string ClientId, string ClientSecret, string CalendarId, string TimeZone);
record Rule(string MatchType, string Pattern, string? ColorId = null);

static class MatchHelper
{
    public static bool Matches(string? summary, Rule rule)
    {
        if (string.IsNullOrWhiteSpace(summary)) return false;
        return rule.MatchType?.ToLowerInvariant() switch
        {
            "equals" => string.Equals(summary, rule.Pattern, StringComparison.OrdinalIgnoreCase),
            "contains" => summary.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            "regex" => Regex.IsMatch(summary, rule.Pattern, RegexOptions.IgnoreCase),
            _ => false
        };
    }
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Args: start end [--delete] [--copyTo YYYY-MM-DD]
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run -- <start ISO> <end ISO> [--delete] [--copyTo <start-of-target-period ISO>]");
            Console.WriteLine("Example: dotnet run -- 2025-09-01 2025-09-14 --copyTo 2025-09-14");
            return 1;
        }

        var startArg = args[0];
        var endArg = args[1];

        if (!DateTimeOffset.TryParse(startArg, out var srcStart))
        {
            Console.WriteLine("Invalid start date.");
            return 1;
        }
        if (!DateTimeOffset.TryParse(endArg, out var srcEnd))
        {
            Console.WriteLine("Invalid end date.");
            return 1;
        }

        var deleteMode = args.Any(a => a.Equals("--delete", StringComparison.OrdinalIgnoreCase));
        var copyToIndex = Array.FindIndex(args, a => a.Equals("--copyTo", StringComparison.OrdinalIgnoreCase));
        DateTimeOffset? copyToStart = null;

        if (copyToIndex >= 0)
        {
            if (copyToIndex + 1 >= args.Length || !DateTimeOffset.TryParse(args[copyToIndex + 1], out var copyStart))
            {
                Console.WriteLine("Invalid or missing date after --copyTo.");
                return 1;
            }
            copyToStart = copyStart;
        }

        if (deleteMode && copyToStart is not null)
        {
            Console.WriteLine("Cannot use --delete and --copyTo together in one run.");
            return 1;
        }

        // Load configuration
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var google = config.GetSection("Google").Get<GoogleOptions>()
                     ?? throw new InvalidOperationException("Missing Google section in appsettings.json");

        var colorRules = config.GetSection("ColorRules").Get<List<Rule>>() ?? new();
        var deleteRules = config.GetSection("DeleteRules").Get<List<Rule>>() ?? new();

        // Auth
        var secrets = new ClientSecrets
        {
            ClientId = google.ClientId,
            ClientSecret = google.ClientSecret
        };

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { CalendarService.Scope.Calendar }, // full calendar scope (includes update/delete)
            "user",
            CancellationToken.None,
            new FileDataStore("token_store", true) // token.json will be stored here
        );

        // Service
        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GCColorer"
        });

        var calendarId = string.IsNullOrWhiteSpace(google.CalendarId) ? "primary" : google.CalendarId;

        if (copyToStart is not null)
        {
            // COPY MODE
            await CopyEventsAsync(service, calendarId, srcStart, srcEnd, copyToStart.Value, google.TimeZone);
            return 0;
        }

        // Normal modes: color or delete
        if (deleteMode)
        {
            var deleted = await DeleteEventsAsync(service, calendarId, srcStart, srcEnd, deleteRules);
            Console.WriteLine($"\nDone. Deleted: {deleted}");
            return 0;
        }
        else
        {
            var updated = await RecolorEventsAsync(service, calendarId, srcStart, srcEnd, colorRules);
            Console.WriteLine($"\nDone. Updated: {updated}");
            return 0;
        }
    }

    static EventsResource.ListRequest BuildList(CalendarService service, string calendarId, DateTimeOffset start, DateTimeOffset end)
    {
        var req = service.Events.List(calendarId);
        req.TimeMinDateTimeOffset = start.UtcDateTime;
        req.TimeMaxDateTimeOffset = end.UtcDateTime;
        req.SingleEvents = true;   // expand recurring
        req.ShowDeleted = false;
        req.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        return req;
    }

    static async Task<int> RecolorEventsAsync(CalendarService service, string calendarId, DateTimeOffset start, DateTimeOffset end, List<Rule> colorRules)
    {
        Console.WriteLine($"[COLOR] {start}..{end} on '{calendarId}'");
        var updated = 0;
        var req = BuildList(service, calendarId, start, end);

        string? pageToken = null;
        do
        {
            req.PageToken = pageToken;
            var feed = await req.ExecuteAsync();

            if (feed.Items is { Count: > 0 })
            {
                foreach (var ev in feed.Items)
                {
                    var summary = ev.Summary ?? "";
                    var rule = colorRules.FirstOrDefault(r => MatchHelper.Matches(summary, r));
                    if (rule is not null && !string.IsNullOrEmpty(rule.ColorId))
                    {
                        if (ev.ColorId != rule.ColorId)
                        {
                            ev.ColorId = rule.ColorId;
                            await service.Events.Update(ev, calendarId, ev.Id).ExecuteAsync();
                            updated++;
                            Console.WriteLine($"[UPD] {ev.Start?.DateTimeDateTimeOffset ?? (object?)ev.Start?.Date}  {summary}  -> colorId={rule.ColorId}");
                        }
                        else
                        {
                            Console.WriteLine($"[SKIP] {ev.Start?.DateTimeDateTimeOffset ?? (object?)ev.Start?.Date}  {summary} (already {rule.ColorId})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[NO RULE] {ev.Start?.DateTimeDateTimeOffset ?? (object?)ev.Start?.Date}  {summary}");
                    }
                }
            }

            pageToken = feed.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return updated;
    }

    static async Task<int> DeleteEventsAsync(CalendarService service, string calendarId, DateTimeOffset start, DateTimeOffset end, List<Rule> deleteRules)
    {
        Console.WriteLine($"[DELETE] {start}..{end} on '{calendarId}'");
        var deleted = 0;
        var req = BuildList(service, calendarId, start, end);

        string? pageToken = null;
        do
        {
            req.PageToken = pageToken;
            var feed = await req.ExecuteAsync();

            if (feed.Items is { Count: > 0 })
            {
                foreach (var ev in feed.Items)
                {
                    var summary = ev.Summary ?? "";
                    if (deleteRules.Any(r => MatchHelper.Matches(summary, r)))
                    {
                        await service.Events.Delete(calendarId, ev.Id).ExecuteAsync();
                        deleted++;
                        Console.WriteLine($"[DEL] {ev.Start?.DateTimeDateTimeOffset ?? (object?)ev.Start?.Date}  {summary}");
                    }
                }
            }

            pageToken = feed.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return deleted;
    }

    static async Task CopyEventsAsync(CalendarService service, string calendarId,
        DateTimeOffset srcStart, DateTimeOffset srcEnd,
        DateTimeOffset copyToStart, string? timeZone)
    {
        Console.WriteLine($"[COPY] {srcStart}..{srcEnd}  ->  starts at {copyToStart}  (tz={timeZone ?? "calendar default"})");

        // delta between start of the target period and start of the source one
        var delta = copyToStart - srcStart;
        if (delta == TimeSpan.Zero)
        {
            Console.WriteLine("Target start equals source start; nothing to shift. Aborting.");
            return;
        }

        // Load all events from the source period
        var listReq = BuildList(service, calendarId, srcStart, srcEnd);

        // To avoid duplicates: get all events in the target window (of the same width as copied window) upfront
        var tgtStart = copyToStart;
        var tgtEnd = copyToStart + (srcEnd - srcStart);
        var targetIndex = await BuildTargetIndexAsync(service, calendarId, tgtStart, tgtEnd);

        int created = 0, skipped = 0;

        string? pageToken = null;
        do
        {
            listReq.PageToken = pageToken;
            var feed = await listReq.ExecuteAsync();

            if (feed.Items is { Count: > 0 })
            {
                foreach (var src in feed.Items)
                {
                    // Check all-day vs timed
                    var isAllDay = src.Start?.Date != null && src.Start.DateTimeDateTimeOffset == null;

                    EventDateTime newStart, newEnd;

                    if (isAllDay)
                    {
                        // All-day events are stored as Date (YYYY-MM-DD), without time; shift by days
                        var srcStartDate = DateTimeOffset.Parse(src.Start!.Date!).Date;
                        var srcEndDate = DateTimeOffset.Parse(src.End!.Date!).Date;   // Google stores end as EXCLUSIVE for all-day
                        var newStartDate = srcStartDate + delta;
                        var newEndDate = srcEndDate + delta;

                        newStart = new EventDateTime { Date = newStartDate.ToString("yyyy-MM-dd") };
                        newEnd = new EventDateTime { Date = newEndDate.ToString("yyyy-MM-dd") };
                    }
                    else
                    {
                        // Timed: shift by delta, considering local timezone if it was set
                        var sdt = src.Start!.DateTimeDateTimeOffset!.Value + delta;
                        var edt = src.End!.DateTimeDateTimeOffset!.Value + delta;

                        newStart = new EventDateTime
                        {
                            DateTimeDateTimeOffset = sdt,
                             // preserve original timezone or use from settings
                        };
                        newEnd = new EventDateTime
                        {
                            DateTimeDateTimeOffset = edt,
                            TimeZone = src.End!.TimeZone ?? timeZone
                        };
                    }

                    var newSummary = src.Summary;

                    // duplicate protection: key = (Summary, Start.Date or DateTime)
                    var dupKey = isAllDay
                        ? $"{newSummary}__ALlday__{newStart.Date}"
                        : $"{newSummary}__Timed__{newStart.DateTimeDateTimeOffset:O}";

                    if (targetIndex.Contains(dupKey))
                    {
                        skipped++;
                        Console.WriteLine($"[SKIP-EXISTS] {newStart.DateTimeDateTimeOffset ?? (object?)newStart.Date} {newSummary}");
                        continue;
                    }

                    var insert = new Event
                    {
                        Summary = newSummary,
                        Description = src.Description,
                        Location = src.Location,
                        Start = newStart,
                        End = newEnd,
                        ColorId = src.ColorId,
                        Reminders = src.Reminders,
                        Visibility = src.Visibility,
                        Source = src.Source
                        // Don't copy Recurrence since we work with expanded events; RRULE can be transferred if needed
                    };

                    // copying attendees/organizer is usually not needed for personal schedules, but can be done:
                    // insert.Attendees = src.Attendees;

                    await service.Events.Insert(insert, calendarId).ExecuteAsync();
                    created++;
                    Console.WriteLine($"[NEW] {newStart.DateTimeDateTimeOffset ?? (object?)newStart.Date}  {newSummary}");
                }
            }

            pageToken = feed.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        Console.WriteLine($"\nCopy finished. Created: {created}, Skipped (duplicates): {skipped}");
    }

    static async Task<HashSet<string>> BuildTargetIndexAsync(CalendarService service, string calendarId, DateTimeOffset tgtStart, DateTimeOffset tgtEnd)
    {
        var index = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var req = BuildList(service, calendarId, tgtStart, tgtEnd);

        string? pageToken = null;
        do
        {
            req.PageToken = pageToken;
            var feed = await req.ExecuteAsync();
            if (feed.Items is { Count: > 0 })
            {
                foreach (var ev in feed.Items)
                {
                    var isAllDay = ev.Start?.Date != null && ev.Start.DateTimeDateTimeOffset == null;
                    var key = isAllDay
                        ? $"{ev.Summary}__ALlday__{ev.Start!.Date}"
                        : $"{ev.Summary}__Timed__{ev.Start!.DateTimeDateTimeOffset:O}";
                    index.Add(key);
                }
            }
            pageToken = feed.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return index;
    }

}
