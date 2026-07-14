using Audify.Domain.Entities;

namespace Audify.Application.Interfaces;

/// <summary>
/// Read-only source of truth for a listener's play history. This is all
/// StatsService ever talks to - it has no idea whether the data behind it
/// came from an uploaded file, disk, or (in the future) the Spotify Web API.
/// </summary>
public interface IPlayHistoryRepository
{
    /// <summary>Returns every currently loaded play event.</summary>
    Task<IReadOnlyList<PlayEvent>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>True once at least one dataset has been loaded (e.g. via upload).</summary>
    bool HasData { get; }
}
