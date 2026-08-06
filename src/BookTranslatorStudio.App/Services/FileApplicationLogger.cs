using System.IO;
using System.Text;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Guarda los registros diarios en la carpeta local del usuario.
/// </summary>
public sealed class FileApplicationLogger : IApplicationLogger
{
    private static readonly object SyncRoot = new();

    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BookTranslatorStudio",
        "Logs");

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception exception) =>
        Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);

            var logPath = Path.Combine(
                _logDirectory,
                $"BookTranslatorStudio-{DateTime.Now:yyyy-MM-dd}.log");

            var entry = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [")
                .Append(level)
                .Append("] ")
                .AppendLine(message);

            if (exception is not null)
            {
                entry.AppendLine(exception.ToString());
            }

            lock (SyncRoot)
            {
                File.AppendAllText(logPath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // El registro nunca debe cerrar la aplicaciÃ³n.
        }
    }
}

