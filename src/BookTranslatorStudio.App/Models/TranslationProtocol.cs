namespace BookTranslatorStudio.Models;

/// <summary>
/// Contratos de API incluidos. GenericJson permite conectar otros motores.
/// </summary>
public enum TranslationProtocol
{
    OpenAiChatCompletions,
    OllamaGenerate,
    LibreTranslate,
    GenericJson
}
