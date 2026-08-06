using BookTranslatorStudio.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Genera el producto final visible: un PDF con el contenido traducido,
/// respetando el orden de las páginas y de los bloques del documento original.
/// </summary>
public sealed class TranslatedPdfExporter
{
    public TranslatedPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task ExportAsync(
        BookProject project,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var blocks = project.Sections
            .SelectMany(section => section.Blocks)
            .OrderBy(block => block.SourcePageNumber)
            .ThenBy(block => block.Order)
            .ToList();

        var pending = blocks
            .Where(block => !block.IsTranslated)
            .ToList();

        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se puede generar el PDF: faltan " +
                $"{pending.Count:N0} bloques por traducir.");
        }

        var pages = blocks
            .GroupBy(block => block.SourcePageNumber)
            .OrderBy(group => group.Key)
            .ToList();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                Document.Create(document =>
                {
                    foreach (var sourcePage in pages)
                    {
                        document.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(42);
                            page.DefaultTextStyle(
                                style => style
                                    .FontFamily("Arial")
                                    .FontSize(11));

                            page.Header()
                                .PaddingBottom(12)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Row(row =>
                                {
                                    row.RelativeItem()
                                        .Text(project.Name)
                                        .SemiBold()
                                        .FontSize(12);

                                    row.ConstantItem(110)
                                        .AlignRight()
                                        .Text($"Página {sourcePage.Key:N0}")
                                        .FontColor(Colors.Grey.Darken1);
                                });

                            page.Content()
                                .PaddingVertical(18)
                                .Column(column =>
                                {
                                    column.Spacing(10);

                                    foreach (var block in sourcePage)
                                    {
                                        column.Item()
                                            .Text(block.TranslatedText)
                                            .LineHeight(1.35f);
                                    }
                                });

                            page.Footer()
                                .AlignCenter()
                                .Text(text =>
                                {
                                    text.Span("Book Translator Studio · ");
                                    text.CurrentPageNumber();
                                    text.Span(" / ");
                                    text.TotalPages();
                                });
                        });
                    }
                }).GeneratePdf(outputPath);
            },
            cancellationToken);
    }
}
