using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Guarda y recupera proyectos completos de Book Translator Studio.
/// </summary>
public interface IBookProjectService
{
    Task SaveAsync(
        BookProject project,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<BookProject> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
