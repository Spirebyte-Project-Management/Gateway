using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spirebyte.APIGateway.Serialization;

internal sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public T Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, Options);
    }
}