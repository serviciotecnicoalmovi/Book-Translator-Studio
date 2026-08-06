using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Instala, inicia y prepara automáticamente Ollama y TranslateGemma.
/// No requiere cuentas, claves ni configuración manual.
/// </summary>
public sealed class LocalTranslationRuntimeService
{
    private const string ModelName = "translategemma:4b";
    private static readonly Uri TagsEndpoint =
        new("http://localhost:11434/api/tags");

    public async Task EnsureReadyAsync(
        Action<string> reportStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reportStatus);

        reportStatus("Comprobando el motor local...");

        var executable = FindOllamaExecutable();

        if (string.IsNullOrWhiteSpace(executable))
        {
            reportStatus(
                "Instalando el motor local. Esto ocurre una sola vez...");

            await InstallOllamaAsync(cancellationToken);
            executable = FindOllamaExecutable();

            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new InvalidOperationException(
                    "Ollama no quedó instalado correctamente.");
            }
        }

        if (!await IsServerAvailableAsync(cancellationToken))
        {
            reportStatus("Iniciando el motor local...");
            StartOllamaServer(executable);

            await WaitForServerAsync(cancellationToken);
        }

        if (!await IsModelInstalledAsync(cancellationToken))
        {
            reportStatus(
                "Descargando el modelo local de traducción. " +
                "Esto ocurre una sola vez...");

            await RunProcessAsync(
                executable,
                $"pull {ModelName}",
                cancellationToken);
        }

        reportStatus("Motor local listo.");
    }

    private static string? FindOllamaExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Ollama",
                "ollama.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Ollama",
                "ollama.exe"),

            "ollama.exe"
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Equals(
                    "ollama.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var process = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "where.exe",
                            Arguments = "ollama.exe",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        });

                    var output = process?.StandardOutput
                        .ReadToEnd()
                        .Trim();

                    process?.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        return output
                            .Split(
                                ['\r', '\n'],
                                StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault();
                    }
                }
                catch
                {
                    // Se continúa con las rutas conocidas.
                }

                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task InstallOllamaAsync(
        CancellationToken cancellationToken)
    {
        var winget = new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments =
                "install --id Ollama.Ollama -e --silent " +
                "--accept-package-agreements " +
                "--accept-source-agreements",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            await RunProcessAsync(
                winget,
                cancellationToken);
            return;
        }
        catch
        {
            // Fallback al instalador oficial.
        }

        var installerPath = Path.Combine(
            Path.GetTempPath(),
            "OllamaSetup.exe");

        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        await using (var file = File.Create(installerPath))
        await using (var stream = await httpClient.GetStreamAsync(
                         "https://ollama.com/download/OllamaSetup.exe",
                         cancellationToken))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        var installer = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/S",
            UseShellExecute = true
        };

        using var process = Process.Start(installer)
            ?? throw new InvalidOperationException(
                "No fue posible iniciar el instalador de Ollama.");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"El instalador local terminó con código " +
                $"{process.ExitCode}.");
        }
    }

    private static void StartOllamaServer(string executable)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
    }

    private static async Task WaitForServerAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsServerAvailableAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);
        }

        throw new InvalidOperationException(
            "El motor local no pudo iniciarse.");
    }

    private static async Task<bool> IsServerAvailableAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            using var response = await client.GetAsync(
                TagsEndpoint,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsModelInstalledAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var json = await client.GetStringAsync(
                TagsEndpoint,
                cancellationToken);

            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "models",
                    out var models))
            {
                return false;
            }

            return models.EnumerateArray().Any(model =>
            {
                var name = model.TryGetProperty(
                    "name",
                    out var nameElement)
                    ? nameElement.GetString()
                    : null;

                return string.Equals(
                    name,
                    ModelName,
                    StringComparison.OrdinalIgnoreCase);
            });
        }
        catch
        {
            return false;
        }
    }

    private static Task RunProcessAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken) =>
        RunProcessAsync(
            new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            cancellationToken);

    private static async Task RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"No fue posible iniciar {startInfo.FileName}.");

        var outputTask = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? output
                    : error);
        }
    }
}
