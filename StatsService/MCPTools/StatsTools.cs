using Marten;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using StatsService.Models;
using System.ComponentModel;

namespace StatsService.MCPTools;

// Small DTOs for MCP responses
public sealed record TrendingTagDto(string Tag, int Count);
public sealed record TopUserDto(string UserId, int Delta);
public sealed record TopAiDto(string AiId, int Delta);

[McpServerToolType]
public class StatsTools(IQuerySession session)
{
    // 🔹 Trending tags (last 7 days)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the top 5 trending tags over the last 7 days, ranked by total usage count.")]
    public async Task<IReadOnlyList<TrendingTagDto>> GetTrendingTags()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-6);

        // Fix: Disambiguate ToListAsync by specifying the static class
        var rows = await Marten.QueryableExtensions.ToListAsync(
            session.Query<TagDailyUsage>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.Tag, x.Count })
        );

        var top = rows
            .GroupBy(x => x.Tag)
            .Select(g => new TrendingTagDto(
                Tag: g.Key,
                Count: g.Sum(t => t.Count)))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        return top;
    }

    // 🔹 Top users by reputation delta (last 7 days)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the top 5 users over the last 7 days, ranked by net reputation gained.")]
    public async Task<IReadOnlyList<TopUserDto>> GetTopUsers()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-6);

        var userRows = await Marten.QueryableExtensions.ToListAsync(
            session.Query<UserDailyReputation>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.UserId, x.Delta })
        );

        var top = userRows
            .GroupBy(x => x.UserId)
            .Select(g => new TopUserDto(
                UserId: g.Key,
                Delta: g.Sum(t => t.Delta)))
            .OrderByDescending(x => x.Delta)
            .Take(5)
            .ToList();

        return top;
    }

    // 🔹 Top AIs by reputation delta (last 7 days)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the top 5 AI agents over the last 7 days, ranked by net reputation impact.")]
    public async Task<IReadOnlyList<TopAiDto>> GetTopAis()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-6);

        var aiRows = await Marten.QueryableExtensions.ToListAsync(
            session.Query<AIDailyReputation>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.AiId, x.Delta })
        );

        var top = aiRows
            .GroupBy(x => x.AiId)
            .Select(g => new TopAiDto(
                AiId: g.Key,
                Delta: g.Sum(t => t.Delta)))
            .OrderByDescending(x => x.Delta)
            .Take(5)
            .ToList();

        return top;
    }
}
