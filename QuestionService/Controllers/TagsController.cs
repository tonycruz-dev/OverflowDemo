using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using QuestionService.Data;
using QuestionService.Models;
using System.Text.RegularExpressions;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class TagsController(QuestionDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags(string? sort)
    {
        var query = db.Tags.AsQueryable();

        query = sort == "popular"
            ? query.OrderByDescending(x => x.UsageCount).ThenBy(x => x.Name)
                : query.OrderBy(x => x.Name);

        return await query.AsNoTracking().ToListAsync();
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<Tag>> GetTag(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest("Invalid slug");
        var tag = await db.Tags.FirstOrDefaultAsync(x => x.Slug == slug || x.Id == slug);
        if (tag is null) return NotFound();
        return tag;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Tag>> CreateTag([FromBody] Tag tag)
    {
        if (tag is null) return BadRequest("Invalid body");
        if (string.IsNullOrWhiteSpace(tag.Name) || string.IsNullOrWhiteSpace(tag.Description))
            return BadRequest("Name and Description are required");

        var slug = string.IsNullOrWhiteSpace(tag.Slug) ? MakeSlug(tag.Name) : MakeSlug(tag.Slug);
        if (await db.Tags.AnyAsync(x => x.Slug == slug))
            return Conflict("Tag with that slug already exists");

        tag.Id = string.IsNullOrWhiteSpace(tag.Id) ? Guid.NewGuid().ToString() : tag.Id;
        tag.Slug = slug;
        tag.UsageCount = 0; // prevent client manipulation

        await db.Tags.AddAsync(tag);
        await db.SaveChangesAsync();
        return Created($"/Tags/{tag.Slug}", tag);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateTag(string id, [FromBody] Tag update)
    {
        var existing = await db.Tags.FindAsync(id);
        if (existing is null) return NotFound();
        if (update is null) return BadRequest("Invalid body");

        var newName = string.IsNullOrWhiteSpace(update.Name) ? existing.Name : update.Name.Trim();
        var newDescription = string.IsNullOrWhiteSpace(update.Description) ? existing.Description : update.Description.Trim();
        var newSlug = string.IsNullOrWhiteSpace(update.Slug) ? MakeSlug(newName) : MakeSlug(update.Slug);

        if (!string.Equals(existing.Slug, newSlug, StringComparison.OrdinalIgnoreCase) &&
            await db.Tags.AnyAsync(x => x.Slug == newSlug && x.Id != id))
            return Conflict("Another tag with that slug already exists");

        existing.Name = newName;
        existing.Description = newDescription;
        existing.Slug = newSlug;
        // UsageCount not client editable

        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTag(string id)
    {
        var existing = await db.Tags.FindAsync(id);
        if (existing is null) return NotFound();

        if (existing.UsageCount > 0) return BadRequest("Cannot delete a tag that is in use");

        db.Tags.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string MakeSlug(string value)
    {
        value = value.Trim().ToLowerInvariant();
        value = Regex.Replace(value, "[^a-z0-9]+", "-");
        value = Regex.Replace(value, "-+", "-");
        return value.Trim('-');
    }
}
