namespace DocIntel.Api.Content;

// Accepted formats: non-proprietary only for now (decision log). The content type
// comes from the extension, never from what the browser claims.
public static class FileRules
{
    public const long MaxBytes = 25 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".html"] = "text/html",
        [".xml"] = "application/xml",
        [".json"] = "application/json",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".odg"] = "application/vnd.oasis.opendocument.graphics",
    };

    public static IEnumerable<string> Extensions => ContentTypes.Keys;

    // Returns an error message, or null when the file is acceptable.
    public static string? Check(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Choose a file to check in.";
        if (file.Length > MaxBytes) return $"The file is larger than {MaxBytes / (1024 * 1024)} MB.";
        if (!ContentTypes.ContainsKey(Path.GetExtension(file.FileName)))
        {
            return $"This format is not accepted. Use one of: {string.Join(", ", ContentTypes.Keys)}.";
        }
        return null;
    }

    public static string ContentTypeFor(string fileName) => ContentTypes[Path.GetExtension(fileName)];

    // Only the name, never a client-supplied path.
    public static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
