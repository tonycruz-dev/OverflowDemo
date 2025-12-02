using QuestionService.DTOs;

namespace QuestionService.Clients.AI
{
    public interface IGeminiModelsClient
    {
        Task<CreateAiAnswerDto?> GEMINIModelsAnswerCodeErrorAsync(string title, string problemStatement, string model = "gemini-2.5-flash", CancellationToken ct = default);
    }
}