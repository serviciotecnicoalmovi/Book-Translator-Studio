namespace BookTranslatorStudio.Models;

public sealed class BookBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Order { get; set; }

    public int SourcePageNumber { get; set; }

    public string OriginalText { get; set; } = string.Empty;

    public string TranslatedText { get; set; } = string.Empty;

    public TranslationBlockStatus TranslationStatus { get; set; } =
        TranslationBlockStatus.Pending;

    public string TranslationError { get; set; } = string.Empty;

    public int TranslationAttempts { get; set; }

    public DateTime? TranslatedUtc { get; set; }

    public int CharacterCount => OriginalText.Length;

    public int WordCount =>
        string.IsNullOrWhiteSpace(OriginalText)
            ? 0
            : OriginalText.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries).Length;

    public bool IsTranslated =>
        TranslationStatus == TranslationBlockStatus.Completed &&
        !string.IsNullOrWhiteSpace(TranslatedText);

    public string DisplayName =>
        $"Bloque {Order:N0} · pág. {SourcePageNumber:N0} · " +
        $"{WordCount:N0} palabras · {StatusText}";

    public string StatusText => TranslationStatus switch
    {
        TranslationBlockStatus.Completed => "traducido",
        TranslationBlockStatus.InProgress => "procesando",
        TranslationBlockStatus.Failed => "error",
        TranslationBlockStatus.Skipped => "omitido",
        _ => "pendiente"
    };
}
