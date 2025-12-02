namespace StatsService.Models;

public class AIDailyReputation
{
    public required string Id { get; set; }
    public required string AiId { get; set; }
    public DateOnly Date { get; set; }
    public int Delta { get; set; }
}
