namespace BookTranslatorStudio.Models;

/// <summary>
/// Texto extraído de una página individual del documento.
/// </summary>
public sealed record PdfPageContent(
    int PageNumber,
    string Text)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public int CharacterCount => Text.Length;

    public string DisplayName =>
        HasText
            ? $"Página {PageNumber:N0} · {CharacterCount:N0} caracteres"
            : $"Página {PageNumber:N0} · sin texto";
}
