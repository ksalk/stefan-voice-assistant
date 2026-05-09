using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Stefan.Server.Application.Services;

public class XAiSpeechToTextService(IConfiguration configuration) : ISpeechToTextService
{
    private static readonly HttpClient HttpClient = new();

    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        var endpoint = configuration["xAI:Endpoint"] ?? "https://api.x.ai/v1";
        var apiKey = configuration["xAI:ApiKey"] ?? string.Empty;
        var language = configuration["xAI:Language"] ?? "en";

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(streamContent, "file", "audio.wav");
        content.Add(new StringContent("true"), "format");
        content.Add(new StringContent(language), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/stt");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseJson);

        return document.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
    }
}
