using System.Text.Json.Serialization;

namespace Audify.Infrastructure.Spotify;

/// <summary>
/// One raw entry exactly as it appears in Spotify's "Extended streaming history" export
/// (files named Streaming_History_Audio_*.json or Streaming_History_Video_*.json).
/// Field names are kept close to Spotify's own snake_case naming for easy mapping,
/// with [JsonPropertyName] doing the actual translation.
/// This class is intentionally "dumb" - it only exists to deserialize JSON. All real
/// logic happens after it is mapped into the Domain's PlayEvent.
/// </summary>
public class SpotifyRawHistoryEntry
{
    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("ms_played")]
    public long MsPlayed { get; set; }

    [JsonPropertyName("master_metadata_track_name")]
    public string? TrackName { get; set; }

    [JsonPropertyName("master_metadata_album_artist_name")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("master_metadata_album_album_name")]
    public string? AlbumName { get; set; }

    [JsonPropertyName("spotify_track_uri")]
    public string? SpotifyTrackUri { get; set; }

    [JsonPropertyName("episode_name")]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("episode_show_name")]
    public string? EpisodeShowName { get; set; }

    [JsonPropertyName("audiobook_title")]
    public string? AudiobookTitle { get; set; }

    [JsonPropertyName("audiobook_chapter_title")]
    public string? AudiobookChapterTitle { get; set; }

    [JsonPropertyName("skipped")]
    public bool? Skipped { get; set; }
}
