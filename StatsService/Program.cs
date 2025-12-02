using Common;
using Contracts;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using StatsService.Extensions;
using StatsService.Models;
using StatsService.Projections;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    // Bind a queue to the shared 'questions' exchange so this service receives events
    opts.ListenToRabbitQueue("questions.stats", cfg =>
    {
        cfg.BindExchange("questions");
    });

    opts.Policies.AutoApplyTransactions();
    opts.ApplicationAssembly = typeof(Program).Assembly;
});

var connString = builder.Configuration.GetConnectionString("statDb")!;
await connString.EnsurePostgresDatabaseExistsAsync();

builder.Services.AddMarten(opts =>
{
    opts.Connection(connString);

    opts.Events.StreamIdentity = StreamIdentity.AsString;
    opts.Events.AddEventType<QuestionCreated>();
    opts.Events.AddEventType<UserReputationChanged>();
    opts.Events.AddEventType<AiReputationChanged>();

    opts.Schema.For<TagDailyUsage>()
        .Index(x => x.Tag)
        .Index(x => x.Date);

    opts.Schema.For<UserReputationChanged>()
        .Index(x => x.UserId)
        .Index(x => x.Occurred);

    opts.Schema.For<AiReputationChanged>()  // adding index for AiReputationChanged events
        .Index(x => x.AiId)
        .Index(x => x.Occurred);

    opts.Projections.Add(new TrendingTagsProjection(), ProjectionLifecycle.Inline);
    opts.Projections.Add(new TopUsersProjection(), ProjectionLifecycle.Inline);
    opts.Projections.Add(new TopAIProjection(), ProjectionLifecycle.Inline); // registering the TopAIProjection

}).UseLightweightSessions();



var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/stats/trending-tags", async (IQuerySession session) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var start = today.AddDays(-6);

    var rows = await session.Query<TagDailyUsage>()
        .Where(x => x.Date >= start && x.Date <= today)
        .Select(x => new { x.Tag, x.Count })
        .ToListAsync();

    var top = rows
        .GroupBy(x => x.Tag)
        .Select(x => new { tag = x.Key, count = x.Sum(t => t.Count) })
        .OrderByDescending(x => x.count)
        .Take(5)
        .ToList();

    return Results.Ok(top);
});

app.MapGet("/stats/top-users", async (IQuerySession session) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var start = today.AddDays(-6);

    var rows = await session.Query<UserDailyReputation>()
        .Where(x => x.Date >= start && x.Date <= today)
        .Select(x => new { x.UserId, x.Delta })
        .ToListAsync();

    var top = rows.GroupBy(x => x.UserId)
        .Select(g => new { userId = g.Key, delta = g.Sum(t => t.Delta) })
        .OrderByDescending(x => x.delta)
        .Take(5)
        .ToList();

    return Results.Ok(top);
});

// New endpoint for top AIs
app.MapGet("/stats/top-ais", async (IQuerySession session) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var start = today.AddDays(-6);
    var rows = await session.Query<AIDailyReputation>()
        .Where(x => x.Date >= start && x.Date <= today)
        .Select(x => new { x.AiId, x.Delta })
        .ToListAsync();

    var top = rows.GroupBy(x => x.AiId)
        .Select(g => new { aiId = g.Key, delta = g.Sum(t => t.Delta) })
        .OrderByDescending(x => x.delta)
        .Take(5)
        .ToList();
    return Results.Ok(top);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
