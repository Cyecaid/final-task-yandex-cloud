using FinalTask.Models;
using FinalTask.Repository;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGuestbookRepository, YdbRepository>();

var app = builder.Build();

var repository = app.Services.GetRequiredService<IGuestbookRepository>();
await repository.InitializeAsync();

if (args.Contains("--migrate"))
{
    await repository.CreateSchemaAsync();
    return;
}

var instanceId = string.Concat("Replica-", Guid.NewGuid().ToString().AsSpan(0, 5));

app.MapGet("/api/info", () => 
{
    var version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "1.0.0";
    return new AppInfo(version, instanceId, DateTime.UtcNow);
});

app.MapGet("/api/messages", async ([FromServices] IGuestbookRepository repo) => await repo.GetMessagesAsync());

app.MapPost("/api/messages", async ([FromBody] CreateMessageDto dto, [FromServices] IGuestbookRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(dto.Content) || string.IsNullOrWhiteSpace(dto.User))
        return Results.BadRequest("User and Content cannot be empty");

    await repo.AddMessageAsync(dto.User, dto.Content);
    return Results.Ok();
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");