namespace Audify.Domain.Entities;

/// <summary>
/// Represents a single "play" of a track, podcast episode or audiobook chapter,
/// normalised from Spotify's raw extended streaming history export.
/// This is the core unit that every statistic in Audify is built from.
/// </summary>
public class PlayEvent
{
    /// <summary>When playback started (UTC).</summary>
    public DateTime PlayedAtUtc { get; init; }

    /// <summary>How long the item was actually played, in milliseconds.</summary>
    public long MsPlayed { get; init; }

    /// <summary>Track title. Null for podcast/audiobook entries.</summary>
    public string? TrackName { get; init; }

    /// <summary>Artist name. Null for podcast/audiobook entries.</summary>
    public string? ArtistName { get; init; }

    /// <summary>Album name. Null for podcast/audiobook entries.</summary>
    public string? AlbumName { get; init; }

    /// <summary>Spotify URI, used as a stable identifier for grouping (e.g. spotify:track:xyz).</summary>
    public string? SpotifyUri { get; init; }

    /// <summary>Device platform the event was recorded on (android, ios, web_player, etc.).</summary>
    public string? Platform { get; init; }

    /// <summary>True if the user skipped the item before it finished.</summary>
    public bool Skipped { get; init; }

    /// <summary>The content type this event represents.</summary>
    public PlayContentType ContentType { get; init; } = PlayContentType.Track;

    /// <summary>Convenience helper used throughout the stats layer.</summary>
    public double MinutesPlayed => MsPlayed / 60000.0;
}

/// <summary>
/// Spotify's export mixes music, podcasts and audiobooks in the same files.
/// Audify only turns music into stats today, but we keep the other types
/// around so future features (e.g. "top podcasts") don't require re-parsing.
/// </summary>
public enum PlayContentType
{
    Track,
    PodcastEpisode,
    AudiobookChapter
}
