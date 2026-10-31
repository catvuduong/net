using Dapper;
using Npgsql;

namespace Api;

public sealed record SeedBookmark(string Title, string Url, string[] Tags, DateTime CreatedAt);

/// <summary>Sample bookmarks so a fresh database has something to show. Re-applying is a no-op: rows are keyed by URL.</summary>
public static class BookmarkSeed
{
    private static DateTime Day(int day) => new(2026, 10, day, 9, 0, 0, DateTimeKind.Utc);

    public static readonly SeedBookmark[] Items =
    [
        new("ASP.NET Core minimal APIs", "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis", ["dotnet", "api", "docs"], Day(2)),
        new("Dapper", "https://github.com/DapperLib/Dapper", ["dotnet", "database", "open-source"], Day(4)),
        new("Npgsql documentation", "https://www.npgsql.org/doc/index.html", ["dotnet", "postgres", "database"], Day(5)),
        new("PostgreSQL 16 documentation", "https://www.postgresql.org/docs/16/index.html", ["postgres", "database", "docs"], Day(6)),
        new("React: Learn", "https://react.dev/learn", ["frontend", "react", "docs"], Day(8)),
        new("Vite guide", "https://vite.dev/guide/", ["frontend", "tooling", "build"], Day(9)),
        new("CSS Grid Layout", "https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_grid_layout", ["frontend", "css", "docs"], Day(12)),
        new("Playwright: Getting started", "https://playwright.dev/docs/intro", ["testing", "e2e", "frontend"], Day(14)),
        new("xUnit.net", "https://xunit.net/", ["testing", "dotnet"], Day(15)),
        new("Docker Compose overview", "https://docs.docker.com/compose/", ["devops", "tooling", "docker"], Day(19)),
        new("Vercel documentation", "https://vercel.com/docs", ["hosting", "frontend", "docs"], Day(22)),
        new("OWASP Top Ten", "https://owasp.org/www-project-top-ten/", ["security", "reading"], Day(26)),
    ];

    /// <summary>Inserts any sample bookmark whose URL is not stored yet. Run inside the schema transaction.</summary>
    public static async Task ApplyAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        foreach (var item in Items)
        {
            await conn.ExecuteAsync("""
                INSERT INTO bookmarks (title, url, tags, created_at)
                SELECT @Title, @Url, @Tags, @CreatedAt
                WHERE NOT EXISTS (SELECT 1 FROM bookmarks WHERE url = @Url)
                """, new { item.Title, item.Url, Tags = item.Tags, item.CreatedAt }, tx);
        }
    }
}
