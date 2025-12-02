using QuestionService.DTOs;

namespace QuestionService.Clients.AI
{
    public interface IGitHubModelsClient
    {
        Task<CreateAiAnswerDto?> DeepSeekModelsAnswerCodeErrorAsync(string title, string problemStatement, string model = "gpt-4.1", CancellationToken ct = default);
        Task<CreateAiAnswerDto?> GitHubGPT5ModelsAnswerCodeErrorAsync(string title, string problemStatement, string model = "gpt-5-chat", CancellationToken ct = default);
        Task<CreateAiAnswerDto?> GitHubModelsAnswerCodeErrorAsync(string title, string problemStatement, string model = "gpt-4.1", CancellationToken ct = default);
    }
}