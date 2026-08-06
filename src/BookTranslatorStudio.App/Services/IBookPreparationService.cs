using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Convierte el PDF extraído en un proyecto organizado y editable.
/// </summary>
public interface IBookPreparationService
{
    BookProject Prepare(PdfDocumentInfo document);
}
