using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CaptionTranslator.Translation;

public sealed class ArgosTranslator : IDisposable
{
    private const string Endpoint = "http://127.0.0.1:8765/translate";
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Process? _serviceProcess;

    public ArgosTranslator()
    {
        var projectPath = Environment.GetEnvironmentVariable("CAPTION_TRANSLATOR_WSL_PATH")
            ?? "/home/dev/workspace/caption-translator";
        var distribution = Environment.GetEnvironmentVariable("CAPTION_TRANSLATOR_WSL_DISTRO")
            ?? "Ubuntu";
        var servicePath = $"{projectPath}/tools/argos_service.py";
        var pythonPath = $"{projectPath}/.argos-venv312/bin/python";
        _serviceProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-d {distribution} -- {pythonPath} {servicePath}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });
    }

    public async Task<string?> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { text }),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(Endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("translatedText", out var translated)
                ? translated.GetString()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        if (_serviceProcess is { HasExited: false })
        {
            _serviceProcess.Kill(true);
        }

        _serviceProcess?.Dispose();
    }
}