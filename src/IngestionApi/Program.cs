namespace IngestionApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IMeasurementStore, InMemoryStore>();
        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

        app.MapPost("/api/v1/measurements", async (Measurement m, IMeasurementStore store, HttpContext ctx) =>
        {
            if (!ValidateApiKey(ctx))
                return Results.Unauthorized();

            if (!MeasurementValidator.IsValid(m))
                return Results.BadRequest("invalid measurement");

            await store.AddAsync(m);

            return Results.Accepted($"/api/v1/measurements/{m.MeasurementId}", m);
        });

        app.MapGet("/api/v1/measurements", async (string? type, DateTimeOffset? since, IMeasurementStore store) =>
        {
            var results = await store.QueryAsync(type, since ?? DateTimeOffset.UtcNow.AddMinutes(-5));
            return Results.Ok(results);
        });

        await app.RunAsync();
    }

    private static bool ValidateApiKey(HttpContext ctx)
    {
        return ctx.Request.Headers.TryGetValue("x-api-key", out var v) && v == "local-dev";
    }
}
