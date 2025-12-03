using Contracts;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Reputation;
using System.ComponentModel;
using VoteService.Data;
using VoteService.DTOs;
using VoteService.Models;
using Wolverine;

namespace VoteService.MCPTools;

[McpServerToolType]
public class VotesTools(VoteDbContext db, IMessageBus bus)
{
    // 🔹 Cast a vote on a question or answer
    [McpServerTool(UseStructuredContent = true)]
    [Description("Cast an upvote or downvote on a question or answer.")]
    public async Task<bool> CastVote(CastVoteDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        if (dto.TargetType is not ("Question" or "Answer"))
            throw new ArgumentException("Invalid target type. Must be 'Question' or 'Answer'.", nameof(dto));

        var alreadyVoted = await db.Votes
            .AnyAsync(x => x.UserId == userId && x.TargetId == dto.TargetId);

        if (alreadyVoted)
            throw new InvalidOperationException("User has already voted on this target.");

        db.Votes.Add(new Vote
        {
            TargetId = dto.TargetId,
            TargetType = dto.TargetType,
            UserId = userId,
            VoteValue = dto.VoteValue,
            QuestionId = dto.QuestionId
        });

        await db.SaveChangesAsync();

        var reason = (dto.VoteValue, dto.TargetType) switch
        {
            (1, "Question") => ReputationReason.QuestionUpvoted,
            (1, "Answer") => ReputationReason.AnswerUpvoted,
            (-1, "Answer") => ReputationReason.AnswerDownvoted,
            _ => ReputationReason.QuestionDownvoted
        };

        await bus.PublishAsync(ReputationHelper.MakeEvent(dto.TargetUserId, reason, userId));
        await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));

        return true;
    }

    // 🔹 Get current user's votes for a question
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get all votes that the current user has cast on a given question (question + answers).")]
    public async Task<IReadOnlyList<UserVotesResult>> GetUserVotesForQuestion(string questionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var votes = await db.Votes
            .Where(x => x.UserId == userId && x.QuestionId == questionId)
            .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
            .ToListAsync();

        return votes;
    }

    // 🔹 Cast a vote on an AI (AI answer / question)
    [McpServerTool(UseStructuredContent = true)]
    [Description("Cast an upvote or downvote on an AI-generated question or answer.")]
    public async Task<bool> CastAiVote(CastVoteAiDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        if (dto.TargetType is not ("Question" or "Answer"))
            throw new ArgumentException("Invalid target type. Must be 'Question' or 'Answer'.", nameof(dto));

        var alreadyVoted = await db.VoteAIs
            .AnyAsync(x => x.AiId == dto.AiId
                           && x.TargetId == dto.TargetId
                           && x.UserId == userId);

        if (alreadyVoted)
            throw new InvalidOperationException("User has already voted on this AI target.");

        db.VoteAIs.Add(new VoteAI
        {
            TargetId = dto.TargetId,
            TargetType = dto.TargetType,
            AiId = dto.AiId,
            UserId = userId,
            VoteValue = dto.VoteValue,
            QuestionId = dto.QuestionId
        });

        await db.SaveChangesAsync();

        var reason = (dto.VoteValue, dto.TargetType) switch
        {
            (1, "Question") => ReputationReason.QuestionUpvoted,
            (1, "Answer") => ReputationReason.AnswerUpvoted,
            (-1, "Answer") => ReputationReason.AnswerDownvoted,
            _ => ReputationReason.QuestionDownvoted
        };

        await bus.PublishAsync(ReputationHelper.MakeEventAi(dto.AiId, reason, userId));
        await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));

        return true;
    }

    // 🔹 Get votes for a given question + AI (all users)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get all votes for a given question and AI agent.")]
    public async Task<IReadOnlyList<UserVotesResult>> GetVotesForAi(string questionId, string aiId, string userId)
    {
        // userId is only used as an auth surrogate (like RequireAuthorization)
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var votes = await db.VoteAIs
            .Where(x => x.AiId == aiId && x.QuestionId == questionId)
            .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
            .ToListAsync();

        return votes;
    }

    // 🔹 Get all AI votes for a question (all AIs, all users)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get all AI votes for a given question across all AI agents and users.")]
    public async Task<IReadOnlyList<UserVotesAiResult>> GetAllAiVotesForQuestion(string questionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var votes = await db.VoteAIs
            .Where(x => x.QuestionId == questionId)
            .Select(x => new UserVotesAiResult(
                x.AiId,
                x.QuestionId,
                x.UserId,
                x.TargetId,
                x.TargetType,
                x.VoteValue))
            .ToListAsync();

        return votes;
    }
}
