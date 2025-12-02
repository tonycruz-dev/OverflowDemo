using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using QuestionService.Data;
using QuestionService.Models;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class AIModelesController(QuestionDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AIModel>>> GetAIModels()
    {
         return await db.AIModels.AsNoTracking().ToListAsync();
    }

    // Added: fetch a single AI model by id for edit page
    [HttpGet("{id}")]
    public async Task<ActionResult<AIModel>> GetAIModel(string id)
    {
        var model = await db.AIModels.FindAsync(id);
        if (model is null) return NotFound();
        return model;
    }

    // Create a new AI model (requires authentication)
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AIModel>> CreateAIModel([FromBody] AIModel model)
    {
        if (model is null) return BadRequest("Invalid body");

        // Ensure unique name
        if (!string.IsNullOrWhiteSpace(model.Name) && await db.AIModels.AnyAsync(x => x.Name == model.Name))
        {
            return Conflict("An AI model with that name already exists.");
        }

        model.Id = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString() : model.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;

        await db.AIModels.AddAsync(model);
        await db.SaveChangesAsync();

        return Created($"/AIModeles/{model.Id}", model);
    }

    // Update an existing AI model (requires authentication)
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAIModel(string id, [FromBody] AIModel update)
    {
        var existing = await db.AIModels.FindAsync(id);
        if (existing is null) return NotFound();
        if (update is null) return BadRequest("Invalid body");

        // Handle name uniqueness if changed
        if (!string.IsNullOrWhiteSpace(update.Name) && !string.Equals(existing.Name, update.Name, StringComparison.Ordinal) &&
            await db.AIModels.AnyAsync(x => x.Name == update.Name && x.Id != id))
        {
            return Conflict("Another AI model with that name already exists.");
        }

        existing.Name = update.Name ?? existing.Name;
        existing.Description = update.Description ?? existing.Description;
        existing.Version = update.Version ?? existing.Version;
        existing.Role = update.Role ?? existing.Role;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // Delete an AI model (requires authentication)
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAIModel(string id)
    {
        var existing = await db.AIModels.FindAsync(id);
        if (existing is null) return NotFound();

        db.AIModels.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }
}