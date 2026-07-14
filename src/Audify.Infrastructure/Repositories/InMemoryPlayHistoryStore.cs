using Audify.Application.Interfaces;
using Audify.Domain.Entities;

namespace Audify.Infrastructure.Repositories;

/// <summary>
/// Holds the currently loaded play history in memory. Registered as a singleton,
/// so the data lives for the lifetime of the running app (it resets if you
/// restart the server, or you can hit /api/upload/clear to reset it manually).
///
/// This is intentionally simple for the mockup stage - a single shared dataset,
/// no per-user isolation. Once Spotify login is added, this can either become
/// per-user (keyed by a session/user id) or be replaced entirely by a repository
/// that talks to the Spotify Web API - StatsService won't need to change either way,
/// since it only depends on IPlayHistoryRepository.
/// </summary>
public class InMemoryPlayHistoryStore : IPlayHistoryStore
{
    private readonly object _lock = new();
    private List<PlayEvent> _events = new();

    public bool HasData
    {
        get { lock (_lock) { return _events.Count > 0; } }
    }

    public Task<IReadOnlyList<PlayEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Return a snapshot copy so callers can't mutate our internal list.
            return Task.FromResult<IReadOnlyList<PlayEvent>>(_events.ToList());
        }
    }

    public Task ReplaceAllAsync(IReadOnlyList<PlayEvent> events, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events = events.ToList();
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events = new List<PlayEvent>();
        }
        return Task.CompletedTask;
    }
}
