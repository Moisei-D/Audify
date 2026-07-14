namespace Audify.Application.DTOs;

/// <summary>
/// A resolved date window used to filter every stats query. Always built by
/// StatsService itself (see ResolveRangeAsync) - never constructed directly from
/// "now", because the data being analysed is historical and can be years old.
/// "Last N months" is anchored to the most recent event in the dataset, not to
/// today's real-world date.
/// </summary>
public record DateRangeFilter(DateTime FromUtc, DateTime ToUtc);

/// <summary>High level summary card shown at the top of the dashboard.</summary>
public record ListeningSummaryDto(
    long TotalTracksPlayed,
    double TotalMinutesListened,
    int UniqueArtists,
    int UniqueTracks,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc
);

/// <summary>One row in the "Top Artists" chart/table.</summary>
public record TopArtistDto(string ArtistName, int PlayCount, double MinutesListened);

/// <summary>One row in the "Top Tracks" chart/table.</summary>
public record TopTrackDto(string TrackName, string ArtistName, int PlayCount, double MinutesListened);

/// <summary>One row in the "Top Albums" chart/table.</summary>
public record TopAlbumDto(string AlbumName, string ArtistName, int PlayCount, double MinutesListened);

/// <summary>One point on the listening-over-time trend chart.</summary>
public record MonthlyListeningDto(string Month, double MinutesListened);

/// <summary>Placeholder for genre breakdown. Spotify's personal-data export does NOT include
/// genre information, so this is populated only once the future Spotify Web API
/// integration (artist -> genres lookup) is wired up. See README "Roadmap".
/// </summary>
public record TopGenreDto(string GenreName, int PlayCount);

/// <summary>Returned after a successful drag-and-drop upload, so the UI can confirm what was loaded.</summary>
public record UploadResultDto(
    int FilesProcessed,
    int EventsLoaded,
    int TrackEventsLoaded,
    DateTime? EarliestEventUtc,
    DateTime? LatestEventUtc
);

/// <summary>The full date span covered by the currently loaded dataset - used to size the
/// slider/date pickers and to default the dashboard to a range that actually has data in it.</summary>
public record DatasetRangeDto(DateTime EarliestUtc, DateTime LatestUtc, int TotalTrackEvents);

/// <summary>Minutes listened broken down by day of week (Monday..Sunday) - a "listening habits" chart.</summary>
public record DayOfWeekStatDto(int DayIndex, string DayName, double MinutesListened, int PlayCount);

/// <summary>Minutes listened broken down by hour of day (0-23, UTC) - Audify's "listening clock".</summary>
public record HourOfDayStatDto(int Hour, double MinutesListened, int PlayCount);

/// <summary>Minutes listened broken down by device/platform (android, ios, web_player, ...).</summary>
public record PlatformStatDto(string Platform, double MinutesListened, int PlayCount);

/// <summary>Minutes listened broken down by content type (music tracks vs podcasts vs audiobooks).</summary>
public record ContentTypeStatDto(string ContentType, double MinutesListened, int PlayCount);

/// <summary>Assorted "listening habits" numbers that don't fit neatly into a chart/list.</summary>
public record ListeningHabitsDto(
    double SkipRatePercent,
    int LongestStreakDays,
    DateTime? MostActiveDayUtc,
    double MostActiveDayMinutes
);
