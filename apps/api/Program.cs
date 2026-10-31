using Api;

var builder = WebApplication.CreateBuilder(args);

// Render (and most hosts) pass the port to listen on
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<Db>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    else policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => Results.Text("net API (ASP.NET Core). Endpoints: GET /health, GET /bookmarks, POST /bookmarks, DELETE /bookmarks/{id}, GET /tags"));
app.MapGet("/health", async (Db db) => Results.Ok(new { status = "ok", db = await db.StatusAsync() }));
app.MapBookmarks();

app.Run();

public partial class Program;
