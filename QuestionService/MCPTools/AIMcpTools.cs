using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using QuestionService.Data;
using QuestionService.Models;
using System.ComponentModel;

namespace QuestionService.MCPTools;

[McpServerToolType]
public class AIMcpTools(QuestionDbContext db)
{

    [McpServerTool(UseStructuredContent = true), Description("Retrieves all AI models from the database using a no-tracking query.")]
    public async Task<ActionResult<IReadOnlyList<AIModel>>> GetAIModels()
    {
        return await db.AIModels.AsNoTracking().ToListAsync();
    }
    [McpServerTool(UseStructuredContent = true), Description("Retrieve a specific AI model from the database by its ID.")]
    public async Task<ActionResult<AIModel>> GetAIModel(string id)
    {
        var model = await db.AIModels.FindAsync(id);
        if (model is null)
        {
            // MCP: throw to signal an error to the client
            throw new InvalidOperationException($"AI model with id '{id}' was not found.");
        }
        return model;
    }


    // ➕ Create a new AI model (requires authentication)
    [McpServerTool(UseStructuredContent = true),
    /// Authorize,
     Description("Create a new AI model. Ensures unique name and assigns creation metadata.")]
    public async Task<ActionResult<AIModel>> CreateAIModel(AIModel model)
    {
        if (model is null)
        {
            throw new ArgumentException("Request body is required.", nameof(model));
        }

        // Ensure unique name
        if (!string.IsNullOrWhiteSpace(model.Name) &&
            await db.AIModels.AnyAsync(x => x.Name == model.Name))
        {
            throw new InvalidOperationException(
             $"An AI model with the name '{model.Name}' already exists.");
        }

        model.Id = string.IsNullOrWhiteSpace(model.Id)
            ? Guid.NewGuid().ToString()
            : model.Id;

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;

        await db.AIModels.AddAsync(model);
        await db.SaveChangesAsync();

        return model;
    }


    // ✏️ Update an existing AI model (requires authentication)
    // ✏️ Update an existing AI model
    [McpServerTool(UseStructuredContent = true, ReadOnly = false)]
    //[Authorize]
    [Description("Update an existing AI model by ID. Validates name uniqueness and updates metadata.")]
    public async Task<AIModel> UpdateAIModel(string id, AIModel update)
    {
        if (update is null)
        {
            throw new ArgumentException("Request body is required.", nameof(update));
        }

        var existing = await db.AIModels.FindAsync(id);
        if (existing is null)
        {
            throw new InvalidOperationException($"AI model with id '{id}' was not found.");
        }

        // Validate name uniqueness if changed
        if (!string.IsNullOrWhiteSpace(update.Name) &&
            !string.Equals(existing.Name, update.Name, StringComparison.Ordinal) &&
            await db.AIModels.AnyAsync(x => x.Name == update.Name && x.Id != id))
        {
            throw new InvalidOperationException(
                $"Another AI model with the name '{update.Name}' already exists.");
        }

        existing.Name = update.Name ?? existing.Name;
        existing.Description = update.Description ?? existing.Description;
        existing.Version = update.Version ?? existing.Version;
        existing.Role = update.Role ?? existing.Role;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // MCP: return the updated entity instead of NoContent()
        return existing;
    }


    // ❌ Delete an AI model
    [McpServerTool(UseStructuredContent = true, ReadOnly = false, Destructive = true)]
    //[Authorize]
    [Description("Delete an AI model from the database by ID.")]
    public async Task<bool> DeleteAIModel(string id)
    {
        var existing = await db.AIModels.FindAsync(id);
        if (existing is null)
        {
            throw new InvalidOperationException($"AI model with id '{id}' was not found.");
        }

        db.AIModels.Remove(existing);
        await db.SaveChangesAsync();

        // MCP: return a simple success flag (or a richer result type if you prefer)
        return true;
    }

}
