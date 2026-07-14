using Audify.Domain.Entities;

namespace Audify.Application.Interfaces;

/// <summary>
/// Turns a raw uploaded Spotify export file into domain PlayEvents.
/// Implemented in Infrastructure (see SpotifyHistoryJsonParser) since it knows
/// about Spotify's specific JSON shape - the Application layer just needs "a stream in, events out".
/// </summary>
public interface ISpotifyHistoryParser
{
    Task<IReadOnlyList<PlayEvent>> ParseAsync(Stream jsonStream, CancellationToken cancellationToken = default);
}
