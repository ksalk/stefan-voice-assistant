namespace StefanAssistant.Server.Common
{
    public class NodeNotifier
    {
        private static readonly HttpClient _httpClient = new();

        public async Task SendTextNotification(string deviceId, string message)
        {
            var content = new StringContent(message, System.Text.Encoding.UTF8, "text/plain");
            var response = await _httpClient.PostAsync("http://localhost:8080/text", content);
            response.EnsureSuccessStatusCode();
        }
    }
}