using Contracts;
using Typesense;

namespace QuestionService.MessageHandlers;

public class AcceptAnswerHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerAccepted message)
    {
        await client.UpdateDocument("questions", message.QuestionId,
            new { HasAcceptedAnswer = true });
    }
}
