using QuestionService.DTOs;

namespace QuestionService.Clients.AI
{
    public interface IGroqAIAnswer
    {
        Task<CreateAiAnswerDto?> OpenAIAnswerCodeErrorAsync(string title, string problemStatement, string model, CancellationToken ct = default);
        Task<CreateAiAnswerDto?> QwenAnswerCodeErrorAsync(string title, string problemStatement, CancellationToken ct = default);
    }
}