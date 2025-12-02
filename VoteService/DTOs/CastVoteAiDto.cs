namespace VoteService.DTOs;

public record CastVoteAiDto(
        string TargetId,
        string TargetType,
        string TargetUserId,
        string AiId,
        string QuestionId,
        int VoteValue
    );
