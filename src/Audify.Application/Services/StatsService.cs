using Audify.Application.DTOs;
using Audify.Application.Interfaces;
using Audify.Domain.Entities;

namespace Audify.Application.Services;

/// <summary>
/// Computes dashboard statistics from raw play events.
/// Pure aggregation logic only - no knowledge of JSON, HTTP or Spotify's API lives here,
/// which is what makes it easy to unit test and to reuse once data starts coming
/// from the Spotify Web API instead of an uploaded export file.
/// </summary>
public class StatsService : IStatsService
{
    private readonly IPlayHistoryRepository _repository;

    public StatsService(IPlayHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListeningSummaryDto> GetSummaryAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (range, tracks) = await LoadTracksAsync(months, from, to, ct);

        return new ListeningSummaryDto(
            TotalTracksPlayed: tracks.Count,
            TotalMinutesListened: Math.Round(tracks.Sum(e => e.MinutesPlayed), 1),
            UniqueArtists: tracks.Select(e => e.ArtistName).Distinct().Count(),
            UniqueTracks: tracks.Select(e => e.SpotifyUri ?? e.TrackName).Distinct().Count(),
            RangeStartUtc: range.FromUtc,
            RangeEndUtc: range.ToUtc
        );
    }

    public async Task<IReadOnlyList<TopArtistDto>> GetTopArtistsAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        return tracks
            .GroupBy(e => e.ArtistName!)
            .Select(g => new TopArtistDto(g.Key, g.Count(), Math.Round(g.Sum(e => e.MinutesPlayed), 1)))
            .OrderByDescending(a => a.MinutesListened)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<TopTrackDto>> GetTopTracksAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        return tracks
            .GroupBy(e => (e.TrackName, e.ArtistName))
            .Select(g => new TopTrackDto(g.Key.TrackName!, g.Key.ArtistName!, g.Count(), Math.Round(g.Sum(e => e.MinutesPlayed), 1)))
            .OrderByDescending(t => t.MinutesListened)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<TopAlbumDto>> GetTopAlbumsAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        return tracks
            .Where(e => e.AlbumName is not null)
            .GroupBy(e => (e.AlbumName, e.ArtistName))
            .Select(g => new TopAlbumDto(g.Key.AlbumName!, g.Key.ArtistName!, g.Count(), Math.Round(g.Sum(e => e.MinutesPlayed), 1)))
            .OrderByDescending(a => a.MinutesListened)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyListeningDto>> GetListeningTrendAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        return tracks
            .GroupBy(e => new DateTime(e.PlayedAtUtc.Year, e.PlayedAtUtc.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyListeningDto(g.Key.ToString("yyyy-MM"), Math.Round(g.Sum(e => e.MinutesPlayed), 1)))
            .ToList();
    }

    public async Task<IReadOnlyList<DayOfWeekStatDto>> GetDayOfWeekBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        // Always return all 7 days, Monday-first, even if some have zero plays,
        // so the chart's x-axis stays stable regardless of the selected range.
        var byDay = tracks
            .GroupBy(e => e.PlayedAtUtc.DayOfWeek)
            .ToDictionary(g => g.Key, g => (Minutes: g.Sum(e => e.MinutesPlayed), Count: g.Count()));

