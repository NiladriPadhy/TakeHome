using IngestionApi.Services;

namespace IngestionApi.Filters;

public class ApiKeyAuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var validator = httpContext.RequestServices.GetRequiredService<IApiKeyValidator>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<ApiKeyAuthFilter>>();

        httpContext.Request.Headers.TryGetValue("x-api-key", out var apiKey);

        if (!validator.IsValid(apiKey))
        {
            logger.LogWarning("Authentication failed: invalid or missing API key for {Path}",
                httpContext.Request.Path);
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
