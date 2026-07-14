using Audify.Application.Interfaces;
using Audify.Application.Services;
using Audify.Infrastructure.Parsing;
using Audify.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- Services -----------------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// One shared in-memory dataset, populated by drag-and-drop uploads.
// Registered as both interfaces so the upload endpoint can write to it
// (IPlayHistoryStore) while StatsService can only ever read from it
// (IPlayHistoryRepository) - see the interfaces themselves for why that split exists.
builder.Services.AddSingleton<InMemoryPlayHistoryStore>();
builder.Services.AddSingleton<IPlayHistoryStore>(sp => sp.GetRequiredService<InMemoryPlayHistoryStore>());
builder.Services.AddSingleton<IPlayHistoryRepository>(sp => sp.GetRequiredService<InMemoryPlayHistoryStore>());

builder.Services.AddSingleton<ISpotifyHistoryParser, SpotifyHistoryJsonParser>();
builder.Services.AddScoped<IStatsService, StatsService>();

// Uploaded exports can be large (years of history) - raise the multipart body limit.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200 * 1024 * 1024; // 200 MB
});

var app = builder.Build();

// --- Middleware pipeline --------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();   // serves wwwroot/index.html at "/"
app.UseStaticFiles();    // serves wwwroot/css, wwwroot/js, etc.

app.UseAuthorization();

app.MapControllers();

app.Run();