        var orderedDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        return orderedDays
            .Select((day, index) =>
            {
                byDay.TryGetValue(day, out var stat);
                return new DayOfWeekStatDto(index, day.ToString(), Math.Round(stat.Minutes, 1), stat.Count);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<HourOfDayStatDto>> GetHourOfDayBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        var byHour = tracks
            .GroupBy(e => e.PlayedAtUtc.Hour)
            .ToDictionary(g => g.Key, g => (Minutes: g.Sum(e => e.MinutesPlayed), Count: g.Count()));

        return Enumerable.Range(0, 24)
            .Select(hour =>
            {
                byHour.TryGetValue(hour, out var stat);
                return new HourOfDayStatDto(hour, Math.Round(stat.Minutes, 1), stat.Count);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PlatformStatDto>> GetPlatformBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (_, tracks) = await LoadTracksAsync(months, from, to, ct);

        return tracks
            .GroupBy(e => NormalizePlatform(e.Platform))
            .Select(g => new PlatformStatDto(g.Key, Math.Round(g.Sum(e => e.MinutesPlayed), 1), g.Count()))
            .OrderByDescending(p => p.MinutesListened)
            .ToList();
    }

    public async Task<IReadOnlyList<ContentTypeStatDto>> GetContentTypeBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var range = await ResolveRangeAsync(months, from, to, ct);
        var all = await _repository.GetAllAsync(ct);

        // Unlike the other stats, this one intentionally includes ALL content types
        // (not just music tracks), since the whole point is to compare them.
        var inRange = all.Where(e => e.PlayedAtUtc >= range.FromUtc && e.PlayedAtUtc <= range.ToUtc && e.MsPlayed > 0);

        return inRange
            .GroupBy(e => e.ContentType)
            .Select(g => new ContentTypeStatDto(FormatContentType(g.Key), Math.Round(g.Sum(e => e.MinutesPlayed), 1), g.Count()))
            .OrderByDescending(c => c.MinutesListened)
            .ToList();
    }

    public async Task<ListeningHabitsDto> GetListeningHabitsAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var range = await ResolveRangeAsync(months, from, to, ct);
        var all = await _repository.GetAllAsync(ct);

        // Skip rate looks at every track *attempt* in range, including the near-instant
        // skips that have ms_played == 0 - those are exactly the plays we'd otherwise
        // filter out everywhere else, but they're the whole point of a skip rate.
        var trackAttempts = all
            .Where(e => e.ContentType == PlayContentType.Track)
            .Where(e => e.PlayedAtUtc >= range.FromUtc && e.PlayedAtUtc <= range.ToUtc)
            .ToList();

        var skipRate = trackAttempts.Count == 0
            ? 0
            : Math.Round(100.0 * trackAttempts.Count(e => e.Skipped) / trackAttempts.Count, 1);

        var listenedDays = trackAttempts
            .Where(e => e.MsPlayed > 0)
            .Select(e => e.PlayedAtUtc.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var longestStreak = ComputeLongestStreak(listenedDays);

        var mostActiveDay = trackAttempts
            .Where(e => e.MsPlayed > 0)
            .GroupBy(e => e.PlayedAtUtc.Date)
            .Select(g => new { Date = g.Key, Minutes = g.Sum(e => e.MinutesPlayed) })
            .OrderByDescending(g => g.Minutes)
            .FirstOrDefault();

        return new ListeningHabitsDto(
            SkipRatePercent: skipRate,
            LongestStreakDays: longestStreak,
            MostActiveDayUtc: mostActiveDay?.Date,
            MostActiveDayMinutes: mostActiveDay is null ? 0 : Math.Round(mostActiveDay.Minutes, 1)
        );
    }

    public async Task<DatasetRangeDto?> GetDatasetRangeAsync(CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(ct);
        var tracks = all.Where(e => e.ContentType == PlayContentType.Track && e.MsPlayed > 0).ToList();

        if (tracks.Count == 0)
        {
            return null;
        }

        return new DatasetRangeDto(
            EarliestUtc: tracks.Min(e => e.PlayedAtUtc),
            LatestUtc: tracks.Max(e => e.PlayedAtUtc),
            TotalTrackEvents: tracks.Count
        );
    }

    // --- Shared helpers -----------------------------------------------------

    /// <summary>
    /// Resolves the query params into a concrete date window. An explicit from/to wins.
    /// Otherwise "last N months" is anchored to the most recent event currently loaded
    /// (falling back to UtcNow only when there is no data at all yet) - exported Spotify
    /// history is historical, so anchoring to today's date would silently return nothing.
    /// </summary>
    private async Task<DateRangeFilter> ResolveRangeAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue)
        {
            return new DateRangeFilter(
                DateTime.SpecifyKind(from.Value, DateTimeKind.Utc),
                DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
        }

        var datasetRange = await GetDatasetRangeAsync(ct);
        var anchor = datasetRange?.LatestUtc ?? DateTime.UtcNow;
        var start = anchor.AddMonths(-Math.Clamp(months ?? 6, 1, 12));

        return new DateRangeFilter(start, anchor);
    }

    /// <summary>Loads events, keeps only real music-track plays (skips podcasts/audiobooks
    /// and "phantom" 0ms plays), and applies the requested date range.</summary>
    private async Task<(DateRangeFilter Range, List<PlayEvent> Tracks)> LoadTracksAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var range = await ResolveRangeAsync(months, from, to, ct);
        var all = await _repository.GetAllAsync(ct);

        var tracks = all
            .Where(e => e.ContentType == PlayContentType.Track)
            .Where(e => e.ArtistName is not null && e.TrackName is not null)
            .Where(e => e.MsPlayed > 0)
            .Where(e => e.PlayedAtUtc >= range.FromUtc && e.PlayedAtUtc <= range.ToUtc)
            .ToList();

        return (range, tracks);
    }

    private static int ComputeLongestStreak(IReadOnlyList<DateTime> sortedDistinctDays)
    {
        if (sortedDistinctDays.Count == 0) return 0;

        var longest = 1;
        var current = 1;

        for (var i = 1; i < sortedDistinctDays.Count; i++)
        {
            if ((sortedDistinctDays[i] - sortedDistinctDays[i - 1]).Days == 1)
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    private static string NormalizePlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return "Unknown";

        var lower = platform.ToLowerInvariant();
        if (lower.Contains("android")) return "Android";
        if (lower.Contains("ios") || lower.Contains("iphone") || lower.Contains("ipad")) return "iOS";
        if (lower.Contains("windows")) return "Windows";
        if (lower.Contains("osx") || lower.Contains("mac")) return "macOS";
        if (lower.Contains("web")) return "Web Player";
        if (lower.Contains("linux")) return "Linux";
        return platform;
    }

    private static string FormatContentType(PlayContentType type) => type switch
    {
        PlayContentType.Track => "Music",
        PlayContentType.PodcastEpisode => "Podcasts",
        PlayContentType.AudiobookChapter => "Audiobooks",
        _ => type.ToString()
    };
}
