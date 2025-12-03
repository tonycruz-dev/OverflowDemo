using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using ProfileService.Data;
using ProfileService.DTOs;
using ProfileService.Models;
using System.ComponentModel;

namespace ProfileService.MCPTools;

[McpServerToolType]
public class ProfilesTools(ProfileDbContext db)
{

    // Get all profiles count
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the total count of user profiles.")]
    public async Task<int> GetProfilesCount()
    {
        return await db.UserProfiles.CountAsync();
    }

    //get all UserProfiles
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get all user profiles.")]
    public async Task<IReadOnlyList<UserProfile>> GetAllUserProfiles()
    {
        return await db.UserProfiles.ToListAsync();
    }
    // 🔹 Get the current user's profile
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get the profile of the current user by their ID.")]
    public async Task<UserProfile> GetMyProfile(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var profile = await db.UserProfiles.FindAsync(userId);
        return profile is null ? throw new InvalidOperationException("Profile not found for current user.") : profile;
    }

    // 🔹 Get a batch of profiles by comma-separated IDs
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get a batch of profile summaries by a list of user IDs.")]
    public async Task<IReadOnlyList<ProfileSummaryDto>> GetProfilesBatch(List<string> ids)
    {
        if (ids is null || ids.Count == 0)
            throw new ArgumentException("At least one id is required.", nameof(ids));

        var distinctIds = ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var rows = await db.UserProfiles
            .Where(x => distinctIds.Contains(x.Id))
            .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation))
            .ToListAsync();

        return rows;
    }

    // 🔹 Get all profiles (optionally sorted)
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get all user profiles, optionally sorted by reputation or display name.")]
    public async Task<IReadOnlyList<UserProfile>> GetProfiles(string? sortBy)
    {
        var query = db.UserProfiles.AsQueryable();

        query = sortBy == "reputation"
            ? query.OrderByDescending(x => x.Reputation)
            : query.OrderBy(x => x.DisplayName);

        return await query.ToListAsync();
    }

    // 🔹 Get a single profile by ID
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Get a single user profile by ID.")]
    public async Task<UserProfile> GetProfile(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Profile id is required.", nameof(id));

        var profile = await db.UserProfiles.FindAsync(id);
        if (profile is null)
            throw new InvalidOperationException($"Profile '{id}' was not found.");

        return profile;
    }

    // 🔹 Edit the current user's profile
    [McpServerTool(UseStructuredContent = true)]
    [Description("Edit the current user's profile (display name and description).")]
    public async Task<UserProfile> EditMyProfile(EditProfileDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User id is required.");

        var profile = await db.UserProfiles.FindAsync(userId) ?? throw new InvalidOperationException("Profile not found for current user.");
        profile.DisplayName = dto.DisplayName ?? profile.DisplayName;
        profile.Description = dto.Description ?? profile.Description;

        await db.SaveChangesAsync();

        return profile;
    }
}
