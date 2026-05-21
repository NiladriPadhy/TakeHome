namespace IngestionApi.Services;

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly string? _expectedKey;

    public ApiKeyValidator(IConfiguration configuration)
    {
        _expectedKey = configuration["ApiKey"] ?? "local-dev";
    }

    public bool IsValid(string? apiKey)
        => !string.IsNullOrEmpty(apiKey) && apiKey == _expectedKey;
}
