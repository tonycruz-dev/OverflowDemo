using Contracts;
using Typesense;

namespace QuestionService.MessageHandlers;

public class AnswerCountUpdatedHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerCountUpdated message)
    {
        await client.UpdateDocument("questions", message.QuestionId,
            new { message.AnswerCount }
        );
    }
}