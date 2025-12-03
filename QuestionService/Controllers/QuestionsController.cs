using Common;
using Contracts;
using FastExpressionCompiler;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using QuestionService.Clients.AI;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.RequestHelpers;
using QuestionService.Services;
using Reputation;
using System.Security.Claims;
using Wolverine;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class QuestionsController(
    QuestionDbContext context, 
    IMessageBus bus, 
    TagService tagService,
    IGeminiModelsClient gemini,
    IGitHubModelsClient githubClient,
    IGroqAIAnswer helpAIAnswer) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestions(CreateQuestionDto dto)
    {
        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("Invalid tags");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");

        if (userId is null || name is null) return BadRequest("Cannot get user details");

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

            await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content,
                question.CreatedAt, question.TagSlugs));

            await tx.CommitAsync();
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            Console.WriteLine(e);
            throw;
        }



        var slugs = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (slugs.Length > 0)
        {
            await context.Tags
                .Where(t => slugs.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount,
                    t => t.UsageCount + 1));
        }

        return Created($"/questions/{question.Id}", question);
    }
    [HttpGet]
    public async Task<ActionResult<PaginationResult<Question>>> GetQuestions([FromQuery] QuestionsQuery q)
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
    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await context.Questions
           .Include(x => x.Answers)
           .Include(x => x.AiAnswers)
           .FirstOrDefaultAsync(x => x.Id == id);
        if (question is null) return NotFound();

        await context.Questions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));

        return question;
    }
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await context.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (question.AskerId != userId)
        {
            return Forbid();
        }

        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("Invalid tags");

        var original = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var incoming = dto.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var removed = original.Except(incoming, StringComparer.OrdinalIgnoreCase).ToArray();
        var added = incoming.Except(original, StringComparer.OrdinalIgnoreCase).ToArray();


        var sanitizer = new HtmlSanitizer();
        question.Title = dto.Title;
        question.Content = sanitizer.Sanitize(dto.Content);
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        if (removed.Length > 0)
        {
            await context.Tags
                .Where(t => removed.Contains(t.Slug) && t.UsageCount > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount,
                    t => t.UsageCount - 1));
        }

        if (added.Length > 0)
        {
            await context.Tags
                .Where(t => added.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount,
                    t => t.UsageCount + 1));
        }

        await bus.PublishAsync(new Contracts.QuestionUpdated(
            question.Id,
            question.Title,
            question.Content,
            question.TagSlugs.AsArray()
        ));

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var question = await context.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (question.AskerId != userId)
        {
            return Forbid();
        }
        context.Questions.Remove(question);
        await context.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.QuestionDeleted(id));
        return NoContent(); ;
    }

    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    {
        var question = await context.Questions.FindAsync(questionId);

        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");

        if (userId is null || name is null) return BadRequest("Cannot get user details");

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

        return Created($"/questions/{questionId}", answer);
    }

    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var answer = await context.Answers.FindAsync(answerId);
        if (answer is null) return NotFound();
        if (answer.QuestionId != questionId) return BadRequest("Cannot update answer details");

        var sanitizer = new HtmlSanitizer();

        answer.Content = sanitizer.Sanitize(dto.Content);
        answer.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId || answer.Accepted) return BadRequest("Cannot delete this answer");

        context.Answers.Remove(answer);
        question.AnswerCount--;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

        return NoContent();
    }

    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId || question.HasAcceptedAnswer) return BadRequest("Cannot accept answer");

        answer.Accepted = true;
        question.HasAcceptedAnswer = true;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerAccepted(questionId));
        await bus.PublishAsync(ReputationHelper.MakeEvent(answer.UserId,
           ReputationReason.AnswerAccepted, question.AskerId));

        return NoContent();
    }

    /// <summary>
    /// /////////////// AI Answers endpoints
    /// </summary>
    /// <param name="questionId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize] // consider [Authorize(Roles = "ai,admin")] or a custom policy
    [HttpPost("{questionId}/ai-answers")]
    public async Task<ActionResult<List<AnswerByAI>>> PostAiAnswer(
        string questionId,
    [FromBody] AiAnswerRequestDto? body,                 // optional JSON body
    [FromQuery] string? include = null)
    {
        var question = await context.Questions.FindAsync(questionId);
        if (question is null) return NotFound();

        // Compose final "include" from query OR body
        var includeFromQuery = string.IsNullOrWhiteSpace(include)
            ? []
            : include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var requested = (body?.Include?.Count > 0 ? body!.Include! : [.. includeFromQuery])
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        // Load all available model names from DB (lowercased)
        var allModelNames = await context.AIModels
            .AsNoTracking()
            .Select(m => m.Name.ToLower())
            .ToListAsync();

        // If client did not request any, use all available models
        if (requested.Count == 0)
        {
            requested = allModelNames;
        }
        else
        {
            // Keep only models that actually exist in DB
            requested = [.. requested.Where(r => allModelNames.Contains(r))];
        }

        // Max length logic (query > body > default)
        //int contentMax = maxChars
        //    ?? body?.MaxChars
        //    ?? 10000;

        var sanitizer = new HtmlSanitizer();

        // Map normalized keys to concrete invocations
        // Each function returns CreateAiAnswerDto? (your existing type),
        // which we transform into AnswerByAI.
        var runners = new Dictionary<string, Func<Task<(string modelLabel, CreateAiAnswerDto? result)>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var modelName in allModelNames)
        {
            runners[modelName] = async () =>
            {
                CreateAiAnswerDto? r = null;
                // Pattern-based selection of provider
                if (modelName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
                {
                    r = await gemini.GEMINIModelsAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                else if (modelName.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase))
                {
                    r = await githubClient.GitHubGPT5ModelsAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                else if (modelName.Contains("gpt-4.1", StringComparison.OrdinalIgnoreCase))
                {
                    r = await githubClient.GitHubModelsAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                else if (modelName.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
                {
                    r = await githubClient.DeepSeekModelsAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                else if (modelName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                {
                    r = await helpAIAnswer.QwenAnswerCodeErrorAsync(question.Title, question.Content);
                }
                else if (modelName.Contains("openai", StringComparison.OrdinalIgnoreCase) || modelName.Contains("meta-llama", StringComparison.OrdinalIgnoreCase))
                {
                    r = await helpAIAnswer.OpenAIAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                else
                {
                    // Fallback generic OpenAI-style handler
                    r = await helpAIAnswer.OpenAIAnswerCodeErrorAsync(question.Title, question.Content, modelName);
                }
                return (modelName, r);
            };
        }

        // Filter to only known runners and keep order of request
        var toRun = requested.Where(runners.ContainsKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (toRun.Count == 0)
            return BadRequest(new { error = "No supported models in 'include'." });

        // Run sequentially to play nicely with provider rate limits
        // (If you prefer parallel and your quotas allow, you can use Task.WhenAll)
        var producedAnswers = new List<AnswerByAI>(capacity: toRun.Count);
        var generatingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-ai"; // capture once
        foreach (var key in toRun)
        {
            try
            {
                var (label, dto) = await runners[key]();

                if (dto is null || string.IsNullOrWhiteSpace(dto.Content))
                    continue;
                var content = Markdown.ToHtml(dto.Content ?? "");
                var diagnosis = Markdown.ToHtml(dto.Diagnosis ?? "");
                var likelyRootCause = Markdown.ToHtml(dto.LikelyRootCause ?? "");
                var fixStepByStep = Markdown.ToHtml(dto.FixStepByStep ?? "");
                var codePatch = Markdown.ToHtml(dto.CodePatch ?? "");
                var alternatives = Markdown.ToHtml(dto.Alternatives ?? "");
                var gotchas = Markdown.ToHtml(dto.Gotchas ?? "");

                var ai = new AnswerByAI
                {
                    Content = content,
                    AiModel = string.IsNullOrWhiteSpace(dto.AiModel) ? label : dto.AiModel!,
                    ConfidenceScore = dto.ConfidenceScore,
                    RawAiResponse = dto.RawAiResponse,
                    PromptUsed = dto.PromptUsed,
                    QuestionId = questionId,
                    UserId = generatingUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Diagnosis = diagnosis,
                    LikelyRootCause = likelyRootCause,
                    FixStepByStep = fixStepByStep,
                    CodePatch = codePatch,
                    Alternatives = alternatives,
                    Gotchas = gotchas
                };

                producedAnswers.Add(ai);
                await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
            }
            catch (Exception)
            {
                // Optional: collect per-model failures to return to client
                //_logger.LogWarning(ex, "AI model '{Model}' failed.", key);
                continue;
            }
        }

        if (producedAnswers.Count == 0)
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "All selected AI model calls failed or returned empty content." });

        // Save once
        question.AiAnswers ??= [];
        question.AiAnswers.AddRange(producedAnswers);
        await context.SaveChangesAsync();

        // Return all newly created answers
        return Ok(producedAnswers);
    }

    [Authorize] // consider [Authorize(Roles = "ai,admin")]
    [HttpPut("{questionId}/ai-answers/{aiAnswerId}")]
    public async Task<ActionResult> UpdateAiAnswer(string questionId, string aiAnswerId, UpdateAiAnswerDto dto)
    {
        var ai = await context.AiAnswers.FindAsync(aiAnswerId);
        var question = await context.Questions.FindAsync(questionId);
        if (ai is null) return NotFound();
        if (ai.QuestionId != questionId) return BadRequest("Cannot update answer details");
        // Only update fields that are provided (partial update semantics)
        // if ai.Votes > 0 then ai.Votes=ai.Votes+ dto.Votes.Value
        if (ai.Votes > 0 && dto.Votes.HasValue)
        {
            ai.Votes += dto.Votes.Value;
        }
        else if (dto.Votes.HasValue)
        {
            ai.Votes = dto.Votes.Value;
        }

        if (dto.UserHelpfulVotes.HasValue) ai.UserHelpfulVotes = dto.UserHelpfulVotes.Value;
        if (dto.UserNotHelpfulVotes.HasValue) ai.UserNotHelpfulVotes = dto.UserNotHelpfulVotes.Value;
        if (dto.HasVoted.HasValue) ai.HasVoted = dto.HasVoted.Value;

        // if dto.Votes.Value > 5 accepted
        if (dto.Votes.HasValue && dto.Votes.Value >= 10)
        {
            if (question is not null && question.AskerId is not null)
            {
                await bus.PublishAsync(new AnswerAccepted(questionId));
                await bus.PublishAsync(ReputationHelper.MakeEventAi(ai.Id, ReputationReason.AnswerAccepted, question.AskerId));
            }
            ai.Accepted = true;
        }

        ai.UpdatedAt = DateTime.UtcNow;
        // if

        await context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize] // consider [Authorize(Roles = "ai,admin")]
    [HttpDelete("{questionId}/ai-answers/{aiAnswerId}")]
    public async Task<ActionResult> DeleteAiAnswer(string questionId, string aiAnswerId)
    {
        var ai = await context.AiAnswers.FindAsync(aiAnswerId);
        if (ai is null) return NotFound();
        if (ai.QuestionId != questionId) return BadRequest("Cannot delete this answer");

        // Optional: only AI/admin can delete
        // if (!User.IsInRole("ai") && !User.IsInRole("admin")) return Forbid();

        context.AiAnswers.Remove(ai);
        await context.SaveChangesAsync();
        return NoContent();
    }

    //get top 8 ai answers value by votes
    [HttpGet("topai-answers")]
    public async Task<ActionResult<List<AiAnswerSummaryDto>>> GetTopAiAnswers()
    {

        // I need top sum of votes ai answers group by AiModel for a question
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date; // retains Utc kind
        var startUtc = todayUtc.AddDays(-15);
        var endUtc = todayUtc.AddDays(1).AddTicks(-1); // end of today (UTC)

        var query = context.AiAnswers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt <= endUtc)
            .GroupBy(x => new { x.AiModel })
            .Select(g => new { g.Key.AiModel, TotalVotes = g.Sum(x => x.Votes) })
            .OrderByDescending(x => x.TotalVotes)
            .Take(8);

        var list = await query.ToListAsync();
        var answers = list.Select(x => new AiAnswerSummaryDto(x.AiModel, x.TotalVotes)).ToList();

        return Ok(answers);
    }
}