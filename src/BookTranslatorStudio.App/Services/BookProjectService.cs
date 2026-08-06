using System.IO;
using System.Text.Json;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Persistencia JSON legible, versionada y sin base de datos externa.
/// </summary>
public sealed class BookProjectService : IBookProjectService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        BookProject project,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        project.ModifiedUtc = DateTime.UtcNow;

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await JsonSerializer.SerializeAsync(
            stream,
            project,
            SerializerOptions,
            cancellationToken);
    }

    public async Task<BookProject> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var project = await JsonSerializer.DeserializeAsync<BookProject>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (project is null)
        {
            throw new InvalidDataException(
                "El archivo no contiene un proyecto válido.");
        }

        if (project.FormatVersion != 1)
        {
            throw new InvalidDataException(
                $"La versión del proyecto ({project.FormatVersion}) no es compatible.");
        }

        return project;
    }
}
