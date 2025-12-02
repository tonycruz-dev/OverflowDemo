using QuestionService.Clients.AI;
using QuestionService.DTOs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QuestionService.Extensions;

public class GroqAIAnswer(IConfiguration configuration, ILogger<GroqAIAnswer> logger) : IGroqAIAnswer
{
 private readonly ILogger<GroqAIAnswer> _logger = logger;
 private readonly IConfiguration _config = configuration;

 private static readonly JsonSerializerOptions CachedJsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};

    public async Task<CreateAiAnswerDto?> OpenAIAnswerCodeErrorAsync(
    string title,
    string problemStatement,
    string model,
    CancellationToken ct = default)
    {
        // Basic guard
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        // API key (prefer config/env)
        var apiKey = _config["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GROQ_API_KEY env var not set. Using placeholder will cause 401.");
            apiKey = "REPLACE_ME";
        }

        // Pick a Groq model suitable for coding answers
        var url = "https://api.groq.com/openai/v1/chat/completions";

        // Build an instruction that yields a Stack Overflow–style answer
        var userContent = new StringBuilder();
        userContent.AppendLine("# Problem Title");
        userContent.AppendLine(string.IsNullOrWhiteSpace(title) ? "Untitled issue" : title.Trim());
        userContent.AppendLine();
        userContent.AppendLine("# Problem Statement");
        userContent.AppendLine(problemStatement?.Trim() ?? "");
        userContent.AppendLine();


        var payload = new
        {
            model,
            messages = new[]
            {
            new { role = "system", content =
                "You are a senior engineer writing Stack Overflow–quality answers. " +
                "Provide: diagnosis, likely root cause, step-by-step fix, a minimal code patch (or config change), " +
                "and brief alternative approaches when relevant. Be precise, concise, and actionable."
            },
            new { role = "user", content = userContent.ToString() }
        },
            temperature = 0.1,
            max_tokens = 4096,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "so_style_answer",
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            summary = new { type = "string", description = "One-paragraph executive summary of the fix." },
                            diagnosis = new { type = "string", description = "What is happening and why." },
                            rootCause = new { type = "string", description = "The most likely root cause." },
                            fixSteps = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Ordered, clear steps to fix."
                            },
                            codePatch = new { type = "string", description = "Unified diff or full corrected snippet if diff is impractical." },
                            alternatives = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Other viable approaches or mitigations."
                            },
                            gotchas = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Common pitfalls, version quirks, or caveats."
                            },
                            confidence = new { type = "number", description = "0..1 confidence score for this answer." }
                        },
                        required = new[] { "summary", "diagnosis", "rootCause", "fixSteps", "codePatch", "confidence" },
                        additionalProperties = false
                    }
                }
            }
        };

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await PostJsonWithDiagnosticsAsync(client, url, payload, ct);
        if (response == null) return null;

        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(raw);

            // Groq returns content at choices[0].message.content
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Define a local record to parse the schema
            var opts = CachedJsonOptions;

            var parsed = JsonSerializer.Deserialize<SoStyleAnswer>(content!, opts);
            if (parsed == null) return null;


            // I need to return diagnosis, root cause, fix steps, code patch, alternatives, gotchas, confidence

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


            // Compose a Stack Overflow–style final answer body
            var sb = new StringBuilder();
            sb.AppendLine(parsed.summary?.Trim() ?? "");
            sb.AppendLine();
            sb.AppendLine("**Diagnosis**");
            sb.AppendLine(parsed.diagnosis?.Trim() ?? "");
            sb.AppendLine();
            sb.AppendLine("**Likely root cause**");
            sb.AppendLine(parsed.rootCause?.Trim() ?? "");
            sb.AppendLine();
            sb.AppendLine("**Fix (step-by-step)**");
            for (int i = 0; i < (parsed.fixSteps?.Count ?? 0); i++)
                sb.AppendLine($"{i + 1}. {parsed.fixSteps![i]}");
            if (!string.IsNullOrWhiteSpace(parsed.codePatch))
            {
                sb.AppendLine();
                sb.AppendLine("**Code patch**");
                sb.AppendLine("```diff");
                sb.AppendLine(parsed.codePatch?.Trim() ?? "");
                sb.AppendLine("```");
            }
            if (parsed.alternatives is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("**Alternatives**");
                foreach (var alt in parsed.alternatives) sb.AppendLine($"- {alt}");
            }
            if (parsed.gotchas is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("**Gotchas**");
                foreach (var g in parsed.gotchas) sb.AppendLine($"- {g}");
            }

            // Map into your existing AnswerByAI DTO
            var answer = new CreateAiAnswerDto(
                sb.ToString(),         // Content
                model,                 // AiModel
                (float?)parsed.confidence, // ConfidenceScore
                content,               // RawAiResponse
                userContent.ToString(), // PromptUsed
                sbDiagnosis.ToString(), // Diagnosis
                sbRootCause.ToString(), // LikelyRootCause
                sbFixSteps.ToString(),  // FixStepByStep
                sbCodePatch.ToString(), // CodePatch
                sbAlternatives.ToString(), // Alternatives
                sbGotchas.ToString()     // Gotchas
            );

            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq JSON response");
            return null;
        }
    }
    private async Task<HttpResponseMessage?> PostJsonWithDiagnosticsAsync(
       HttpClient client, string url, object payload, CancellationToken ct = default)
    {
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Calling Groq endpoint: {Url}", url);
            var resp = await client.PostAsync(url, content, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogError("Request failed: {Status} {Reason}. Body: {Body}", (int)resp.StatusCode, resp.ReasonPhrase, body);
                return null; // prevents null-ref in caller
            }

            return resp;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Groq request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Groq API");
            return null;
        }
    }

    public async Task<CreateAiAnswerDto?> QwenAnswerCodeErrorAsync(
    string title,
    string problemStatement,
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        // API key
        var apiKey = _config["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GROQ_API_KEY env var not set. Using placeholder will cause 401.");
            apiKey = "REPLACE_ME";
        }

        // Qwen on Groq (you can swap to other Groq models)
        const string model = "qwen/qwen3-32b";
        const string url = "https://api.groq.com/openai/v1/chat/completions";

        // Build a SO-style prompt
        var userContent = new StringBuilder();
        userContent.AppendLine("# Problem Title");
        userContent.AppendLine(string.IsNullOrWhiteSpace(title) ? "Untitled issue" : title.Trim());
        userContent.AppendLine();
        userContent.AppendLine("# Problem Statement");
        userContent.AppendLine(problemStatement.Trim());
        userContent.AppendLine();
      

        var baseReq = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new[]
            {
            new { role = "system", content =
                "You are a senior engineer writing Stack Overflow–quality answers. " +
                "Return JSON ONLY (no markdown). Provide: summary, diagnosis, rootCause, " +
                "fixSteps (array), codePatch (string), alternatives (array), gotchas (array), confidence (0..1). " +
                "Be precise, concise, and actionable."
            },
            new { role = "user", content = userContent.ToString() }
        },
            ["temperature"] = 0.1,
            ["max_tokens"] = 4096
        };

        // Attempt 1: strict JSON schema
        var req1 = new Dictionary<string, object?>(baseReq)
        {
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "so_style_answer",
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            summary = new { type = "string" },
                            diagnosis = new { type = "string" },
                            rootCause = new { type = "string" },
                            fixSteps = new { type = "array", items = new { type = "string" } },
                            codePatch = new { type = "string" },
                            alternatives = new { type = "array", items = new { type = "string" } },
                            gotchas = new { type = "array", items = new { type = "string" } },
                            confidence = new { type = "number" }
                        },
                        required = new[] { "summary", "diagnosis", "rootCause", "fixSteps", "codePatch", "confidence" },
                        additionalProperties = false
                    }
                }
            }
        };

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Try strict → json_object → plain
        var parsed = await TryCallAndParseAsync(client, url, req1, ct);
        if (parsed is null)
        {
            var req2 = new Dictionary<string, object?>(baseReq)
            {
                ["response_format"] = new { type = "json_object" }
            };
            parsed = await TryCallAndParseAsync(client, url, req2, ct)
                  ?? await TryCallAndParseAsync(client, url, baseReq, ct);
        }

        if (parsed is null) return null;

        // I need to return diagnosis, root cause, fix steps, code patch, alternatives, gotchas, confidence

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

        // Compose final markdown content for UI
        var sb = new StringBuilder();
        sb.AppendLine(parsed.summary.Trim());
        sb.AppendLine();
        sb.AppendLine("**Diagnosis**");
        sb.AppendLine(parsed.diagnosis?.Trim() ?? "");
        sb.AppendLine();
        sb.AppendLine("**Likely root cause**");
        sb.AppendLine(parsed.rootCause?.Trim() ?? "");
        sb.AppendLine();
        sb.AppendLine("**Fix (step-by-step)**");
        if (parsed.fixSteps is { Count: > 0 })
        {
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sb.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
        }
        if (!string.IsNullOrWhiteSpace(parsed.codePatch))
        {
            sb.AppendLine();
            sb.AppendLine("**Code patch**");
            sb.AppendLine("```diff");
            sb.AppendLine(parsed.codePatch.Trim());
            sb.AppendLine("```");
        }
        if (parsed.alternatives is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("**Alternatives**");
            foreach (var alt in parsed.alternatives) sb.AppendLine($"- {alt}");
        }
        if (parsed.gotchas is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("**Gotchas**");
            foreach (var g in parsed.gotchas) sb.AppendLine($"- {g}");
        }

        // Return your CreateAiAnswerDto
        return new CreateAiAnswerDto(
            Content: sb.ToString(),
            AiModel: model,
            ConfidenceScore: (float?)parsed.confidence,
            RawAiResponse: parsed.__raw ?? "",       // full JSON from model if available
            PromptUsed: userContent.ToString(),
            Diagnosis:  sbDiagnosis.ToString(),
            LikelyRootCause: sbRootCause.ToString(),
            FixStepByStep: sbFixSteps.ToString(),
            CodePatch: sbCodePatch.ToString(),
            Alternatives: sbAlternatives.ToString(),
            Gotchas: sbGotchas.ToString()
        );

        // ---------------- helpers ----------------
        async Task<SoStyleAnswer?> TryCallAndParseAsync(HttpClient http, string url, object payload, CancellationToken ct)
        {
            try
            {
                var resp = await PostJsonWithDiagnosticsAsync(http, url, payload, ct);
                if (resp == null) return null;

                var raw = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);

                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content)) return null;

                content = StripCodeFences(content);

                var opts = CachedJsonOptions;

                var parsed = JsonSerializer.Deserialize<SoStyleAnswer>(content, opts);
                if (parsed is null) return null;

                // stash raw for auditing (optional)
                parsed.__raw = content;
                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse answer content");
                return null;
            }
        }

        string StripCodeFences(string s)
        {
            s = s.Trim();
            if (s.StartsWith("```") )
            {
                var firstNewline = s.IndexOf('\n');
                if (firstNewline >= 0) s = s[(firstNewline + 1)..].Trim();
                if (s.EndsWith("```") ) s = s[..^3].Trim();
            }
            return s;
        }
    }

}
