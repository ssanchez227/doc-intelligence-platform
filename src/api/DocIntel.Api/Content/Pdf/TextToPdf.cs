using System.Text;
using System.Text.RegularExpressions;

namespace DocIntel.Api.Content.Pdf;

// Turns plain text, CSV and Markdown into a PDF. Markdown gets headings, bullets,
// numbered items, code blocks and paragraphs; inline marks (** _ ` and links) are
// reduced to their text. Good enough for a readable rendition, not a typesetter.
public static partial class TextToPdf
{
    public static bool CanConvert(string contentType) =>
        contentType is "text/plain" or "text/markdown" or "text/csv";

    public static byte[] Convert(string text, string contentType)
    {
        var pdf = new SimplePdf();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (contentType == "text/markdown") Markdown(pdf, lines);
        else foreach (var line in lines) pdf.Paragraph(line, SimplePdf.Style.Code);
        return pdf.ToBytes();
    }

    private static void Markdown(SimplePdf pdf, string[] lines)
    {
        var paragraph = new StringBuilder();
        var inCode = false;

        void Flush()
        {
            if (paragraph.Length == 0) return;
            pdf.Paragraph(Inline(paragraph.ToString()));
            pdf.Space(6);
            paragraph.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                inCode = !inCode;
                pdf.Space(4);
                continue;
            }
            if (inCode) { pdf.Paragraph(raw, SimplePdf.Style.Code); continue; }

            if (line.Length == 0) { Flush(); continue; }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                Flush();
                var level = heading.Groups[1].Value.Length;
                pdf.Space(level == 1 ? 8 : 6);
                pdf.Paragraph(Inline(heading.Groups[2].Value), level switch
                {
                    1 => SimplePdf.Style.Heading1,
                    2 => SimplePdf.Style.Heading2,
                    _ => SimplePdf.Style.Heading3,
                });
                pdf.Space(4);
                continue;
            }

            var bullet = BulletRegex().Match(line);
            if (bullet.Success)
            {
                Flush();
                var marker = char.IsDigit(bullet.Groups[2].Value[0]) ? bullet.Groups[2].Value + " " : "- ";
                pdf.Paragraph(Inline(bullet.Groups[3].Value), prefix: bullet.Groups[1].Value + marker);
                continue;
            }

            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(line.Trim());
        }
        Flush();
    }

    private static string Inline(string text)
    {
        text = LinkRegex().Replace(text, "$1");
        return text.Replace("**", "").Replace("__", "").Replace("`", "");
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();
}
