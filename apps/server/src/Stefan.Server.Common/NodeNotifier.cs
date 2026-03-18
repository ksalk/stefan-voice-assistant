namespace Stefan.Server.Common
{
    public class NodeNotifier
    {
        private static readonly HttpClient _httpClient = new();

        public async Task SendAudioNotification(string deviceId, byte[] audioData)
        {
            var content = new ByteArrayContent(audioData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            var response = await _httpClient.PostAsync("http://localhost:8080/audio", content);
            response.EnsureSuccessStatusCode();
        }
    }
}