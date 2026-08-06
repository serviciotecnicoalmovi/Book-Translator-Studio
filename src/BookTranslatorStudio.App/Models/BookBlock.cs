namespace BookTranslatorStudio.Models;

/// <summary>
/// Unidad editable preparada para el flujo posterior de traducción.
/// </summary>
public sealed class BookBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Order { get; set; }

    public int SourcePageNumber { get; set; }

    public string OriginalText { get; set; } = string.Empty;

    public string TranslatedText { get; set; } = string.Empty;

    public int CharacterCount => OriginalText.Length;

    public int WordCount =>
        string.IsNullOrWhiteSpace(OriginalText)
            ? 0
            : OriginalText.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries).Length;

    public string DisplayName =>
        $"Bloque {Order:N0} · pág. {SourcePageNumber:N0} · {WordCount:N0} palabras";
}
