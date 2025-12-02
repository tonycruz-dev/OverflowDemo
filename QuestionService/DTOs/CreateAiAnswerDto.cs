using System.ComponentModel.DataAnnotations;

namespace QuestionService.DTOs;

public record CreateAiAnswerDto(
    [Required] string Content,
    string? AiModel,
    float? ConfidenceScore,
    string? RawAiResponse,
    string? PromptUsed,
    string? Diagnosis,
    string? LikelyRootCause,
    string? FixStepByStep,
    string? CodePatch,
    string? Alternatives,
    string? Gotchas
);
// NOTE: Removed [Required] attributes so we can perform partial updates (e.g. just scores)
// without having to resend the entire AI answer content payload.
public record UpdateAiAnswerDto(
    string? Id,
    string? Content,
    string? AiModel,
    float? ConfidenceScore,
    string? RawAiResponse,
    int? Votes,
    int? UserHelpfulVotes,
    int? UserNotHelpfulVotes,
    string? PromptUsed,
    string? Diagnosis,
    string? LikelyRootCause,
    string? FixStepByStep,
    string? CodePatch,
    string? Alternatives,
    string? Gotchas,
    bool? HasVoted
);


//     public int? UserHelpfulVotes { get; set; }
//     public int? UserNotHelpfulVotes { get; set; }
