namespace AgentFxLineBot;

public static class LineBotUtils
{
    public record LineContent(string Id, string MimeType, byte[] Content);
    static HttpClient httpClient = new HttpClient();
    public static async Task<LineContent> GetUserUploadedContentAsync(string contentId, string accessToken)
    {
        contentId = contentId.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api-data.line.me/v2/bot/message/" + contentId + "/content");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadAsByteArrayAsync();
        var mimeType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new LineContent(contentId, mimeType, data);
    }
}