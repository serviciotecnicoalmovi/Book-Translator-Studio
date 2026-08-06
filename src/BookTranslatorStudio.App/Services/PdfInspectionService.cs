using System.Text;
using System.Text.RegularExpressions;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Valida el archivo PDF y realiza un conteo inicial de objetos de página.
/// Esta etapa no extrae texto; esa capacidad se añadirá en la siguiente versión.
/// </summary>
public sealed partial class PdfInspectionService : IPdfInspectionService
{
    private const int HeaderLength = 5;

    public PdfDocumentInfo Inspect(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

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

        var fileInfo = new FileInfo(filePath);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        ValidateHeader(stream);

        var pageCount = DetectPageCount(stream);

        return new PdfDocumentInfo(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            pageCount);
    }

    private static void ValidateHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderLength];
        var bytesRead = stream.Read(header);

        if (bytesRead != HeaderLength ||
            !header.SequenceEqual("%PDF-"u8))
        {
            throw new InvalidDataException(
                "El archivo no contiene un encabezado PDF válido.");
        }
    }

    private static int DetectPageCount(Stream stream)
    {
        stream.Position = 0;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var content = Encoding.Latin1.GetString(memory.ToArray());

        // Excluye /Type /Pages y cuenta únicamente objetos /Type /Page.
        var count = PageObjectRegex().Matches(content).Count;
        return Math.Max(count, 1);
    }

    [GeneratedRegex(
        @"/Type\s*/Page(?!s)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex PageObjectRegex();
}
