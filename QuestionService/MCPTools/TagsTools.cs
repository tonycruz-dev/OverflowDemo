using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using QuestionService.Data;
using System.ComponentModel;
using System.Text.RegularExpressions;
using QuestionService.Models;
namespace QuestionService.MCPTools;

[McpServerToolType]
public class TagsTools(QuestionDbContext db)
{
    // 🔹 Get tags (optionally sorted by popularity)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Retrieve all tags, optionally sorted by popularity or by name.")]
    public async Task<IReadOnlyList<Tag>> GetTags(string? sort)
    {
        var query = db.Tags.AsQueryable();

        query = sort == "popular"
            ? query.OrderByDescending(x => x.UsageCount).ThenBy(x => x.Name)
            : query.OrderBy(x => x.Name);

        return await query.AsNoTracking().ToListAsync();
    }

    // 🔹 Get a single tag by slug or ID
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Retrieve a single tag by its slug or ID.")]
    public async Task<Tag> GetTag(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.", nameof(slug));

        var tag = await db.Tags.FirstOrDefaultAsync(x => x.Slug == slug || x.Id == slug);
        if (tag is null)
            throw new InvalidOperationException($"Tag '{slug}' was not found.");

        return tag;
    }

    // 🔹 Create a new tag
    // Keep [Authorize] if you're also exposing this as an HTTP endpoint.
    [McpServerTool(UseStructuredContent = true, ReadOnly = false)]
    //[Authorize]
    [Description("Create a new tag. Generates a slug and enforces uniqueness.")]
    public async Task<Tag> CreateTag(Tag tag)
    {
        if (tag is null)
            throw new ArgumentException("Request body is required.", nameof(tag));

        if (string.IsNullOrWhiteSpace(tag.Name) || string.IsNullOrWhiteSpace(tag.Description))
            throw new ArgumentException("Name and Description are required.", nameof(tag));

        var slug = string.IsNullOrWhiteSpace(tag.Slug)
            ? MakeSlug(tag.Name)
            : MakeSlug(tag.Slug);

        if (await db.Tags.AnyAsync(x => x.Slug == slug))
            throw new InvalidOperationException($"Tag with slug '{slug}' already exists.");

        tag.Id = string.IsNullOrWhiteSpace(tag.Id) ? Guid.NewGuid().ToString() : tag.Id;
        tag.Slug = slug;
        tag.UsageCount = 0; // prevent client manipulation

        await db.Tags.AddAsync(tag);
        await db.SaveChangesAsync();

        return tag;
    }

    // 🔹 Update an existing tag
    [McpServerTool(UseStructuredContent = true, ReadOnly = false)]
    //[Authorize]
    [Description("Update an existing tag's name, description, or slug. Enforces slug uniqueness.")]
    public async Task<Tag> UpdateTag(string id, Tag update)
    {
        if (update is null)
            throw new ArgumentException("Request body is required.", nameof(update));

        var existing = await db.Tags.FindAsync(id);
        if (existing is null)
            throw new InvalidOperationException($"Tag with id '{id}' was not found.");

        var newName = string.IsNullOrWhiteSpace(update.Name)
            ? existing.Name
            : update.Name.Trim();

        var newDescription = string.IsNullOrWhiteSpace(update.Description)
            ? existing.Description
            : update.Description.Trim();

        var newSlug = string.IsNullOrWhiteSpace(update.Slug)
            ? MakeSlug(newName)
            : MakeSlug(update.Slug);

        if (!string.Equals(existing.Slug, newSlug, StringComparison.OrdinalIgnoreCase) &&
            await db.Tags.AnyAsync(x => x.Slug == newSlug && x.Id != id))
        {
            throw new InvalidOperationException($"Another tag with slug '{newSlug}' already exists.");
        }

        existing.Name = newName;
        existing.Description = newDescription;
        existing.Slug = newSlug;
        // UsageCount not client editable

        await db.SaveChangesAsync();
        return existing;
    }

    // 🔹 Delete a tag
    [McpServerTool(UseStructuredContent = true, ReadOnly = false, Destructive = true)]
    //[Authorize]
    [Description("Delete a tag by ID. Fails if the tag is currently in use.")]
    public async Task<bool> DeleteTag(string id)
    {
        var existing = await db.Tags.FindAsync(id);
        if (existing is null)
            throw new InvalidOperationException($"Tag with id '{id}' was not found.");

        if (existing.UsageCount > 0)
            throw new InvalidOperationException("Cannot delete a tag that is in use.");

        db.Tags.Remove(existing);
        await db.SaveChangesAsync();
        return true;
    }

    private static string MakeSlug(string value)
    {
        value = value.Trim().ToLowerInvariant();
        value = Regex.Replace(value, "[^a-z0-9]+", "-");
        value = Regex.Replace(value, "-+", "-");
        return value.Trim('-');
    }
}
