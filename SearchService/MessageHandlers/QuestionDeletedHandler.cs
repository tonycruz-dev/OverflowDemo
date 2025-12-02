using Contracts;
using SearchService.Models;
using Typesense;

namespace QuestionService.MessageHandlers;

public class QuestionDeletedHandler(ITypesenseClient client)
{
    public async Task Handle(QuestionDeleted message)
    {
        await client.DeleteDocument<SearchQuestion>("questions", message.QuestionId);
    }
}
