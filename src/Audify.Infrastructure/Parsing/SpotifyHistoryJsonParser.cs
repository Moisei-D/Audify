using System.Text.Json;
using Audify.Application.Interfaces;
using Audify.Domain.Entities;
using Audify.Infrastructure.Spotify;

namespace Audify.Infrastructure.Parsing;

/// <summary>
/// Deserializes one Spotify extended-streaming-history JSON file (the shape of
/// Streaming_History_Audio_*.json / Streaming_History_Video_*.json) and maps each
/// raw entry into a domain PlayEvent, classifying it as a track, podcast episode
/// or audiobook chapter along the way.
/// </summary>
public class SpotifyHistoryJsonParser : ISpotifyHistoryParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<PlayEvent>> ParseAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        var rawEntries = await JsonSerializer.DeserializeAsync<List<SpotifyRawHistoryEntry>>(
            jsonStream, JsonOptions, cancellationToken) ?? new List<SpotifyRawHistoryEntry>();

        return rawEntries.Select(MapToPlayEvent).ToList();
    }

    private static PlayEvent MapToPlayEvent(SpotifyRawHistoryEntry raw)
    {
        var contentType = raw.AudiobookTitle is not null
            ? PlayContentType.AudiobookChapter
            : raw.EpisodeName is not null
                ? PlayContentType.PodcastEpisode
                : PlayContentType.Track;

        return new PlayEvent
        {
            PlayedAtUtc = DateTime.SpecifyKind(raw.Timestamp, DateTimeKind.Utc),
            MsPlayed = raw.MsPlayed,
            TrackName = raw.TrackName,
            ArtistName = raw.ArtistName,
            AlbumName = raw.AlbumName,
            SpotifyUri = raw.SpotifyTrackUri,
            Platform = raw.Platform,
            Skipped = raw.Skipped ?? false,
            ContentType = contentType
        };
    }
}
