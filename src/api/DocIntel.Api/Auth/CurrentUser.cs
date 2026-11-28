using System.Security.Claims;

namespace DocIntel.Api.Auth;

// Group keys used in the database. The token carries Entra group object IDs;
// GroupMap (configuration section "Groups") translates them, so no tenant-specific
// ID is stored in the database or the repo.
public static class GroupKeys
{
    public const string Readers = "readers";
    public const string Contributors = "contributors";
    public const string Reviewers = "reviewers";
    public const string Administrators = "administrators";
    public const string Finance = "finance";
    public const string HR = "hr";
}

// Configuration "Groups": key -> Entra group object ID, e.g. Groups__contributors.
public sealed class GroupMap : Dictionary<string, string>
{
    public GroupMap() : base(StringComparer.OrdinalIgnoreCase) { }
}

public sealed record CurrentUser(string Oid, string Name, IReadOnlySet<string> GroupKeys)
{
    public bool IsAdministrator => GroupKeys.Contains(Auth.GroupKeys.Administrators);
    public bool IsContributor => GroupKeys.Contains(Auth.GroupKeys.Contributors);

    // M1 read rule: one of the document's groups, or an administrator.
    public bool CanRead(IEnumerable<string> documentGroups) =>
        IsAdministrator || documentGroups.Any(GroupKeys.Contains);

    public static CurrentUser From(ClaimsPrincipal principal, GroupMap map)
    {
        var oid = principal.FindFirstValue("oid") ?? throw new InvalidOperationException("Token has no oid claim.");
        var name = principal.FindFirstValue("name") ?? principal.FindFirstValue("preferred_username") ?? oid;
        var tokenGroups = principal.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = map.Where(kv => tokenGroups.Contains(kv.Value)).Select(kv => kv.Key.ToLowerInvariant()).ToHashSet();
        return new CurrentUser(oid, name, keys);
    }
}
