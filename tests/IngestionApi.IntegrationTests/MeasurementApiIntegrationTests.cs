using Microsoft.AspNetCore.Mvc.Testing;

namespace IngestionApi.IntegrationTests;

public class MeasurementApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MeasurementApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
    }

    [Fact]
    public async Task Post_And_Query_Measurement_Succeeds()
    {
        await Task.CompletedTask;

        Assert.Fail();
    }
}