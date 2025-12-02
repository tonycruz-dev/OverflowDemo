namespace QuestionService.Clients.AI;

sealed class SoStyleAnswer
{
    public string summary { get; set; } = "";
    public string diagnosis { get; set; } = "";
    public string rootCause { get; set; } = "";
    public List<string>? fixSteps { get; set; }
    public string codePatch { get; set; } = "";
    public List<string>? alternatives { get; set; }
    public List<string>? gotchas { get; set; }
    public double confidence { get; set; }
    public string? __raw { get; set; }
}

