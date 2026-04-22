using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ProjectArrange.Core;
using ProjectArrange.Core.Ai;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.Infrastructure.Ai;

public sealed class PythonHttpAiService(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IAiService
{
    public async Task<Result<AiChatResponse>> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = (configuration["Ai:Endpoint"] ?? "http://127.0.0.1:7312").Trim();
            if (string.IsNullOrWhiteSpace(endpoint)) return Result<AiChatResponse>.Fail("Ai:Endpoint is empty.");
            var url = endpoint.TrimEnd('/') + "/v1/chat";

            var client = httpClientFactory.CreateClient("ai");
            var payload = JsonSerializer.Serialize(request);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                return Result<AiChatResponse>.Fail($"HTTP {(int)resp.StatusCode}: {body}");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<AiChatResponse>(body);
                if (parsed is not null) return Result<AiChatResponse>.Ok(parsed with { Raw = body });
            }
            catch
            {
            }

            return Result<AiChatResponse>.Ok(new AiChatResponse(body, body));
        }
        catch (Exception ex)
        {
            return Result<AiChatResponse>.Fail(ex.Message);
        }
    }
}

