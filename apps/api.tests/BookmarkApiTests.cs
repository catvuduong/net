using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

// Runs against the real database (DATABASE_URL). Each test uses its own random token so it never depends on seed data or other tests,
// and every bookmark a test creates is deleted again, so the app's list (and its screenshot) stays clean.
public class BookmarkApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly List<int> _created = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var id in _created) await _client.DeleteAsync($"/bookmarks/{id}");
    }

    [Fact]
    public async Task Created_bookmark_has_normalized_tags_and_appears_in_the_list()
    {
        var token = Token();

        var response = await PostBookmark($"Article {token}", $"https://example.com/{token}", [" Docs ", "DOCS", "e2e"]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<Bookmark>())!;
        _created.Add(created.Id);
        Assert.Equal(["docs", "e2e"], created.Tags);
        Assert.Contains(await ListAsync($"?q={token}"), item => item.Id == created.Id);
    }

    [Fact]
    public async Task Missing_title_returns_400_with_a_title_error()
    {
        var response = await PostBookmark("   ", "https://example.com/no-title");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", await ErrorFields(response));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("example.com")]
    public async Task Non_http_url_returns_400_with_a_url_error(string url)
    {
        var response = await PostBookmark("Bad link", url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("url", await ErrorFields(response));
    }

    [Fact]
    public async Task Every_invalid_field_is_reported_together()
    {
        var response = await PostBookmark("", "nope", [new string('t', BookmarkRules.MaxTagLength + 1)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new[] { "tags", "title", "url" }, (await ErrorFields(response)).Order());
    }

    [Fact]
    public async Task Tag_filter_returns_only_matching_bookmarks_newest_first()
    {
        var tag = $"filter{Token()}";
        var first = await CreateAsync($"One {tag}", $"https://example.com/one/{tag}", [tag]);
        var other = await CreateAsync($"Unrelated {tag}", $"https://example.com/other/{tag}", ["something-else"]);
        var second = await CreateAsync($"Two {tag}", $"https://example.com/two/{tag}", [tag, "extra"]);

        var items = await ListAsync($"?tag={tag.ToUpperInvariant()}");

        Assert.Equal([second.Id, first.Id], items.Where(item => item.Tags.Contains(tag)).Select(item => item.Id));
        Assert.DoesNotContain(items, item => item.Id == other.Id);
    }

    [Fact]
    public async Task Search_matches_title_and_url_ignoring_case()
    {
        var token = Token();
        var byTitle = await CreateAsync($"Quokka {token}", "https://example.com/titles");
        var byUrl = await CreateAsync("Plain title", $"https://example.com/{token}");

        var items = await ListAsync($"?q={token.ToUpperInvariant()}");

        Assert.Contains(items, item => item.Id == byUrl.Id);
        Assert.Contains(items, item => item.Id == byTitle.Id);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Search_treats_like_wildcards_as_plain_text(string wildcard)
    {
        var token = Token();
        var bookmark = await CreateAsync($"Plain {token}", $"https://example.com/{token}");

        var items = await ListAsync($"?q={Uri.EscapeDataString(wildcard)}");

        Assert.DoesNotContain(items, item => item.Id == bookmark.Id);
    }

    [Fact]
    public async Task Deleting_a_bookmark_removes_it_and_a_second_delete_is_404()
    {
        var token = Token();
        var bookmark = await CreateAsync($"Temporary {token}", $"https://example.com/{token}");

        var first = await _client.DeleteAsync($"/bookmarks/{bookmark.Id}");
        var second = await _client.DeleteAsync($"/bookmarks/{bookmark.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.DoesNotContain(await ListAsync($"?q={token}"), item => item.Id == bookmark.Id);
    }

    [Fact]
    public async Task Tag_counts_include_each_bookmark_once()
    {
        var tag = $"count{Token()}";
        await CreateAsync($"A {tag}", $"https://example.com/a/{tag}", [tag, tag.ToUpperInvariant()]);
        await CreateAsync($"B {tag}", $"https://example.com/b/{tag}", [tag]);

        var counts = (await _client.GetFromJsonAsync<List<TagCount>>("/tags"))!;

        Assert.Equal(2, counts.Single(count => count.Tag == tag).Count);
        Assert.Equal(counts.OrderByDescending(count => count.Count).Select(count => count.Count), counts.Select(count => count.Count));
    }

    [Fact]
    public async Task Seeding_again_does_not_duplicate_sample_bookmarks()
    {
        var db = factory.Services.GetRequiredService<Db>();
        for (var run = 0; run < 2; run++)
        {
            await using var conn = await db.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await BookmarkSeed.ApplyAsync(conn, tx);
            await tx.CommitAsync();
        }

        await using var check = await db.OpenAsync();
        var urls = BookmarkSeed.Items.Select(item => item.Url).ToArray();
        var stored = await check.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM bookmarks WHERE url = ANY(@urls)", new { urls });

        Assert.Equal(BookmarkSeed.Items.Length, stored);
    }

    private static string Token() => Guid.NewGuid().ToString("N")[..10];

    private Task<HttpResponseMessage> PostBookmark(string title, string url, string[]? tags = null) =>
        _client.PostAsJsonAsync("/bookmarks", new { title, url, tags });

    private async Task<Bookmark> CreateAsync(string title, string url, string[]? tags = null)
    {
        var response = await PostBookmark(title, url, tags);
        response.EnsureSuccessStatusCode();
        var bookmark = (await response.Content.ReadFromJsonAsync<Bookmark>())!;
        _created.Add(bookmark.Id);
        return bookmark;
    }

    private async Task<List<Bookmark>> ListAsync(string query) =>
        (await _client.GetFromJsonAsync<List<Bookmark>>($"/bookmarks{query}"))!;

    private static async Task<List<string>> ErrorFields(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject().Select(field => field.Name).ToList();
    }
}
