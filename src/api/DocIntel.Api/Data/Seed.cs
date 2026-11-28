using DocIntel.Api.Auth;
using DocIntel.Api.Content;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Data;

// Starter categories until categories can be managed (UC-30, M6). Default groups
// are group keys, not Entra object IDs: the IDs are tenant-specific and stay out
// of the repo (see GroupMap).
internal static class Seed
{
    public static void Categories(ModelBuilder model)
    {
        model.Entity<Category>().HasData(
            new Category { Id = 1, Name = "General" },
            new Category { Id = 2, Name = "Policies" },
            new Category { Id = 3, Name = "Finance" },
            new Category { Id = 4, Name = "HR" });

        model.Entity<CategoryDefaultGroup>().HasData(
            new CategoryDefaultGroup { CategoryId = 1, GroupKey = GroupKeys.Readers },
            new CategoryDefaultGroup { CategoryId = 1, GroupKey = GroupKeys.Contributors },
            new CategoryDefaultGroup { CategoryId = 2, GroupKey = GroupKeys.Readers },
            new CategoryDefaultGroup { CategoryId = 2, GroupKey = GroupKeys.Contributors },
            new CategoryDefaultGroup { CategoryId = 3, GroupKey = GroupKeys.Finance },
            new CategoryDefaultGroup { CategoryId = 4, GroupKey = GroupKeys.HR });
    }
}
