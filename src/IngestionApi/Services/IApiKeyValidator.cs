namespace IngestionApi.Services;

public interface IApiKeyValidator
{
    bool IsValid(string? apiKey);
}
