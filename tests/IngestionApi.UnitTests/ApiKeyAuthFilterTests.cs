using IngestionApi.Filters;
using IngestionApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IngestionApi.UnitTests;

public class ApiKeyAuthFilterTests
{
    private readonly IApiKeyValidator _validator = Substitute.For<IApiKeyValidator>();
    private readonly ApiKeyAuthFilter _filter = new();

    private (EndpointFilterInvocationContext context, EndpointFilterDelegate next) CreateContext(string? apiKey)
    {
        var httpContext = new DefaultHttpContext();
        if (apiKey is not null)
        {
            httpContext.Request.Headers["x-api-key"] = apiKey;
        }

        var services = new ServiceCollection();
        services.AddSingleton(_validator);
        services.AddSingleton<ILogger<ApiKeyAuthFilter>>(Substitute.For<ILogger<ApiKeyAuthFilter>>());
        httpContext.RequestServices = services.BuildServiceProvider();

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var next = Substitute.For<EndpointFilterDelegate>();
        next.Invoke(Arg.Any<EndpointFilterInvocationContext>()).Returns("ok");

        return (context, next);
    }

    [Fact]
    public async Task ValidApiKey_CallsNext()
    {
        _validator.IsValid("valid-key").Returns(true);
        var (context, next) = CreateContext("valid-key");

        var result = await _filter.InvokeAsync(context, next);

        Assert.Equal("ok", result);
        await next.Received(1).Invoke(Arg.Any<EndpointFilterInvocationContext>());
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        _validator.IsValid("bad-key").Returns(false);
        var (context, next) = CreateContext("bad-key");

        var result = await _filter.InvokeAsync(context, next);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await next.DidNotReceive().Invoke(Arg.Any<EndpointFilterInvocationContext>());
    }

    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        _validator.IsValid(null).Returns(false);
        var (context, next) = CreateContext(null);

        var result = await _filter.InvokeAsync(context, next);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task EmptyApiKey_Returns401()
    {
        _validator.IsValid("").Returns(false);
        var (context, next) = CreateContext("");

        var result = await _filter.InvokeAsync(context, next);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
