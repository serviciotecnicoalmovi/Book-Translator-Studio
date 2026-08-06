namespace BookTranslatorStudio.Models;

/// <summary>
/// Información real y contenido textual del documento PDF seleccionado.
/// </summary>
public sealed record PdfDocumentInfo(
    string FileName,
    string FullPath,
    long FileSizeBytes,
    int PageCount,
    IReadOnlyList<PdfPageContent> Pages)
{
    public string FormattedSize =>
        FileSizeBytes switch
        {
            >= 1_073_741_824 => $"{FileSizeBytes / 1_073_741_824d:N2} GB",
            >= 1_048_576 => $"{FileSizeBytes / 1_048_576d:N2} MB",
            >= 1_024 => $"{FileSizeBytes / 1_024d:N2} KB",
            _ => $"{FileSizeBytes:N0} bytes"
        };

    public int PagesWithText => Pages.Count(page => page.HasText);

    public int TotalCharacterCount => Pages.Sum(page => page.CharacterCount);

    public bool RequiresOcr => PagesWithText == 0;
}
