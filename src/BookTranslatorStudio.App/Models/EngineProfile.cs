namespace BookTranslatorStudio.Models;

/// <summary>
/// Perfil abierto y persistente. Todos los parámetros pertenecen al usuario.
/// </summary>
public sealed class EngineProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Motor personalizado";

    public TranslationProtocol Protocol { get; set; } =
        TranslationProtocol.OpenAiChatCompletions;

    public string Endpoint { get; set; } =
        "https://api.openai.com/v1/chat/completions";

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public bool RememberApiKey { get; set; }

    public string Headers { get; set; } = string.Empty;

    public string RequestTemplate { get; set; } =
        "{\"text\":{{text_json}},\"source\":\"{{source}}\",\"target\":\"{{target}}\"}";

    public string ResponseJsonPath { get; set; } = "translatedText";

    public double Temperature { get; set; } = 0.2;

    public int TimeoutSeconds { get; set; }

    public EngineProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Protocol = Protocol,
        Endpoint = Endpoint,
        Model = Model,
        ApiKey = ApiKey,
        RememberApiKey = RememberApiKey,
        Headers = Headers,
        RequestTemplate = RequestTemplate,
        ResponseJsonPath = ResponseJsonPath,
        Temperature = Temperature,
        TimeoutSeconds = TimeoutSeconds
    };

    public override string ToString() => Name;
}
