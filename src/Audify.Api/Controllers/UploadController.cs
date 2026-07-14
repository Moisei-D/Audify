using Audify.Application.DTOs;
using Audify.Application.Interfaces;
using Audify.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Audify.Api.Controllers;

/// <summary>
/// Handles the drag-and-drop upload of Spotify's exported JSON files.
/// Every upload fully replaces the previously loaded dataset (drop all your
/// Streaming_History_*.json files at once to combine them).
/// </summary>
[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private readonly ISpotifyHistoryParser _parser;
    private readonly IPlayHistoryStore _store;

    public UploadController(ISpotifyHistoryParser parser, IPlayHistoryStore store)
    {
        _parser = parser;
        _store = store;
    }

    /// <summary>Accepts one or more Spotify export *.json files and loads them into memory.</summary>
    [HttpPost]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload(CancellationToken ct)
    {
        var files = Request.Form.Files;

        if (files.Count == 0)
        {
            return BadRequest("No files were received.");
        }

        var allEvents = new List<PlayEvent>();

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue; // silently skip anything that isn't a json export
            }

            await using var stream = file.OpenReadStream();
            var events = await _parser.ParseAsync(stream, ct);
            allEvents.AddRange(events);
        }

        if (allEvents.Count == 0)
        {
            return BadRequest("None of the uploaded files could be parsed as Spotify history JSON.");
        }

        await _store.ReplaceAllAsync(allEvents, ct);

        var trackEvents = allEvents.Where(e => e.ContentType == PlayContentType.Track).ToList();

        return Ok(new UploadResultDto(
            FilesProcessed: files.Count,
            EventsLoaded: allEvents.Count,
            TrackEventsLoaded: trackEvents.Count,
            EarliestEventUtc: allEvents.Count > 0 ? allEvents.Min(e => e.PlayedAtUtc) : null,
            LatestEventUtc: allEvents.Count > 0 ? allEvents.Max(e => e.PlayedAtUtc) : null
        ));
    }

    /// <summary>Lets the frontend check on load whether data is already sitting in memory.</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        return Ok(new { hasData = _store.HasData });
    }

    /// <summary>Clears the loaded dataset so the user can start over with different files.</summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _store.ClearAsync(ct);
        return NoContent();
    }
}
