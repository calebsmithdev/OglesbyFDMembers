using System.Text.RegularExpressions;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OglesbyFDMembers.App.Services;

public interface IPdfVvecExtractor
{
    Task<List<UtilityRow>> ExtractAsync(Stream pdfStream, CancellationToken ct = default);
}

public sealed class PdfVvecExtractor : IPdfVvecExtractor
{
    public async Task<List<UtilityRow>> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var rows = new List<UtilityRow>();

        using var doc = PdfDocument.Open(ms);
        foreach (var page in doc.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var words = page.GetWords();
            if (words is null || !words.Any()) continue;

            var lines = GroupWordsIntoLines(words.ToList(), yTolerance: 3.0);

            foreach (var line in lines)
            {
                var fields = SplitIntoFields(line, gapThreshold: 20.0);
                if (fields.Count < 4) continue;
                if (IsHeaderOrFooter(fields)) continue;

                var name = fields[2].Trim(); // 3rd column
                var amountRaw = fields[3];   // 4th column
                var amount = DollarsToDecimal(amountRaw);

                if (!string.IsNullOrWhiteSpace(name) && amount.HasValue && amount.Value > 0)
                {
                    rows.Add(new UtilityRow { Name = name, Amount = amount.Value });
                }
            }
        }

        // Deduplicate exact (Name, Amount) while preserving order
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<UtilityRow>();
        foreach (var r in rows)
        {
            var key = r.Name + "\n" + r.Amount.ToString("0.00");
            if (seen.Add(key)) unique.Add(r);
        }

        return unique;
    }

    private static List<List<Word>> GroupWordsIntoLines(IReadOnlyList<Word> words, double yTolerance)
    {
        var sorted = words
            .OrderBy(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<List<Word>>();
        foreach (var w in sorted)
        {
            bool placed = false;
            foreach (var line in lines)
            {
                var avgBottom = line.Average(x => x.BoundingBox.Bottom);
                if (Math.Abs(w.BoundingBox.Bottom - avgBottom) <= yTolerance)
                {
                    line.Add(w);
                    placed = true;
                    break;
                }
            }
            if (!placed) lines.Add(new List<Word> { w });
        }
        foreach (var l in lines) l.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
        return lines;
    }

    private static List<string> SplitIntoFields(List<Word> lineWords, double gapThreshold)
    {
        var fields = new List<string>();
        if (lineWords.Count == 0) return fields;

        var current = new List<string> { lineWords[0].Text };
        double lastRight = lineWords[0].BoundingBox.Right;
        for (int i = 1; i < lineWords.Count; i++)
        {
            var w = lineWords[i];
            var gap = w.BoundingBox.Left - lastRight;
            if (gap > gapThreshold)
            {
                fields.Add(string.Join(" ", current));
                current.Clear();
            }
            current.Add(w.Text);
            lastRight = w.BoundingBox.Right;
        }
        fields.Add(string.Join(" ", current));
        return fields;
    }

    private static bool IsHeaderOrFooter(List<string> fields)
    {
        var joined = string.Join(" ", fields).ToUpperInvariant();
        if (joined.Contains("PAGE ") || joined.Contains("TOTALS")) return true;
        if (joined.Contains("LAST NAME") || joined.Contains("FIRST NAME") || joined.Contains("AMOUNT")) return true;
        return false;
    }

    private static decimal? DollarsToDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        t = t.Replace('O', '0').Replace('o', '0');
        t = Regex.Replace(t, @",\s*00\b", ".00"); // 7,00 -> 7.00
        t = Regex.Replace(t, @"(?<=\d),(?=\d{3}\b)", ""); // 1,400 -> 1400
        var m = Regex.Match(t, @"(\d+)(?:\.(\d{1,2}))?");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var whole)) return null;
        var cents = 0;
        if (m.Groups[2].Success)
        {
            var f = m.Groups[2].Value.PadRight(2, '0');
            _ = int.TryParse(f, out cents);
        }
        return whole + (cents / 100m);
    }
}
