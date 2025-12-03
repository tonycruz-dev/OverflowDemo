using Common;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Reputation;
using System.Security.Claims;
using VoteService.Data;
using VoteService.DTOs;
using VoteService.Models;
using Wolverine;
using Scalar.AspNetCore; // Added for Scalar API Explorer

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// OpenAPI document generation for Scalar explorer
builder.Services.AddOpenApi();

builder.Services.AddKeyCloakAuthentication();
await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    opts.ApplicationAssembly = typeof(Program).Assembly;
});
builder.AddNpgsqlDbContext<VoteDbContext>("voteDb");

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();


var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Expose the OpenAPI document (e.g. /openapi/v1.json)
    app.MapOpenApi();
    //app.MapScalarApi("/scalar");
}

// Map Scalar API Explorer UI at /scalar (requires auth)
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Vote Service API")
        .WithTheme(ScalarTheme.Default); // Optional theme
});

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
.WithName("GetWeatherForecast")
.WithOpenApi(); // Include in OpenAPI doc

app.MapPost("/votes", async (CastVoteDto dto, VoteDbContext db, ClaimsPrincipal user, IMessageBus bus) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    if (dto.TargetType is not ("Question" or "Answer"))
        return Results.BadRequest("Invalid target type");

    var alreadyVoted = await db.Votes.AnyAsync(x => x.UserId == userId && x.TargetId == dto.TargetId);

    if (alreadyVoted) return Results.BadRequest("Already voted");

    db.Votes.Add(new Vote
    {
        TargetId = dto.TargetId,
        TargetType = dto.TargetType,
        UserId = userId,
        VoteValue = dto.VoteValue,
        QuestionId = dto.QuestionId
    });

    await db.SaveChangesAsync();

    var reason = (dto.VoteValue, dto.TargetType) switch
    {
        (1, "Question") => ReputationReason.QuestionUpvoted,
        (1, "Answer") => ReputationReason.AnswerUpvoted,
        (-1, "Answer") => ReputationReason.AnswerDownvoted,
        _ => ReputationReason.QuestionDownvoted
    };

    await bus.PublishAsync(ReputationHelper.MakeEvent(dto.TargetUserId, reason, userId));
    await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));

    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

app.MapGet("/votes/{questionId}", async (string questionId, VoteDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var votes = await db.Votes
        .Where(x => x.UserId == userId && x.QuestionId == questionId)
        .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
        .ToListAsync();

    return Results.Ok(votes);
}).RequireAuthorization().WithOpenApi();

app.MapPost("/votesai", async (CastVoteAiDto dto, VoteDbContext db, ClaimsPrincipal user, IMessageBus bus) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    if (dto.TargetType is not ("Question" or "Answer"))
        return Results.BadRequest("Invalid target type");

    var alreadyVoted = await db.VoteAIs.AnyAsync(x => x.AiId == dto.AiId && x.TargetId == dto.TargetId && x.UserId ==userId);

    if (alreadyVoted) return Results.BadRequest("Already voted");

    db.VoteAIs.Add(new VoteAI
    {
        TargetId = dto.TargetId,
        TargetType = dto.TargetType,
        AiId = dto.AiId,
        UserId = userId,
        VoteValue = dto.VoteValue,
        QuestionId = dto.QuestionId
    });

    await db.SaveChangesAsync();

    var reason = (dto.VoteValue, dto.TargetType) switch
    {
        (1, "Question") => ReputationReason.QuestionUpvoted,
        (1, "Answer") => ReputationReason.AnswerUpvoted,
        (-1, "Answer") => ReputationReason.AnswerDownvoted,
        _ => ReputationReason.QuestionDownvoted
    };

    await bus.PublishAsync(ReputationHelper.MakeEventAi(dto.AiId, reason, userId));
    await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));

    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

app.MapGet("/votesai/{questionId}/{aiId}", async (string questionId,string aiId, VoteDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var votes = await db.VoteAIs
        .Where(x => x.AiId == aiId && x.QuestionId == questionId)
        .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
        .ToListAsync();

    return Results.Ok(votes);
}).RequireAuthorization().WithOpenApi();

app.MapGet("/votesais/{questionId}", async (string questionId, VoteDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var votes = await db.VoteAIs
        .Where(x => x.QuestionId == questionId)
        .Select(x => new UserVotesAiResult(x.AiId,x.QuestionId, x.UserId, x.TargetId, x.TargetType, x.VoteValue))
        .ToListAsync();

    return Results.Ok(votes);
}).RequireAuthorization().WithOpenApi();


await app.MigrateDbContextAsync<VoteDbContext>();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
