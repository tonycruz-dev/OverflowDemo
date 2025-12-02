namespace VoteService.DTOs;

public record UserVotesResult(string TargetId, string TargetType, int VoteValue);

public record UserVotesAiResult(string AiId, string QuestionId, string UserId,string TargetId, string TargetType, int VoteValue);