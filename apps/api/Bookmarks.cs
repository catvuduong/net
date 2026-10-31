using Dapper;

namespace Api;

// A class with setters rather than a record: Npgsql reports text[] columns as System.Array, which Dapper cannot match to a constructor parameter.
public sealed class Bookmark
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public sealed record BookmarkRequest(string? Title, string? Url, string[]? Tags);

public sealed record TagCount(string Tag, int Count);

public static class BookmarkRules
{
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxTags = 10;
    public const int MaxTagLength = 30;

    public static bool IsValidHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>Trims, lowercases and de-duplicates tags, dropping empty ones. Order of first occurrence is kept.</summary>
    public static string[] NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? [])
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct()
            .ToArray();

    /// <summary>Counts how many bookmarks carry each tag. Most used first, then alphabetical.</summary>
    public static IReadOnlyList<TagCount> CountTags(IEnumerable<IEnumerable<string>> tagLists) =>
        tagLists
            .SelectMany(tags => NormalizeTags(tags))
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Select(group => new TagCount(group.Key, group.Count()))
            .OrderByDescending(tag => tag.Count)
            .ThenBy(tag => tag.Tag, StringComparer.Ordinal)
            .ToList();

    /// <summary>Validates an already trimmed and normalized request. Returns field errors, empty when valid.</summary>
    public static Dictionary<string, string[]> Validate(BookmarkRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(request.Title))
            errors["title"] = ["Title is required."];
        else if (request.Title.Length > MaxTitleLength)
            errors["title"] = [$"Title must be {MaxTitleLength} characters or fewer."];

        if (string.IsNullOrEmpty(request.Url))
            errors["url"] = ["URL is required."];
        else if (request.Url.Length > MaxUrlLength)
            errors["url"] = [$"URL must be {MaxUrlLength} characters or fewer."];
        else if (!IsValidHttpUrl(request.Url))
            errors["url"] = ["Enter a valid URL starting with http:// or https://."];

        var tags = request.Tags ?? [];
        if (tags.Length > MaxTags)
            errors["tags"] = [$"Use at most {MaxTags} tags."];
        else if (tags.Any(tag => tag.Length > MaxTagLength))
            errors["tags"] = [$"Each tag must be {MaxTagLength} characters or fewer."];

        return errors;
    }
}

public static class BookmarkEndpoints
{
    private const string Columns = "id AS Id, title AS Title, url AS Url, tags AS Tags, created_at AS CreatedAt";

    public static void MapBookmarks(this WebApplication app)
    {
        app.MapGet("/bookmarks", async Task<IResult> (Db db, string? q, string? tag) =>
        {
            var search = string.IsNullOrWhiteSpace(q) ? null : $"%{EscapeLike(q.Trim())}%";
            var tagFilter = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim().ToLowerInvariant();

            await using var conn = await db.OpenAsync();
            var items = await conn.QueryAsync<Bookmark>($"""
                SELECT {Columns}
                FROM bookmarks
                WHERE (CAST(@search AS text) IS NULL OR title ILIKE @search OR url ILIKE @search)
                  AND (CAST(@tag AS text) IS NULL OR CAST(@tag AS text) = ANY(tags))
                ORDER BY created_at DESC, id DESC
                """, new { search, tag = tagFilter });
            return Results.Ok(items);
        });

        app.MapPost("/bookmarks", async Task<IResult> (BookmarkRequest request, Db db) =>
        {
            var input = new BookmarkRequest(request.Title?.Trim(), request.Url?.Trim(), BookmarkRules.NormalizeTags(request.Tags));
            var errors = BookmarkRules.Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await using var conn = await db.OpenAsync();
            var created = await conn.QuerySingleAsync<Bookmark>($"""
                INSERT INTO bookmarks (title, url, tags)
                VALUES (@Title, @Url, @Tags)
                RETURNING {Columns}
                """, new { input.Title, input.Url, Tags = input.Tags });
            return Results.Json(created, statusCode: StatusCodes.Status201Created);
        });

        app.MapDelete("/bookmarks/{id:int}", async Task<IResult> (int id, Db db) =>
        {
            await using var conn = await db.OpenAsync();
            var deleted = await conn.ExecuteAsync("DELETE FROM bookmarks WHERE id = @id", new { id });
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        app.MapGet("/tags", async (Db db) =>
        {
            await using var conn = await db.OpenAsync();
            var rows = await conn.QueryAsync<Bookmark>("SELECT tags AS Tags FROM bookmarks");
            return Results.Ok(BookmarkRules.CountTags(rows.Select(row => row.Tags)));
        });
    }

    /// <summary>Escapes LIKE wildcards so a search for "100%" matches that text instead of everything.</summary>
    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
