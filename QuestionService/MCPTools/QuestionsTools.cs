using Common;
using Contracts;
using Ganss.Xss;
using Markdig;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using QuestionService.Clients.AI;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.RequestHelpers;
using QuestionService.Services;
using Reputation;
using System.ComponentModel;
using Wolverine;

namespace QuestionService.MCPTools;

[McpServerToolType]
public class QuestionsTools(QuestionDbContext context,IMessageBus bus, TagService tagService)
{
    // 🔹 Create a question
    [McpServerTool(UseStructuredContent = true)]
    [Description("Create a new question with tags and sanitized HTML content.")]
    public async Task<Question> CreateQuestion(CreateQuestionDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        if (!await tagService.AreTagsValidAsync(dto.Tags))
            throw new ArgumentException("Invalid tags.", nameof(dto));

        var sanitizer = new HtmlSanitizer();

        var question = new Question
        {
            Title = dto.Title,
            Content = sanitizer.Sanitize(dto.Content),
            TagSlugs = dto.Tags,
            AskerId = userId
        };

        await using var tx = await context.Database.BeginTransactionAsync();

        try
        {
            await context.Questions.AddAsync(question);
            await context.SaveChangesAsync();

            await bus.PublishAsync(new QuestionCreated(
                question.Id,
                question.Title,
                question.Content,
                question.CreatedAt,
                question.TagSlugs));

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var slugs = question.TagSlugs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (slugs.Length > 0)
        {
            await context.Tags
                .Where(t => slugs.Contains(t.Slug))
                .ExecuteUpdateAsync(x =>
                    x.SetProperty(t => t.UsageCount, t => t.UsageCount + 1));
        }

        return question;
    }

    // 🔹 Get paged questions
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Retrieve a paginated list of questions, optionally filtered by tag and sorted by activity, newest, or unanswered.")]
    public async Task<PaginationResult<Question>> GetQuestions(QuestionsQuery q)
    {
        var query = context.Questions.AsQueryable();

        if (!string.IsNullOrEmpty(q.Tag))
        {
            query = query.Where(x => x.TagSlugs.Contains(q.Tag));
        }

        query = q.Sort switch
        {
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "active" => query.OrderByDescending(x => new[]
            {
                x.CreatedAt,
                x.UpdatedAt ?? DateTime.MinValue,
                x.Answers.Max(a => (DateTime?)a.CreatedAt) ?? DateTime.MinValue,
                x.Answers.Max(a => a.UpdatedAt) ?? DateTime.MinValue,
                x.AiAnswers.Max(a => (DateTime?)a.CreatedAt) ?? DateTime.MinValue,
                x.AiAnswers.Max(a => a.UpdatedAt) ?? DateTime.MinValue
            }.Max()),
            "unanswered" => query.Where(x => x.AnswerCount == 0)
                .OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var result = await query.ToPaginatedListAsync(q);
        return result;
    }

    // 🔹 Get a single question (increments view count)
    [McpServerTool(UseStructuredContent = true, ReadOnly = false)]
    [Description("Retrieve a question with its answers and AI answers, and increment its view count.")]
    public async Task<Question> GetQuestion(string id)
    {
        var question = await context.Questions
            .Include(x => x.Answers)
            .Include(x => x.AiAnswers)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (question is null)
            throw new InvalidOperationException($"Question '{id}' was not found.");

        await context.Questions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setter =>
                setter.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));

        return question;
    }

    // 🔹 Update a question
    
    // 🔹 Delete a question
    [McpServerTool(UseStructuredContent = true, Destructive = true)]
    [Description("Delete a question. Only the original asker may delete.")]
    public async Task<bool> DeleteQuestion(string id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var question = await context.Questions.FindAsync(id);
        if (question is null)
            throw new InvalidOperationException($"Question '{id}' was not found.");

        if (question.AskerId != userId)
            throw new InvalidOperationException("User is not allowed to delete this question.");

        context.Questions.Remove(question);
        await context.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.QuestionDeleted(id));
        return true;
    }

    // 🔹 Post an answer
    [McpServerTool(UseStructuredContent = true)]
    [Description("Post an answer to a question with sanitized HTML content.")]
    public async Task<Answer> PostAnswer(string questionId, CreateAnswerDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var question = await context.Questions.FindAsync(questionId);
        if (question is null)
            throw new InvalidOperationException($"Question '{questionId}' was not found.");

        var sanitizer = new HtmlSanitizer();

        var answer = new Answer
        {
            Content = sanitizer.Sanitize(dto.Content),
            UserId = userId,
            QuestionId = questionId
        };

        question.Answers.Add(answer);
        question.AnswerCount++;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

        return answer;
    }

    // 🔹 Update an answer
    [McpServerTool(UseStructuredContent = true)]
    [Description("Update the content of an answer.")]
    public async Task<Answer> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var answer = await context.Answers.FindAsync(answerId);
        if (answer is null)
            throw new InvalidOperationException($"Answer '{answerId}' was not found.");

        if (answer.QuestionId != questionId)
            throw new InvalidOperationException("Cannot update answer details for a different question.");

        var sanitizer = new HtmlSanitizer();
        answer.Content = sanitizer.Sanitize(dto.Content);
        answer.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return answer;
    }

    // 🔹 Delete an answer
    [McpServerTool(UseStructuredContent = true, Destructive = true)]
    [Description("Delete an answer that has not been accepted.")]
    public async Task<bool> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);

        if (answer is null || question is null)
            throw new InvalidOperationException("Question or answer not found.");

        if (answer.QuestionId != questionId || answer.Accepted)
            throw new InvalidOperationException("Cannot delete this answer.");

        context.Answers.Remove(answer);
        question.AnswerCount--;

        await context.SaveChangesAsync();
        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

        return true;
    }

    // 🔹 Accept an answer
    [McpServerTool(UseStructuredContent = true)]
    [Description("Mark an answer as accepted for a given question and emit reputation events.")]
    public async Task<bool> AcceptAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);

        if (answer is null || question is null)
            throw new InvalidOperationException("Question or answer not found.");

        if (answer.QuestionId != questionId || question.HasAcceptedAnswer)
            throw new InvalidOperationException("Cannot accept this answer.");

        answer.Accepted = true;
        question.HasAcceptedAnswer = true;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerAccepted(questionId));
        if (!string.IsNullOrWhiteSpace(question.AskerId))
        {
            await bus.PublishAsync(ReputationHelper.MakeEvent(
                answer.UserId,
                ReputationReason.AnswerAccepted,
                question.AskerId));
        }

        return true;
    }

    

    // 🔹 Get top AI answers aggregated by model
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the top 8 AI models by total votes for AI answers in the last 15 days.")]
    public async Task<List<AiAnswerSummaryDto>> GetTopAiAnswers()
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var startUtc = todayUtc.AddDays(-15);
        var endUtc = todayUtc.AddDays(1).AddTicks(-1);

        var query = context.AiAnswers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt <= endUtc)
            .GroupBy(x => new { x.AiModel })
            .Select(g => new { g.Key.AiModel, TotalVotes = g.Sum(x => x.Votes) })
            .OrderByDescending(x => x.TotalVotes)
            .Take(8);

        var list = await query.ToListAsync();
        var answers = list
            .Select(x => new AiAnswerSummaryDto(x.AiModel, x.TotalVotes))
            .ToList();

        return answers;
    }
}
