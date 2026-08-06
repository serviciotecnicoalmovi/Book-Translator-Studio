using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Abre un PDF, obtiene su conteo real de páginas y extrae su texto.
/// </summary>
public interface IPdfInspectionService
{
    Task<PdfDocumentInfo> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
