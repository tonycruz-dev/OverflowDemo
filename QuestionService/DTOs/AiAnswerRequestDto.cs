namespace QuestionService.DTOs;

public sealed class AiAnswerRequestDto
{
    /// <summary>Which models to include. Examples: "gpt-5-chat", "gemini-2.5-flash", "openai/gpt-oss-120b", "meta-llama/llama-4-maverick-17b-128e-instruct", "qwen/qwen3-32b", "gpt-4.1", "DeepSeek-V3-0324"</summary>
    public List<string>? Include { get; set; }

    /// <summary>Max characters to persist in Content. Default 5000.</summary>
    public int? MaxChars { get; set; }
}

