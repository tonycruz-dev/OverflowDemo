using Contracts;
using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using StatsService.Models;

namespace StatsService.Projections;

public class TopUsersProjection : EventProjection
{
    public TopUsersProjection() => ProjectAsync<IEvent<UserReputationChanged>>(Apply);

    private static async Task Apply(IEvent<UserReputationChanged> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(ev.Timestamp.UtcDateTime);

        var data = ev.Data;
        var id = $"{data.UserId}:{day:yyyy-MM-dd}";

        var doc = await ops.LoadAsync<UserDailyReputation>(id, ct)
                  ?? new UserDailyReputation
                  {
                      Id = id,
                      UserId = data.UserId,
                      Date = day,
                      Delta = 0
                  };

        doc.Delta += data.Delta;
        ops.Store(doc);
    }
}


public class TopAIProjection : EventProjection
{
    public TopAIProjection() => ProjectAsync<IEvent<AiReputationChanged>>(Apply);

    private static async Task Apply(IEvent<AiReputationChanged> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(ev.Timestamp.UtcDateTime);

        var data = ev.Data;
        var id = $"{data.AiId}:{day:yyyy-MM-dd}";

        var doc = await ops.LoadAsync<AIDailyReputation>(id, ct)
                  ?? new AIDailyReputation
                  {
                      Id = id,
                      AiId = data.AiId,
                      Date = day,
                      Delta = 0
                  };

        doc.Delta += data.Delta;
        ops.Store(doc);
    }
}
