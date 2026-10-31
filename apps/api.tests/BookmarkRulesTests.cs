using Api;

namespace Api.Tests;

public class BookmarkRulesTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:3001/health")]
    [InlineData("https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0")]
    [InlineData("HTTPS://EXAMPLE.COM/Mixed-Case")]
    public void Http_and_https_urls_are_valid(string url) =>
        Assert.True(BookmarkRules.IsValidHttpUrl(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file.zip")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("https://")]
    [InlineData("not a url")]
    public void Anything_else_is_invalid(string? url) =>
        Assert.False(BookmarkRules.IsValidHttpUrl(url));

    [Fact]
    public void Tags_are_trimmed_lowercased_and_deduplicated_in_order()
    {
        var tags = BookmarkRules.NormalizeTags(["  Docs ", "docs", "", "   ", "API", "dotnet", "api"]);

        Assert.Equal(["docs", "api", "dotnet"], tags);
    }

    [Fact]
    public void Missing_tags_normalize_to_none() =>
        Assert.Empty(BookmarkRules.NormalizeTags(null));

    [Fact]
    public void Tag_counts_are_per_bookmark_ordered_by_count_then_name()
    {
        IEnumerable<string>[] bookmarks =
        [
            ["dotnet", "docs"],
            ["Dotnet", "dotnet", "database"],
            ["docs", "frontend"],
            ["frontend"],
            [],
        ];

        var counts = BookmarkRules.CountTags(bookmarks);

        Assert.Equal(
            [
                new TagCount("docs", 2),
                new TagCount("dotnet", 2),
                new TagCount("frontend", 2),
                new TagCount("database", 1),
            ],
            counts);
    }

    [Fact]
    public void Tag_counts_are_empty_without_bookmarks() =>
        Assert.Empty(BookmarkRules.CountTags([]));

    [Fact]
    public void Valid_request_has_no_errors()
    {
        var errors = BookmarkRules.Validate(new BookmarkRequest("Useful", "https://example.com", ["docs"]));

        Assert.Empty(errors);
    }

    [Fact]
    public void Missing_title_and_url_are_both_reported()
    {
        var errors = BookmarkRules.Validate(new BookmarkRequest(null, null, []));

        Assert.Equal(["Title is required."], errors["title"]);
        Assert.Equal(["URL is required."], errors["url"]);
    }

    [Fact]
    public void Title_longer_than_the_limit_is_rejected()
    {
        var title = new string('x', BookmarkRules.MaxTitleLength + 1);

        var errors = BookmarkRules.Validate(new BookmarkRequest(title, "https://example.com", []));

        Assert.Contains("title", errors.Keys);
    }

    [Fact]
    public void Non_http_url_is_rejected_with_a_url_error()
    {
        var errors = BookmarkRules.Validate(new BookmarkRequest("Shell", "ftp://example.com", []));

        Assert.Contains("url", errors.Keys);
        Assert.DoesNotContain("title", errors.Keys);
    }

    [Fact]
    public void Too_many_tags_are_rejected()
    {
        var tags = Enumerable.Range(0, BookmarkRules.MaxTags + 1).Select(i => $"tag{i}").ToArray();

        var errors = BookmarkRules.Validate(new BookmarkRequest("Many", "https://example.com", tags));

        Assert.Contains("tags", errors.Keys);
    }
}
