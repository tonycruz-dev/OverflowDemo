using ModelContextProtocol.Server;
using SearchService.Models;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MCPTools;

[McpServerToolType]
public class SearchTools(ITypesenseClient client)
{
    // 🔍 Full search over title + content, with optional [tag] syntax
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Search questions by text query across title and content. Optionally filter by [tag] in the query.")]
    public async Task<IReadOnlyList<SearchQuestion>> SearchQuestions(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query is required.", nameof(query));

        // [aspire]something
        string? tag = null;
        var tagMatch = Regex.Match(query, @"\[(.*?)\]");
        if (tagMatch.Success)
        {
            tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
        }

        var searchParams = new SearchParameters(query, "title,content");

        if (!string.IsNullOrWhiteSpace(tag))
        {
            searchParams.FilterBy = $"tags:=[{tag}]";
        }

        try
        {
            var result = await client.Search<SearchQuestion>("questions", searchParams);
            return [.. result.Hits.Select(h => h.Document)];
        }
        catch (Exception ex)
        {
            // MCP: surface a clear error message
            throw new InvalidOperationException($"Typesense search failed: {ex.Message}", ex);
        }
    }

    // 🔍 Search similar titles only
    [McpServerTool(UseStructuredContent = true, ReadOnly = true)]
    [Description("Search for questions with similar titles to the given query.")]
    public async Task<IReadOnlyList<SearchQuestion>> SearchSimilarTitles(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query is required.", nameof(query));

        var searchParams = new SearchParameters(query, "title");

        try
        {
            var result = await client.Search<SearchQuestion>("questions", searchParams);
            return [.. result.Hits.Select(h => h.Document)];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Typesense search failed: {ex.Message}", ex);
        }
    }
}

