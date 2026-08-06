using System.IO;
using System.Text.RegularExpressions;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Reconstruye el contenido del libro en secciones y bloques coherentes.
/// Las heurísticas son internas: el usuario trabaja con el libro ya preparado.
/// </summary>
public sealed partial class BookPreparationService : IBookPreparationService
{
    private const int MaximumBlockCharacters = 2_800;

    public BookProject Prepare(PdfDocumentInfo document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var project = new BookProject
        {
            Name = Path.GetFileNameWithoutExtension(document.FileName),
            SourcePdfPath = document.FullPath,
            SourcePageCount = document.PageCount
        };

        BookSection? currentSection = null;
        var sectionOrder = 0;
        var blockOrder = 0;

        foreach (var page in document.Pages)
        {
            if (!page.HasText)
            {
                continue;
            }

            var paragraphs = SplitParagraphs(page.Text);

            foreach (var paragraph in paragraphs)
            {
                if (LooksLikeSectionTitle(paragraph))
                {
                    currentSection = new BookSection
                    {
                        Order = ++sectionOrder,
                        Title = NormalizeTitle(paragraph),
                        StartPageNumber = page.PageNumber
                    };

                    project.Sections.Add(currentSection);
                    continue;
                }

                currentSection ??= CreateInitialSection(
                    project,
                    ref sectionOrder,
                    page.PageNumber);

                foreach (var blockText in SplitLargeParagraph(paragraph))
                {
                    currentSection.Blocks.Add(new BookBlock
                    {
                        Order = ++blockOrder,
                        SourcePageNumber = page.PageNumber,
                        OriginalText = blockText
                    });
                }
            }
        }

        if (project.Sections.Count == 0)
        {
            project.Sections.Add(new BookSection
            {
                Order = 1,
                Title = "Contenido sin texto extraíble",
                StartPageNumber = 1
            });
        }

        return project;
    }

    private static BookSection CreateInitialSection(
        BookProject project,
        ref int sectionOrder,
        int pageNumber)
    {
        var section = new BookSection
        {
            Order = ++sectionOrder,
            Title = "Inicio del libro",
            StartPageNumber = pageNumber
        };

        project.Sections.Add(section);
        return section;
    }

    private static IReadOnlyList<string> SplitParagraphs(string text) =>
        ParagraphSeparatorRegex()
            .Split(text.Replace("\r\n", "\n"))
            .Select(NormalizeParagraph)
            .Where(paragraph => paragraph.Length > 0)
            .ToList();

    private static string NormalizeParagraph(string paragraph) =>
        InlineWhitespaceRegex()
            .Replace(paragraph.Trim(), " ");

    private static bool LooksLikeSectionTitle(string text)
    {
        if (text.Length is < 3 or > 120)
        {
            return false;
        }

        if (ChapterTitleRegex().IsMatch(text))
        {
            return true;
        }

        var words = text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Length > 12 || text.EndsWith('.'))
        {
            return false;
        }

        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
        {
            return false;
        }

        var uppercaseRatio =
            letters.Count(char.IsUpper) / (double)letters.Length;

        return uppercaseRatio >= 0.72;
    }

    private static string NormalizeTitle(string title) =>
        InlineWhitespaceRegex().Replace(title.Trim(), " ");

    private static IEnumerable<string> SplitLargeParagraph(string paragraph)
    {
        if (paragraph.Length <= MaximumBlockCharacters)
        {
            yield return paragraph;
            yield break;
        }

        var sentences = SentenceBoundaryRegex().Split(paragraph);
        var current = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var clean = sentence.Trim();
            if (clean.Length == 0)
            {
                continue;
            }

            if (currentLength > 0 &&
                currentLength + clean.Length + 1 > MaximumBlockCharacters)
            {
                yield return string.Join(" ", current);
                current.Clear();
                currentLength = 0;
            }

            current.Add(clean);
            currentLength += clean.Length + 1;
        }

        if (current.Count > 0)
        {
            yield return string.Join(" ", current);
        }
    }

    [GeneratedRegex(@"\n\s*\n+")]
    private static partial Regex ParagraphSeparatorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(
        @"^(cap[ií]tulo|chapter|parte|part|secci[oó]n|section|pr[oó]logo|ep[ií]logo|introducci[oó]n|conclusi[oó]n|ap[eé]ndice)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChapterTitleRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBoundaryRegex();
}
