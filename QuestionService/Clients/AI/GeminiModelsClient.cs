using QuestionService.Clients.AI;
using QuestionService.DTOs;
using System.Text;
using System.Text.Json;

namespace QuestionService.Extensions;

public class GeminiModelsClient(IConfiguration configuration, ILogger<GeminiModelsClient> logger) : IGeminiModelsClient
{
    private readonly IConfiguration _config = configuration;
    private readonly ILogger<GeminiModelsClient> _logger = logger;
    public async Task<CreateAiAnswerDto?> GEMINIModelsAnswerCodeErrorAsync(
    string title,
    string problemStatement,
    string model = "gemini-2.5-flash-lite",
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        if (string.IsNullOrWhiteSpace(model))
        {
             model = "gemini-2.5-flash-lite";
        }

        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini API key missing. Set GEMINI_API_KEY. Returning null.");
            return null;
        }

        // Build prompt: same contract (JSON-only schema)
        var sys = "You are a senior engineer writing Stack Overflow–quality answers. " +
                  "Return JSON ONLY matching the schema with fields: summary, diagnosis, rootCause, " +
                  "fixSteps (array), codePatch (diff or full snippet), alternatives (array), " +
                  "gotchas (array), confidence (0..1). No markdown, no extra fields.";

        var userContent = new StringBuilder();
        userContent.AppendLine("# Problem Title");
        userContent.AppendLine(string.IsNullOrWhiteSpace(title) ? "Untitled issue" : title.Trim());
        userContent.AppendLine();
        userContent.AppendLine("# Problem Statement");
        userContent.AppendLine(problemStatement.Trim());

        // Gemini generateContent (v1beta)
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = sys + "\n\n" + userContent.ToString() }
                    }
                }
            }
        };

        using var http = new HttpClient();
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini request failed: {StatusCode} {Reason}. Body: {Body}", (int)resp.StatusCode, resp.StatusCode, body);
            return null;
        }

        string? text;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return null;
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            text = parts[0].GetProperty("text").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response. Raw body: {Body}", body);
            return null;
        }

        if (string.IsNullOrWhiteSpace(text)) return null;

        string ExtractJson2(string raw)
        {
            int first = raw.IndexOf('{');
            int last = raw.LastIndexOf('}');
            if (first >= 0 && last > first) return raw[first..(last + 1)];
            return raw;
        }
        var jsonCandidate = ExtractJson2(text);

        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        SoStyleAnswer? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SoStyleAnswer>(jsonCandidate, jsonOpts);
            if (parsed == null) return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON produced by Gemini. Raw output: {Output}", text);
            return null;
        }
        // Build fields
        var sbDiagnosis = new StringBuilder();
        sbDiagnosis.AppendLine(parsed.diagnosis?.Trim() ?? "");
        sbDiagnosis.AppendLine();

        var sbRootCause = new StringBuilder();
        sbRootCause.AppendLine(parsed.rootCause?.Trim() ?? "");
        sbRootCause.AppendLine();

        var sbFixSteps = new StringBuilder();
        if (parsed.fixSteps is { Count: > 0 })
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sbFixSteps.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
        sbFixSteps.AppendLine();
        var sbCodePatch = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(parsed.codePatch))
        {
            sbCodePatch.AppendLine("```diff");
            sbCodePatch.AppendLine(parsed.codePatch.Trim());
            sbCodePatch.AppendLine("```");
            sbCodePatch.AppendLine();
        }
        var sbAlternatives = new StringBuilder();
        if (parsed.alternatives is { Count: > 0 })
        {
            foreach (var alt in parsed.alternatives) sbAlternatives.AppendLine($"- {alt}");
            sbAlternatives.AppendLine();
        }
        var sbGotchas = new StringBuilder();
        if (parsed.gotchas is { Count: > 0 })
        {
            foreach (var g in parsed.gotchas) sbGotchas.AppendLine($"- {g}");
            sbGotchas.AppendLine();
        }
        sbGotchas.AppendLine();

        var sbOut = new StringBuilder();
        sbOut.AppendLine(parsed.summary?.Trim() ?? "");
        sbOut.AppendLine();
        sbOut.AppendLine("**Diagnosis**");
        sbOut.AppendLine(parsed.diagnosis?.Trim() ?? "");
        sbOut.AppendLine();
        sbOut.AppendLine("**Likely root cause**");
        sbOut.AppendLine(parsed.rootCause?.Trim() ?? "");
        sbOut.AppendLine();
        sbOut.AppendLine("**Fix (step-by-step)**");
        if (parsed.fixSteps is { Count: > 0 })
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sbOut.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
        if (!string.IsNullOrWhiteSpace(parsed.codePatch))
        {
            sbOut.AppendLine();
            sbOut.AppendLine("**Code patch**");
            sbOut.AppendLine("```diff");
            sbOut.AppendLine(parsed.codePatch.Trim());
            sbOut.AppendLine("```");
        }
        if (parsed.alternatives is { Count: > 0 })
        {
            sbOut.AppendLine();
            sbOut.AppendLine("**Alternatives**");
            foreach (var alt in parsed.alternatives) sbOut.AppendLine($"- {alt}");
        }
        if (parsed.gotchas is { Count: > 0 })
        {
            sbOut.AppendLine();
            sbOut.AppendLine("**Gotchas**");
            foreach (var g in parsed.gotchas) sbOut.AppendLine($"- {g}");
        }

        return new CreateAiAnswerDto(
            Content: sbOut.ToString(),
            AiModel: model,
            ConfidenceScore: (float?)parsed.confidence,
            RawAiResponse: text!,
            PromptUsed: userContent.ToString(),
            Diagnosis: sbDiagnosis.ToString(),
            LikelyRootCause:  sbRootCause.ToString(),
            FixStepByStep: sbFixSteps.ToString(),
            CodePatch: sbCodePatch.ToString(),
            Alternatives: sbAlternatives.ToString(),
            Gotchas: sbGotchas.ToString()
        );
    }
}
