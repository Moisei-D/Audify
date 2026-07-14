using Audify.Application.DTOs;
using Audify.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Audify.Api.Controllers;

/// <summary>
/// Read-only endpoints backing the Audify dashboard. Every endpoint accepts either
/// a "months" query param (1-12, powers the slider) or explicit "from"/"to" dates
/// (powers the custom date-range picker and the "all time" view) - the two are
/// mutually exclusive, and "months" is always anchored to the dataset's most
/// recent event rather than to today's date (see StatsService for why).
/// </summary>
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("data-range")]
    public async Task<ActionResult<DatasetRangeDto>> GetDataRange(CancellationToken ct)
    {
        var range = await _statsService.GetDatasetRangeAsync(ct);
        return range is null ? NotFound() : Ok(range);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ListeningSummaryDto>> GetSummary(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetSummaryAsync(months, from, to, ct));

    [HttpGet("top-artists")]
    public async Task<ActionResult<IReadOnlyList<TopArtistDto>>> GetTopArtists(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int take = 10, CancellationToken ct = default)
        => Ok(await _statsService.GetTopArtistsAsync(months, from, to, take, ct));

    [HttpGet("top-tracks")]
    public async Task<ActionResult<IReadOnlyList<TopTrackDto>>> GetTopTracks(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int take = 10, CancellationToken ct = default)
        => Ok(await _statsService.GetTopTracksAsync(months, from, to, take, ct));

    [HttpGet("top-albums")]
    public async Task<ActionResult<IReadOnlyList<TopAlbumDto>>> GetTopAlbums(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int take = 10, CancellationToken ct = default)
        => Ok(await _statsService.GetTopAlbumsAsync(months, from, to, take, ct));

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<MonthlyListeningDto>>> GetTrend(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetListeningTrendAsync(months, from, to, ct));

    [HttpGet("by-day-of-week")]
    public async Task<ActionResult<IReadOnlyList<DayOfWeekStatDto>>> GetByDayOfWeek(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetDayOfWeekBreakdownAsync(months, from, to, ct));

    [HttpGet("by-hour")]
    public async Task<ActionResult<IReadOnlyList<HourOfDayStatDto>>> GetByHour(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetHourOfDayBreakdownAsync(months, from, to, ct));

    [HttpGet("platforms")]
    public async Task<ActionResult<IReadOnlyList<PlatformStatDto>>> GetPlatforms(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetPlatformBreakdownAsync(months, from, to, ct));

    [HttpGet("content-types")]
    public async Task<ActionResult<IReadOnlyList<ContentTypeStatDto>>> GetContentTypes(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetContentTypeBreakdownAsync(months, from, to, ct));

    [HttpGet("habits")]
    public async Task<ActionResult<ListeningHabitsDto>> GetHabits(
        [FromQuery] int? months, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _statsService.GetListeningHabitsAsync(months, from, to, ct));
}
