namespace BookTranslatorStudio.Services;

/// <summary>
/// Contrato mínimo para el registro de actividad y errores.
/// </summary>
public interface IApplicationLogger
{
    void Info(string message);
    void Error(string message, Exception exception);
}
