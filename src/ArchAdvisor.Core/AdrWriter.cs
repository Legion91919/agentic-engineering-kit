using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchAdvisor.Core;

public sealed record AdrInput(
    string Title,
    string Context,
    string Decision,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> Consequences,
    string Status = "Accepted");

/// <summary>Writes Architecture Decision Records in the lightweight Nygard format.</summary>
public static partial class AdrWriter
{
    public static string FileName(int number, string title) =>
        string.Create(CultureInfo.InvariantCulture, $"{number:0000}-{Slug(title)}.md");

    /// <summary>Next free ADR number given the file names already in the decisions folder.</summary>
    public static int NextNumber(IEnumerable<string> existingFileNames)
    {
        int max = 0;
        foreach (var name in existingFileNames)
        {
            var m = LeadingNumber().Match(Path.GetFileName(name));
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) max = Math.Max(max, n);
        }
        return max + 1;
    }

    public static string Render(int number, AdrInput input, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ArgumentException("An ADR needs a title.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Decision)) throw new ArgumentException("An ADR needs a decision.", nameof(input));

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {number:0000}. {input.Title.Trim()}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Status: {input.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Date: {date:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## Context");
        sb.AppendLine();
        sb.AppendLine(input.Context.Trim());
        sb.AppendLine();
        sb.AppendLine("## Decision");
        sb.AppendLine();
        sb.AppendLine(input.Decision.Trim());
        sb.AppendLine();
        sb.AppendLine("## Alternatives considered");
        sb.AppendLine();
        if (input.Alternatives.Count == 0) sb.AppendLine("- None recorded.");
        foreach (var a in input.Alternatives) sb.AppendLine(CultureInfo.InvariantCulture, $"- {a.Trim()}");
        sb.AppendLine();
        sb.AppendLine("## Consequences");
        sb.AppendLine();
        if (input.Consequences.Count == 0) sb.AppendLine("- None recorded.");
        foreach (var c in input.Consequences) sb.AppendLine(CultureInfo.InvariantCulture, $"- {c.Trim()}");
        return sb.ToString();
    }

    internal static string Slug(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var slug = NonAlphaNum().Replace(lower, "-").Trim('-');
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }

    [GeneratedRegex(@"^(\d{1,4})-")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNum();
}
