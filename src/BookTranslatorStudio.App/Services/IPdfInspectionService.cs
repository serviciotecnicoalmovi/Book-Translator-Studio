using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Analiza la información básica de un PDF sin modificarlo.
/// </summary>
public interface IPdfInspectionService
{
    PdfDocumentInfo Inspect(string filePath);
}
