# Audify - Spotify Stats Dashboard (mockup v3 - drag & drop, expanded stats)

An ASP.NET Core Web API + small JS dashboard that lets you **drag and drop** your
exported Spotify streaming history straight into the browser and see stats
immediately - no config, no files to place on disk beforehand.

## What's in this version

- **Fixed an important bug**: "last N months" now anchors to the most recent
  play in *your uploaded data*, not to today's real-world date. Exported Spotify
  history can be years old, so filtering against "now" was silently showing
  nothing. The dashboard now defaults to **All time** on first load for exactly
  this reason, with quick presets (4 weeks / 6 months / 12 months / all time) and
  the slider/custom range still available underneath.
- A broader set of stats, similar in spirit to sites like statsforspotify.com / stats.fm:
  - Top artists, tracks, albums (as before)
  - Monthly listening trend
  - **Listening by day of week** and **listening clock (hour of day)**
  - **Platform breakdown** (Android / iOS / Web Player / etc.)
  - **Music vs podcasts vs audiobooks** breakdown
  - **Skip rate**, **longest daily listening streak**, and **most active single day**

## How it works

1. You drop your `Streaming_History_*.json` file(s) onto the page.
2. The browser uploads them to `POST /api/upload`.
3. The server parses them and holds the result **in memory** for the lifetime of
   the running app (see "Why in-memory" below).
4. The dashboard fetches the dataset's real date span (`GET /api/stats/data-range`),
   defaults to showing all of it, and lets you narrow down from there.

## Project structure

```
Audify/
├── Audify.sln
├── src/
│   ├── Audify.Domain/            # Entities only. No dependencies on anything else.
│   │   └── Entities/PlayEvent.cs
│   │
│   ├── Audify.Application/       # Business logic + contracts (the "core" of the app).
│   │   ├── DTOs/                 # Shapes returned by the API / consumed by the frontend.
│   │   ├── Interfaces/
│   │   │   ├── IPlayHistoryRepository.cs   # read-only: used by StatsService
│   │   │   ├── IPlayHistoryStore.cs        # read+write: used by the upload endpoint
│   │   │   ├── ISpotifyHistoryParser.cs    # turns a JSON stream into PlayEvents
│   │   │   └── IStatsService.cs
│   │   └── Services/StatsService.cs        # all the aggregation logic, incl. date anchoring
│   │
│   ├── Audify.Infrastructure/    # Implementations of Application's interfaces.
│   │   ├── Spotify/SpotifyRawModels.cs         # raw JSON shape from Spotify's export
│   │   ├── Parsing/SpotifyHistoryJsonParser.cs # JSON -> PlayEvent mapping
│   │   └── Repositories/InMemoryPlayHistoryStore.cs
│   │
│   └── Audify.Api/               # Composition root: ASP.NET Core host.
│       ├── Controllers/
│       │   ├── UploadController.cs   # POST /api/upload, /api/upload/status, /api/upload/clear
│       │   └── StatsController.cs    # summary, top-*, trend, by-day-of-week, by-hour,
│       │                             # platforms, content-types, habits, data-range
│       ├── wwwroot/                  # The dashboard itself (index.html, css, js)
│       └── Program.cs
```

**Clean Architecture**: dependencies only point inward
(`Api -> Infrastructure -> Application -> Domain`). `StatsService` only depends on
`IPlayHistoryRepository` - it has no idea the data came from an upload. That's what
lets the data source change later (see Roadmap) without touching the stats logic.


## How to run it

1. Open `Audify.sln` in Visual Studio 2022+ (ASP.NET and web development workload).
2. **Audify.Api** is already the startup project.
3. Press **F5**. Your browser opens to the drag-and-drop screen.
4. Download your data from [Spotify's privacy page](https://www.spotify.com/account/privacy/)
   ("Extended streaming history"), then drag the `Streaming_History_Audio_*.json`
   (and/or `Streaming_History_Video_*.json`) files onto the dropzone - you can drop
   several at once and they'll be combined.
5. The dashboard appears automatically once the upload finishes.

Swagger UI is available at `/swagger` in Development mode if you want to poke at
the API directly (e.g. to test `/api/upload` with a tool like Postman).

## Why in-memory (and not saved to disk / a database)?

For this stage, simplicity: no database to set up, nothing written to your disk
that you didn't explicitly export yourself. The trade-off is that the data is
lost if you restart the server, and it's a single shared dataset rather than
per-user - both totally fine for a local mockup, and things step 2 below will
naturally replace anyway.

## Why no genre stats yet?

Spotify's personal data export does **not** include genre - genre is a property of
the *artist*, not the play event, and Spotify only exposes it through the Web API.
There's a placeholder card in the UI for it; it'll be filled in once Spotify login
(below) is wired up.

## Roadmap: Spotify login without a separate user system

The architecture is already set up for this:

1. **Use Spotify itself as the identity provider.** Configure ASP.NET Core's OAuth
   handler for Spotify's Authorization Code flow (`accounts.spotify.com/authorize`).
   No user table or passwords - Spotify's access/refresh tokens *are* the session,
   stored in an encrypted auth cookie (`AddCookie()` + `AddOAuth("Spotify", ...)`).
2. **Add a second `IPlayHistoryRepository` implementation**, e.g.
   `SpotifyApiPlayHistoryRepository`, that calls the Spotify Web API
   (`/me/player/recently-played`, `/me/top/tracks`, `/artists/{id}` for genres) using
   the logged-in user's token instead of relying on an uploaded file.
3. **Swap it in for logged-in users** while keeping the upload flow as a fallback
   for anyone who doesn't want to log in. Because `StatsService` and
   `StatsController` only depend on `IPlayHistoryRepository`, none of that code
   needs to change.
4. This also unlocks the genre stat, since artist genres come from the same API.

## Roadmap: CI/CD

Not set up yet, as requested - when you're ready, this project structure works
well with a simple GitHub Actions workflow (`dotnet build` + `dotnet test` +
`dotnet publish`) since every layer is a normal class library/Web SDK project.
Happy to help with that when you get there.
