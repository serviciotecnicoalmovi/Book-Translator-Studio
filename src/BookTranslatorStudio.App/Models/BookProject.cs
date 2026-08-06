namespace BookTranslatorStudio.Models;

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

    public TranslationConfiguration Translation { get; set; } = new();

    public int BlockCount => Sections.Sum(section => section.Blocks.Count);

    public int WordCount => Sections.Sum(section => section.WordCount);

    public int TranslatedBlockCount =>
        Sections.Sum(section =>
            section.Blocks.Count(block => block.IsTranslated));

    public int FailedBlockCount =>
        Sections.Sum(section =>
            section.Blocks.Count(block =>
                block.TranslationStatus == TranslationBlockStatus.Failed));

    public double TranslationProgress =>
        BlockCount == 0
            ? 0
            : TranslatedBlockCount * 100d / BlockCount;
}
