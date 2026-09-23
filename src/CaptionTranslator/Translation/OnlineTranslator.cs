using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace CaptionTranslator.Translation;

public sealed class OnlineTranslator : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<string?> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var requestUri = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=en|pt-BR";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }

            using (document)
            {
            if (!document.RootElement.TryGetProperty("responseStatus", out var status)
                || status.GetInt32() != 200
                || !document.RootElement.TryGetProperty("responseData", out var responseData)
                || !responseData.TryGetProperty("translatedText", out var translatedText))
            {
                return null;
            }

            var result = WebUtility.HtmlDecode(translatedText.GetString() ?? string.Empty);
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
            }
        }
    }

    public void Dispose() => _httpClient.Dispose();
}