using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestionService.Models;

public class AnswerByAI
{
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string Content { get; set; }

    // Which model generated the response (gpt-5-chat, Claude, local model, etc.)
    [MaxLength(100)]
    public string AiModel { get; set; } = "gpt-5-chat";

    // The user (requesting actor) who triggered generation of this AI answer
    [MaxLength(36)]
    public required string UserId { get; set; }

    // Confidence or quality rating if you want to implement scoring later
    public float? ConfidenceScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool Accepted { get; set; }

    // AI answers are never "accepted" by users but users might like or upvote
    public int Votes { get; set; }

    public int? UserHelpfulVotes { get; set; }
    public int? UserNotHelpfulVotes { get; set; }

    public string? RawAiResponse { get; set; }

    public string? PromptUsed { get; set; }

    public string? Diagnosis { get; set; } 
    public string? LikelyRootCause { get; set; } 
    public string? FixStepByStep { get; set; }
    public string? CodePatch { get; set; }
    public string? Alternatives { get; set; }
    public string? Gotchas { get; set; }
    public bool HasVoted { get; set; }

    // nav props
    [MaxLength(36)]
    public required string QuestionId { get; set; }
    [JsonIgnore]
    public Question Question { get; set; } = null!;
}