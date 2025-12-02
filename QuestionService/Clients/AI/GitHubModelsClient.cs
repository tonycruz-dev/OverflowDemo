using Azure;
using Azure.AI.Inference;
using QuestionService.Clients.AI;
using QuestionService.DTOs;
using System.Text;
using System.Text.Json;

namespace QuestionService.Extensions;

public class GitHubModelsClient(IConfiguration configuration, ILogger<GitHubModelsClient> logger) : IGitHubModelsClient
{
    private readonly IConfiguration _config = configuration;
    private readonly ILogger<GitHubModelsClient> _logger = logger;
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<CreateAiAnswerDto?> GitHubModelsAnswerCodeErrorAsync(
   string title,
   string problemStatement,
   string model = "gpt-4.1",     // use a model you actually have access to (see /models)
   CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        // Allow env override for model if caller passed blank
        if (string.IsNullOrWhiteSpace(model))
        {
                model = "gpt-4.1"; // final fallback
        }

        var apiKey = _config["Github:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GitHub Models token missing. Set GITHUB_TOKEN (fine-grained PAT with models access). Returning null.");
            return null;
        }

        // Endpoint: Azure Inference front-door for GitHub Models
        var endpoint = new Uri("https://models.inference.ai.azure.com/");
        var client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(apiKey));

        // Build the prompt (system + user)
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

        var options = new ChatCompletionsOptions
        {
            Model = model, // use caller-specified model, not hard-coded value
            Temperature = 0.1f,
            Messages =
        {
            new ChatRequestSystemMessage(sys),
            new ChatRequestUserMessage(userContent.ToString())
        }
        };

        Response<ChatCompletions> resp;
        try
        {
            resp = await client.CompleteAsync(options, ct);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "GitHub Models request failed: {Status} {ErrorCode}", ex.Status, ex.ErrorCode);
            if (ex.ErrorCode == "unknown_model" || ex.Message.Contains("Unknown model", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Hint: List available models with: curl -H 'Authorization: Bearer <TOKEN>' https://models.inference.ai.azure.com/models. Use the exact id (e.g. 'gpt-4.1-mini').");
            }
            return null;
        }

        // Extract assistant text
        var text = resp.Value?.Content;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Sometimes models may still wrap JSON in markdown fences or prose; strip to first '{' .. last '}' pair.
        static string ExtractJson(string raw)
        {
            int first = raw.IndexOf('{');
            int last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return raw[first..(last + 1)];
            }
            return raw; // fallback
        }
        var jsonCandidate = ExtractJson(text);

        // Parse JSON into your schema class
        var jsonOpts = CachedJsonOptions;

        SoStyleAnswer? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SoStyleAnswer>(jsonCandidate, jsonOpts);
            if (parsed == null) return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse model JSON. Raw output: {Output}", text);
            return null;
        }
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
        // Compose Markdown body for display
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
        if (parsed.fixSteps is { Count: > 0 })
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sb.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
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

        return new CreateAiAnswerDto(
            Content: sb.ToString(),
            AiModel: model,
            ConfidenceScore: (float?)parsed.confidence,
            RawAiResponse: text!,
            PromptUsed: userContent.ToString(),
            Diagnosis:  sbDiagnosis.ToString(),
            LikelyRootCause: sbRootCause.ToString(),
            FixStepByStep: sbFixSteps.ToString(),
            CodePatch: sbCodePatch.ToString(),
            Alternatives: sbAlternatives.ToString(),
            Gotchas: sbGotchas.ToString()
        );
    }


    public async Task<CreateAiAnswerDto?> DeepSeekModelsAnswerCodeErrorAsync(
        string title,
        string problemStatement,
        string model = "DeepSeek-V3-0324",     // use a model you actually have access to (see /models)
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        // Allow env override for model if caller passed blank
        if (string.IsNullOrWhiteSpace(model))
        {
                model = "DeepSeek-V3-0324"; // final fallback
        }

        var apiKey = _config["Github:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GitHub Models token missing. Set GITHUB_TOKEN (fine-grained PAT with models access). Returning null.");
            return null;
        }

        // Endpoint: Azure Inference front-door for GitHub Models
        var endpoint = new Uri("https://models.inference.ai.azure.com/");
        var client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(apiKey));

        // Build the prompt (system + user)
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

        var options = new ChatCompletionsOptions
        {
            Model = model, // use caller-specified model, not hard-coded value
            Temperature = 0.1f,
            Messages =
        {
            new ChatRequestSystemMessage(sys),
            new ChatRequestUserMessage(userContent.ToString())
        }
        };

        Response<ChatCompletions> resp;
        try
        {
            resp = await client.CompleteAsync(options, ct);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "GitHub Models request failed: {Status} {ErrorCode}", ex.Status, ex.ErrorCode);
            if (ex.ErrorCode == "unknown_model" || ex.Message.Contains("Unknown model", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Hint: List available models with: curl -H 'Authorization: Bearer <TOKEN>' https://models.inference.ai.azure.com/models. Use the exact id (e.g. 'gpt-4.1-mini').");
            }
            return null;
        }

        // Extract assistant text
        var text = resp.Value?.Content;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Sometimes models may still wrap JSON in markdown fences or prose; strip to first '{' .. last '}' pair.
        string ExtractJson(string raw)
        {
            int first = raw.IndexOf('{');
            int last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return raw[first..(last + 1)];
            }
            return raw; // fallback
        }
        var jsonCandidate = ExtractJson(text);

        // Parse JSON into your schema class
        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        SoStyleAnswer? parsed;
        try
        {
            // JsonDocument-based manual deserialization (more control over parsing)
            using var doc = JsonDocument.Parse(jsonCandidate);
            var root = doc.RootElement;
            parsed = new SoStyleAnswer
            {
                summary = root.TryGetProperty("summary", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                diagnosis = root.TryGetProperty("diagnosis", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                rootCause = root.TryGetProperty("rootCause", out var rc) ? rc.GetString() ?? string.Empty : string.Empty,
            };

            if (root.TryGetProperty("fixSteps", out var fix) && fix.ValueKind == JsonValueKind.Array)
            {
                parsed.fixSteps = new List<string>();
                foreach (var item in fix.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) parsed.fixSteps.Add(item.GetString()!);
                }
            }

            if (root.TryGetProperty("codePatch", out var cp))
            {
                if (cp.ValueKind == JsonValueKind.String)
                {
                    parsed.codePatch = cp.GetString();
                }
                else if (cp.ValueKind == JsonValueKind.Object)
                {
                    var before = cp.TryGetProperty("before", out var b) ? b.GetString() : null;
                    var after = cp.TryGetProperty("after", out var a) ? a.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(before) || !string.IsNullOrWhiteSpace(after))
                    {
                        // Produce a simple diff-style patch representation.
                        var sbPatch = new StringBuilder();
                        sbPatch.AppendLine("--- before");
                        sbPatch.AppendLine("+++ after");
                        if (!string.IsNullOrWhiteSpace(before)) sbPatch.AppendLine("-" + before!.Trim());
                        if (!string.IsNullOrWhiteSpace(after)) sbPatch.AppendLine("+" + after!.Trim());
                        parsed.codePatch = sbPatch.ToString();
                    }
                }
            }

            if (root.TryGetProperty("alternatives", out var alts) && alts.ValueKind == JsonValueKind.Array)
            {
                parsed.alternatives = new List<string>();
                foreach (var item in alts.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) parsed.alternatives.Add(item.GetString()!);
                }
            }

            if (root.TryGetProperty("gotchas", out var got) && got.ValueKind == JsonValueKind.Array)
            {
                parsed.gotchas = new List<string>();
                foreach (var item in got.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) parsed.gotchas.Add(item.GetString()!);
                }
            }

            if (root.TryGetProperty("confidence", out var conf))
            {
                if (conf.ValueKind == JsonValueKind.Number && conf.TryGetDouble(out var dbl))
                    parsed.confidence = dbl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse model JSON (DeepSeek). Raw output: {Output}", text);
            return null;
        }

        if (parsed == null) return null;

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
        // Compose Markdown body for display
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
        if (parsed.fixSteps is { Count: > 0 })
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sb.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
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
            sb.AppendLine("**Gotchas");
            foreach (var g in parsed.gotchas) sb.AppendLine($"- {g}");
        }

        return new CreateAiAnswerDto(
          Content: sb.ToString(),
          AiModel: model,
          ConfidenceScore: (float?)parsed.confidence,
          RawAiResponse: text!,
          PromptUsed: userContent.ToString(),
          Diagnosis: sbDiagnosis.ToString(),
          LikelyRootCause: sbRootCause.ToString(),
          FixStepByStep: sbFixSteps.ToString(),
          CodePatch: sbCodePatch.ToString(),
          Alternatives: sbAlternatives.ToString(),
          Gotchas: sbGotchas.ToString()
      );
    }


    public async Task<CreateAiAnswerDto?> GitHubGPT5ModelsAnswerCodeErrorAsync(
    string title,
    string problemStatement,
    string model = "gpt-5-chat",     // use a model you actually have access to (see /models)
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStatement))
            return null;

        // Allow env override for model if caller passed blank
        if (string.IsNullOrWhiteSpace(model))
        {
                model = "gpt-5-chat"; // final fallback
        }

        var apiKey = _config["Github:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GitHub Models token missing. Set GITHUB_TOKEN (fine-grained PAT with models access). Returning null.");
            return null;
        }

        // Endpoint: Azure Inference front-door for GitHub Models
        var endpoint = new Uri("https://models.inference.ai.azure.com/");
        var client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(apiKey));

        // Build the prompt (system + user)
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

        // Some models (e.g. gpt-5-chat preview) only accept default temperature; handle gracefully.
        var options = new ChatCompletionsOptions
        {
            Model = model,
            Messages =
        {
            new ChatRequestSystemMessage(sys),
            new ChatRequestUserMessage(userContent.ToString())
        }
        };
        // Try a low temperature first only if model name suggests it supports tuning.
        bool attemptedRetry = false;
        if (!model.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase))
        {
            options.Temperature = 0.1f;
        }

        Response<ChatCompletions> resp;
        try
        {
            resp = await client.CompleteAsync(options, ct);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "unsupported_value" && ex.Message.Contains("temperature", StringComparison.OrdinalIgnoreCase) && !attemptedRetry)
        {
            attemptedRetry = true;
            _logger.LogWarning("Temperature unsupported for model {Model}; retrying with default.", model);
            var retryOptions = new ChatCompletionsOptions
            {
                Model = model,
                Messages =
            {
                new ChatRequestSystemMessage(sys),
                new ChatRequestUserMessage(userContent.ToString())
            }
            };
            resp = await client.CompleteAsync(retryOptions, ct);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "GitHub Models request failed: {Status} {ErrorCode}", ex.Status, ex.ErrorCode);
            if (ex.ErrorCode == "unknown_model" || ex.Message.Contains("Unknown model", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Hint: List available models with: curl -H 'Authorization: Bearer <TOKEN>' https://models.inference.ai.azure.com/models. Use the exact id.");
            }
            return null;
        }

        var text = resp.Value?.Content;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        static string ExtractJson(string raw)
        {
            int first = raw.IndexOf('{');
            int last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return raw[first..(last + 1)];
            }
            return raw;
        }
        var jsonCandidate = ExtractJson(text);

        var jsonOpts = CachedJsonOptions;

        SoStyleAnswer? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SoStyleAnswer>(jsonCandidate, jsonOpts);
            if (parsed == null) return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse model JSON. Raw output: {Output}", text);
            return null;
        }

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
        if (parsed.fixSteps is { Count: > 0 })
            for (int i = 0; i < parsed.fixSteps.Count; i++)
                sb.AppendLine($"{i + 1}. {parsed.fixSteps[i]}");
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

        return new CreateAiAnswerDto(
            Content: sb.ToString(),
            AiModel: model,
            ConfidenceScore: (float?)parsed.confidence,
            RawAiResponse: text!,
            PromptUsed: userContent.ToString(),
            Diagnosis:  sbDiagnosis.ToString(),
            LikelyRootCause: sbRootCause.ToString(),
            FixStepByStep: sbFixSteps.ToString(),
            CodePatch: sbCodePatch.ToString(),
            Alternatives: sbAlternatives.ToString(),
            Gotchas: sbGotchas.ToString()
        );
    }
}
