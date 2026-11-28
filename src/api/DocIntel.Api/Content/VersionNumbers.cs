namespace DocIntel.Api.Content;

// Drafts take the next minor number after the highest existing version (0.1, 0.2;
// after 1.0 comes 1.1). Publishing takes the next major number (1.0, 2.0).
public static class VersionNumbers
{
    public static (int Major, int Minor) First => (0, 1);

    public static (int Major, int Minor) NextMinor(IEnumerable<(int Major, int Minor)> existing)
    {
        var highest = Highest(existing);
        return (highest.Major, highest.Minor + 1);
    }

    public static (int Major, int Minor) NextMajor(IEnumerable<(int Major, int Minor)> existing) =>
        (Highest(existing).Major + 1, 0);

    private static (int Major, int Minor) Highest(IEnumerable<(int Major, int Minor)> existing) =>
        existing.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
            .DefaultIfEmpty((0, 0)).First();
}
