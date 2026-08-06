namespace BookTranslatorStudio.Models;

/// <summary>
/// Configuración sin topes internos. Cero reintentos significa reintentos
/// indefinidos hasta pausar o cancelar. Cero concurrencia usa todos los
/// bloques pendientes como grado solicitado.
/// </summary>
public sealed class TranslationConfiguration
{
    public bool IsConfigured { get; set; }

    public string SourceLanguageCode { get; set; } = "auto";

    public string SourceLanguageName { get; set; } =
        "Detección automática";

    public string TargetLanguageCode { get; set; } = "es";

    public string TargetLanguageName { get; set; } = "Español";

    public string Instruction { get; set; } =
        "Conserva el significado, el tono, los nombres propios, las listas y los párrafos.";

    public int ConcurrentRequests { get; set; } = 1;

    public int RetryCount { get; set; }

    public int RetryDelaySeconds { get; set; } = 2;

    public TranslationScope Scope { get; set; } =
        TranslationScope.PendingBlocks;

    public Guid? SelectedEngineProfileId { get; set; }

    public List<EngineProfile> EngineProfiles { get; set; } =
    [
        new EngineProfile
        {
            Name = "OpenAI compatible",
            Protocol = TranslationProtocol.OpenAiChatCompletions,
            Endpoint = "https://api.openai.com/v1/chat/completions",
            Model = "gpt-4.1-mini"
        },
        new EngineProfile
        {
            Name = "Ollama local",
            Protocol = TranslationProtocol.OllamaGenerate,
            Endpoint = "http://localhost:11434/api/generate",
            Model = "gemma3"
        },
        new EngineProfile
        {
            Name = "LibreTranslate",
            Protocol = TranslationProtocol.LibreTranslate,
            Endpoint = "http://localhost:5000/translate"
        }
    ];
}
