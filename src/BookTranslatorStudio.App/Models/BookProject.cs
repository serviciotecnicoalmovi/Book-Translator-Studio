namespace BookTranslatorStudio.Models;

/// <summary>
/// Proyecto persistente que contiene el libro completamente preparado.
/// </summary>
public sealed class BookProject
{
    public const string FileExtension = ".btsproject";

    public int FormatVersion { get; set; } = 1;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string SourcePdfPath { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    public int SourcePageCount { get; set; }

    public List<BookSection> Sections { get; set; } = [];

    public int BlockCount => Sections.Sum(section => section.Blocks.Count);

    public int WordCount => Sections.Sum(section => section.WordCount);

    public int CharacterCount =>
        Sections.Sum(section =>
            section.Blocks.Sum(block => block.CharacterCount));
}
