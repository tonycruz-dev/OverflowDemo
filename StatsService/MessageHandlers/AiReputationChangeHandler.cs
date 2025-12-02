using Contracts;
using Marten;
using Wolverine.Attributes;

namespace StatsService.MessageHandlers;

public class AiReputationChangeHandler
{
    [Transactional]
    public static async Task Handle(AiReputationChanged message, IDocumentSession session)
    {
        session.Events.Append(message.AiId, message);
        await session.SaveChangesAsync();
    }
}
