using System.Globalization;
using System.Text;

namespace DocIntel.Api.Content.Pdf;

// A small, dependency-free PDF writer for text documents (UC-07). It uses the
// standard PDF fonts (Courier, Helvetica-Bold), which every reader has, so no font
// files are embedded and nothing depends on fonts in the container. Characters
// outside Latin-1 print as '?'. A4 pages, uncompressed content streams.
public sealed class SimplePdf
{
    public enum Style
    {
        Body,     // Courier 10
        Code,     // Courier 9, indented
        Heading1, // Helvetica-Bold 18
        Heading2, // Helvetica-Bold 14
        Heading3, // Helvetica-Bold 12
    }

    private const double PageWidth = 595.28, PageHeight = 841.89, Margin = 56.7;
    private readonly List<StringBuilder> _pages = [];
    private StringBuilder _page = null!;
    private double _y;

    public SimplePdf() => NewPage();

    public void Space(double points)
    {
        _y -= points;
        if (_y < Margin) NewPage();
    }

    // Writes a paragraph, wrapping words to the page width. Courier is monospaced
    // (0.6 em per character); for Helvetica-Bold 0.6 em is a safe upper estimate.
    public void Paragraph(string text, Style style = Style.Body, string prefix = "")
    {
        var (font, size, indent) = style switch
        {
            Style.Heading1 => ("F2", 18.0, 0.0),
            Style.Heading2 => ("F2", 14.0, 0.0),
            Style.Heading3 => ("F2", 12.0, 0.0),
            Style.Code => ("F1", 9.0, 14.0),
            _ => ("F1", 10.0, 0.0),
        };
        var leading = size * 1.35;
        var maxChars = Math.Max(10, (int)((PageWidth - 2 * Margin - indent) / (size * 0.6)));
        var lines = style == Style.Code ? HardWrap(text, maxChars) : Wrap(prefix + text, maxChars, prefix.Length);

        foreach (var line in lines)
        {
            if (_y - leading < Margin) NewPage();
            _y -= leading;
            _page.Append(CultureInfo.InvariantCulture,
                $"BT /{font} {size:0.##} Tf {Margin + indent:0.##} {_y:0.##} Td ({Escape(line)}) Tj ET\n");
        }
    }

    public byte[] ToBytes()
    {
        // Objects: 1 catalog, 2 pages, 3-4 fonts, then a page and its content per page.
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "", // pages, filled below
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };
        var kids = new List<string>();
        foreach (var page in _pages)
        {
            var pageObj = objects.Count + 1;
            kids.Add($"{pageObj} 0 R");
            var content = page.ToString();
            objects.Add(FormattableString.Invariant(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageObj + 1} 0 R >>"));
            objects.Add($"<< /Length {Latin1.GetByteCount(content)} >>\nstream\n{content}endstream");
        }
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {_pages.Count} >>";

        using var output = new MemoryStream();
        void Write(string s) { var b = Latin1.GetBytes(s); output.Write(b); }
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) table.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        Write(table.ToString());
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private void NewPage()
    {
        _page = new StringBuilder();
        _pages.Add(_page);
        _y = PageHeight - Margin;
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '(' or ')') sb.Append('\\').Append(c);
            else if (c == '\t') sb.Append("    ");
            else if (c < 32) continue;
            else if (c > 255) sb.Append('?');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static IEnumerable<string> Wrap(string text, int maxChars, int hangingIndent)
    {
        var line = new StringBuilder();
        var pad = new string(' ', hangingIndent);
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > maxChars - hangingIndent)
            {
                if (line.Length > 0) { yield return line.ToString(); line.Clear().Append(pad); }
                var room = maxChars - line.Length;
                yield return line.Append(w[..room]).ToString();
                line.Clear().Append(pad);
                w = w[room..];
            }
            if (line.Length > 0 && line.Length + 1 + w.Length > maxChars)
            {
                yield return line.ToString();
                line.Clear().Append(pad);
            }
            if (line.Length > 0 && line.ToString() != pad) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0 && line.ToString() != pad) yield return line.ToString();
    }

    private static IEnumerable<string> HardWrap(string text, int maxChars)
    {
        if (text.Length == 0) { yield return ""; yield break; }
        for (var i = 0; i < text.Length; i += maxChars) yield return text.Substring(i, Math.Min(maxChars, text.Length - i));
    }
}
