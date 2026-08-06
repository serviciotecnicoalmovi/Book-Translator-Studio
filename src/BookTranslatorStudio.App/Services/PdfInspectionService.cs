using System.IO;
using BookTranslatorStudio.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Implementa la lectura real de archivos PDF mediante PdfPig.
/// </summary>
public sealed class PdfInspectionService : IPdfInspectionService
{
    public Task<PdfDocumentInfo> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Task.Run(
            () => Inspect(filePath, cancellationToken),
            cancellationToken);
    }

    private static PdfDocumentInfo Inspect(
        string filePath,
        CancellationToken cancellationToken)
    {
        ValidateFile(filePath);

        var fileInfo = new FileInfo(filePath);
        var pages = new List<PdfPageContent>();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractedText = ContentOrderTextExtractor
                .GetText(page)
                .Trim();

            pages.Add(new PdfPageContent(
                page.Number,
                extractedText));
        }

        return new PdfDocumentInfo(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            document.NumberOfPages,
            pages);
    }

    private static void ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "El archivo seleccionado ya no existe.",
                filePath);
        }

        if (!string.Equals(
                Path.GetExtension(filePath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "El archivo seleccionado no tiene extensión PDF.");
        }
    }
}
