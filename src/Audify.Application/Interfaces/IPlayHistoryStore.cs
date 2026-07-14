using Audify.Domain.Entities;

namespace Audify.Application.Interfaces;

/// <summary>
/// Write side of the play history store, used only by the upload endpoint.
/// Kept separate from <see cref="IPlayHistoryRepository"/> so that StatsService
/// (which should only ever read) can't accidentally mutate the dataset -
/// it depends on IPlayHistoryRepository only.
/// </summary>
public interface IPlayHistoryStore : IPlayHistoryRepository
{
    /// <summary>Replaces the entire in-memory dataset with the given events (used on every upload).</summary>
    Task ReplaceAllAsync(IReadOnlyList<PlayEvent> events, CancellationToken cancellationToken = default);

    /// <summary>Clears any loaded data, returning the app to its "nothing uploaded yet" state.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
