namespace QuestionService.Extensions;

static class StringExtensions
{
 public static string Truncate(this string? value, int max)
 => string.IsNullOrEmpty(value) ? string.Empty : (value!.Length <= max ? value : value[..max] + "...");
}

