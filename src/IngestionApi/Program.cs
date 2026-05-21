using System.Text.Json;
using FluentValidation;
using IngestionApi.Events;
using IngestionApi.Filters;
using IngestionApi.Models;
using IngestionApi.Services;
using IngestionApi.Validation;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IngestionApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5100");

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IMeasurementStore, InMemoryStore>();
        builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
        builder.Services.AddSingleton<IMeasurementEventChannel, MeasurementEventChannel>();
        builder.Services.AddScoped<IValidator<Measurement>, MeasurementFluentValidator>();
        builder.Services.AddScoped<IMeasurementService, MeasurementService>();
        builder.Services.AddHealthChecks();
        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        // Health check — outside route group (unauthenticated), backward-compatible JSON response
        app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { status }));
            }
        });

        // API route group with shared auth filter
        var api = app.MapGroup("/api/v1")
            .AddEndpointFilter<ApiKeyAuthFilter>();

        api.MapPost("/measurements", async (Measurement m, IMeasurementService service) =>
        {
            try
            {
                var stored = await service.AddAsync(m);
                return Results.Accepted($"/api/v1/measurements/{stored.MeasurementId}", stored);
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                return Results.Problem(
                    title: "Validation Failed",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://tools.ietf.org/html/rfc7807",
                    extensions: new Dictionary<string, object?> { ["errors"] = errors });
            }
        });

        api.MapGet("/measurements", async (string? type, DateTimeOffset? since, int? skip, int? take, IMeasurementService service, HttpContext ctx) =>
        {
            var query = new PaginatedQuery(type, since, skip ?? 0, take ?? 50);

            if (query.Skip < 0 || query.Take < 1 || query.Take > 500)
            {
                return Results.Problem(
                    title: "Invalid pagination parameters",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://tools.ietf.org/html/rfc7807",
                    detail: "skip must be >= 0, take must be between 1 and 500.");
            }

            var (items, totalCount) = await service.QueryAsync(query);

            ctx.Response.Headers["X-Total-Count"] = totalCount.ToString();
            ctx.Response.Headers["X-Has-More"] = (query.Skip + items.Count < totalCount).ToString().ToLower();

            return Results.Ok(items);
        });

        await app.RunAsync();
    }
}
