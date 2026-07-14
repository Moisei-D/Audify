using Audify.Application.DTOs;

namespace Audify.Application.Interfaces;

/// <summary>
/// All the read-only queries the Audify dashboard needs. Controllers should only
/// ever talk to this interface - never to the repository directly - so the
/// "how stats are calculated" logic lives in one, testable place.
///
/// Every query takes the same three optional filter params: <paramref name="months"/>
/// (slider), or an explicit <paramref name="from"/>/<paramref name="to"/> (custom range /
/// "all time"). When only <c>months</c> is given, the window is anchored to the most
/// recent event in the dataset - NOT to today's real-world date - since exported
/// Spotify history is historical data that may be years old.
/// </summary>
public interface IStatsService
{
    Task<ListeningSummaryDto> GetSummaryAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<TopArtistDto>> GetTopArtistsAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<TopTrackDto>> GetTopTracksAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<TopAlbumDto>> GetTopAlbumsAsync(int? months, DateTime? from, DateTime? to, int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<MonthlyListeningDto>> GetListeningTrendAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<DayOfWeekStatDto>> GetDayOfWeekBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<HourOfDayStatDto>> GetHourOfDayBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<PlatformStatDto>> GetPlatformBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<ContentTypeStatDto>> GetContentTypeBreakdownAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<ListeningHabitsDto> GetListeningHabitsAsync(int? months, DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>The full date span of whatever is currently loaded - used to size the UI's filters.</summary>
    Task<DatasetRangeDto?> GetDatasetRangeAsync(CancellationToken ct = default);
}
