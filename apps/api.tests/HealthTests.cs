using System.Net.Http.Json;
using Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_ok()
    {
        var body = await _client.GetFromJsonAsync<Dictionary<string, string>>("/health");
        Assert.Equal("ok", body!["status"]);
    }

    [Fact]
    public async Task Root_describes_the_api()
    {
        var text = await _client.GetStringAsync("/");
        Assert.Contains("/health", text);
    }

    [Theory]
    [InlineData("postgres://u:p%40ss@db.example.com/app?sslmode=require", "db.example.com", 5432, "u", "p@ss", "app")]
    [InlineData("postgresql://postgres:postgres@localhost:55432/app", "localhost", 55432, "postgres", "postgres", "app")]
    public void Connection_url_is_converted(string url, string host, int port, string user, string password, string database)
    {
        var b = new Npgsql.NpgsqlConnectionStringBuilder(Db.ToConnectionString(url));
        Assert.Equal(host, b.Host);
        Assert.Equal(port, b.Port);
        Assert.Equal(user, b.Username);
        Assert.Equal(password, b.Password);
        Assert.Equal(database, b.Database);
    }
}
