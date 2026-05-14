using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Stefan.Server.Application.Services;

public class XAiTextToSpeechService(IConfiguration configuration) : ITextToSpeechService
{
    private static readonly HttpClient HttpClient = new();

    public async Task<byte[]> SynthesizeAsync(string text)
    {
        var endpoint = configuration["xAI:Endpoint"] ?? "https://api.x.ai/v1";
        var apiKey = configuration["xAI:ApiKey"] ?? string.Empty;
        var language = configuration["xAI:Language"] ?? "en";
        var voiceId = configuration["xAI:TtsVoiceId"] ?? "leo";

        var body = new
        {
            text,
            voice_id = voiceId,
            language,
            output_format = new { codec = "wav" }
        };

        var json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/tts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }
}
