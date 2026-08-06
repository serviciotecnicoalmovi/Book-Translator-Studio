using System.IO;
using System.Net.Http;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Un único motor configurable para contratos conocidos y JSON genérico.
/// </summary>
public sealed class ConfigurableTranslationEngine : ITranslationEngine
{
    public async Task<string> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        using var httpClient = CreateClient(request.Profile);
        using var message = CreateMessage(request);

        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {ExtractError(body)}");
        }

        using var json = JsonDocument.Parse(body);

        var path = request.Profile.Protocol switch
        {
            TranslationProtocol.OpenAiChatCompletions =>
                "choices[0].message.content",
            TranslationProtocol.OllamaGenerate => "response",
            TranslationProtocol.LibreTranslate => "translatedText",
            _ => request.Profile.ResponseJsonPath
        };

        var translated = JsonPathReader.ReadString(
            json.RootElement,
            path).Trim();

        if (string.IsNullOrWhiteSpace(translated))
        {
            throw new InvalidDataException(
                "El motor devolvió una traducción vacía.");
        }

        return translated;
    }

    private static HttpClient CreateClient(EngineProfile profile)
    {
        var client = new HttpClient();

        if (profile.TimeoutSeconds > 0)
        {
            client.Timeout = TimeSpan.FromSeconds(
                profile.TimeoutSeconds);
        }
        else
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        }

        return client;
    }

    private static HttpRequestMessage CreateMessage(
        TranslationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Profile.Endpoint))
        {
            throw new InvalidOperationException(
                "El perfil no tiene una URL configurada.");
        }

        var message = new HttpRequestMessage(
            HttpMethod.Post,
            request.Profile.Endpoint.Trim());

        ApplyHeaders(message, request);
        message.Content = new StringContent(
            BuildBody(request),
            Encoding.UTF8,
            "application/json");

        return message;
    }

    private static void ApplyHeaders(
        HttpRequestMessage message,
        TranslationRequest request)
    {
        var apiKey = string.IsNullOrWhiteSpace(
            request.SessionApiKey)
            ? request.Profile.ApiKey
            : request.SessionApiKey;

        if (request.Profile.Protocol ==
                TranslationProtocol.OpenAiChatCompletions &&
            !string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey.Trim());
        }

        foreach (var line in request.Profile.Headers.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = ReplaceTokens(
                line[(separator + 1)..].Trim(),
                request,
                apiKey,
                jsonEncodeText: false);

            message.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static string BuildBody(TranslationRequest request) =>
        request.Profile.Protocol switch
        {
            TranslationProtocol.OpenAiChatCompletions =>
                JsonSerializer.Serialize(new
                {
                    model = request.Profile.Model,
                    temperature = request.Profile.Temperature,
                    messages = new object[]
                    {
                        new
                        {
                            role = "system",
                            content =
                                "Traduce libros profesionalmente. " +
                                "Devuelve solamente la traducción."
                        },
                        new
                        {
                            role = "user",
                            content = BuildPrompt(request)
                        }
                    }
                }),

            TranslationProtocol.OllamaGenerate =>
                JsonSerializer.Serialize(new
                {
                    model = request.Profile.Model,
                    prompt = BuildPrompt(request),
                    stream = false,

                    // Mantiene el modelo cargado durante todo el libro.
                    keep_alive = -1,

                    options = new
                    {
                        temperature = 0.0,

                        // Un libro normal no necesita el contexto máximo
                        // de 128K para traducir un bloque.
                        num_ctx = CalculateContextSize(request.Text),

                        // Evita generaciones excesivas o explicaciones.
                        num_predict = CalculateOutputTokens(request.Text),

                        // Lotes mayores reducen la sobrecarga en CPU/GPU.
                        num_batch = 512,

                        top_k = 20,
                        top_p = 0.9,
                        repeat_penalty = 1.05
                    }
                }),

            TranslationProtocol.LibreTranslate =>
                JsonSerializer.Serialize(new
                {
                    q = request.Text,
                    source = request.SourceCode,
                    target = request.TargetCode,
                    format = "text",
                    api_key = string.IsNullOrWhiteSpace(
                        request.SessionApiKey)
                        ? request.Profile.ApiKey
                        : request.SessionApiKey
                }),

            _ => ReplaceTokens(
                request.Profile.RequestTemplate,
                request,
                request.SessionApiKey,
                jsonEncodeText: true)
        };

    private static string BuildPrompt(TranslationRequest request)
    {
        var sourceName = request.SourceCode == "auto"
            ? "the automatically detected language"
            : request.SourceName;

        var sourceCode = request.SourceCode == "auto"
            ? "auto"
            : request.SourceCode;

        var targetName = string.IsNullOrWhiteSpace(
            request.TargetName)
            ? request.TargetCode
            : request.TargetName;

        // Formato esperado por TranslateGemma.
        return
            $"You are a professional {sourceName} ({sourceCode}) " +
            $"to {targetName} translator. " +
            $"Your goal is to accurately convey the meaning and nuances " +
            $"of the original text while adhering to the target language " +
            $"grammar, vocabulary, and cultural sensitivities. " +
            $"Produce only the translation, without explanations or " +
            $"commentary. Preserve paragraphs, headings, lists, numbers, " +
            $"citations and names.\n\n" +
            request.Text;
    }

    private static int CalculateContextSize(string text)
    {
        var estimatedTokens = Math.Max(256, text.Length / 3);

        if (estimatedTokens <= 1024)
        {
            return 2048;
        }

        if (estimatedTokens <= 3072)
        {
            return 4096;
        }

        return 8192;
    }

    private static int CalculateOutputTokens(string text)
    {
        var estimatedInputTokens = Math.Max(64, text.Length / 3);

        // La traducción puede crecer, pero no debe multiplicarse sin control.
        return Math.Clamp(
            (int)(estimatedInputTokens * 1.6),
            256,
            8192);
    }

    private static string ReplaceTokens(
        string template,
        TranslationRequest request,
        string apiKey,
        bool jsonEncodeText)
    {
        var textValue = jsonEncodeText
            ? JsonSerializer.Serialize(request.Text)
            : request.Text;

        return template
            .Replace("{{text_json}}", textValue)
            .Replace("{{text}}", request.Text)
            .Replace("{{source}}", request.SourceCode)
            .Replace("{{source_name}}", request.SourceName)
            .Replace("{{target}}", request.TargetCode)
            .Replace("{{target_name}}", request.TargetName)
            .Replace("{{instruction}}", request.Instruction)
            .Replace("{{model}}", request.Profile.Model)
            .Replace(
                "{{temperature}}",
                request.Profile.Temperature.ToString(
                    CultureInfo.InvariantCulture))
            .Replace("{{api_key}}", apiKey);
    }

    private static string ExtractError(string content)
    {
        try
        {
            using var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty(
                    "error",
                    out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty(
                        "message",
                        out var message))
                {
                    return message.GetString() ?? content;
                }

                return error.ToString();
            }
        }
        catch (JsonException)
        {
            // El cuerpo no era JSON; se devuelve sin transformar.
        }

        return content;
    }
}
