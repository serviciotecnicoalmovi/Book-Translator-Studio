namespace BookTranslatorStudio.Models;

/// <summary>
/// Sección lógica del libro. Puede representar portada, introducción,
/// capítulo, apéndice u otro grupo de contenido.
/// </summary>
public sealed class BookSection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;

    public int StartPageNumber { get; set; }

    public List<BookBlock> Blocks { get; set; } = [];

    public int WordCount => Blocks.Sum(block => block.WordCount);

    public string DisplayName =>
        $"{Order:N0}. {Title} · {Blocks.Count:N0} bloques";
}
