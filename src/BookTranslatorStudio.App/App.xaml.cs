using System.Windows;
using System.Windows.Threading;
using BookTranslatorStudio.Services;

namespace BookTranslatorStudio;

/// <summary>
/// Punto de entrada de la aplicación y control central de errores no administrados.
/// </summary>
public partial class App : Application
{
    private readonly IApplicationLogger _logger = new FileApplicationLogger();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _logger.Info("Book Translator Studio v0.1.0 iniciado.");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Info("Book Translator Studio finalizado.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.Error("Error no controlado en la interfaz.", e.Exception);
        MessageBox.Show(
            "Se produjo un error inesperado. El detalle fue guardado en el registro.",
            "Book Translator Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.Error("Error no controlado del dominio de aplicación.", exception);
        }
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error("Error no observado en una tarea.", e.Exception);
        e.SetObserved();
    }
}
