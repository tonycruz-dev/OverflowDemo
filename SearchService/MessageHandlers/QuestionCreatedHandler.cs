using Contracts;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;

namespace QuestionService.MessageHandlers;

public class QuestionCreatedHandler(ITypesenseClient client)
{
    public async Task Handle(QuestionCreated message)
    {
        var created = new DateTimeOffset(message.Created).ToUnixTimeSeconds();

        var doc = new SearchQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            CreatedAt = created,
            Tags = [.. message.Tags],
        };
        await client.CreateDocument("questions", doc);

        Console.WriteLine($"Created question with id {message.QuestionId}");
    }

    private static string StripHtml(string content)
    {
        return Regex.Replace(content, "<.*?>", string.Empty);
    }
}
