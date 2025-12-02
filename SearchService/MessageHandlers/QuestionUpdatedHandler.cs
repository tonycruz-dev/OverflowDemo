using Contracts;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;

namespace QuestionService.MessageHandlers;

public class QuestionUpdatedHandler(ITypesenseClient client)
{
    public async Task Handle(QuestionUpdated message)
    {
        var doc = new SearchQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            Tags = message.Tags.ToArray(),
        };
        await client.UpdateDocument("questions", doc.Id, doc);
    }

    private static string StripHtml(string content)
    {
        return Regex.Replace(content, "<.*?>", string.Empty);
    }
}