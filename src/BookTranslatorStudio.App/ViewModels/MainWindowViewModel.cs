using System.Windows;
using System.Windows.Input;
using BookTranslatorStudio.Commands;
using BookTranslatorStudio.Models;
using BookTranslatorStudio.Services;
using Microsoft.Win32;

namespace BookTranslatorStudio.ViewModels;

/// <summary>
/// Coordina la selección y el análisis inicial de documentos PDF.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IPdfInspectionService _pdfInspectionService;
    private readonly IApplicationLogger _logger;

    private PdfDocumentInfo? _selectedDocument;
    private string _statusMessage =
        "Selecciona un libro PDF para comenzar.";
    private bool _isDocumentLoaded;

    public MainWindowViewModel()
        : this(new PdfInspectionService(), new FileApplicationLogger())
    {
    }

    internal MainWindowViewModel(
        IPdfInspectionService pdfInspectionService,
        IApplicationLogger logger)
    {
        _pdfInspectionService = pdfInspectionService;
        _logger = logger;

        SelectPdfCommand = new RelayCommand(_ => SelectPdf());
        ClearSelectionCommand = new RelayCommand(
            _ => ClearSelection(),
            _ => IsDocumentLoaded);
    }

    public ICommand SelectPdfCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public PdfDocumentInfo? SelectedDocument
    {
        get => _selectedDocument;
        private set => SetProperty(ref _selectedDocument, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsDocumentLoaded
    {
        get => _isDocumentLoaded;
        private set
        {
            if (SetProperty(ref _isDocumentLoaded, value) &&
                ClearSelectionCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    private void SelectPdf()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar libro PDF",
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            StatusMessage = "Selección cancelada.";
            return;
        }

        try
        {
            SelectedDocument = _pdfInspectionService.Inspect(dialog.FileName);
            IsDocumentLoaded = true;
            StatusMessage =
                $"Documento cargado: {SelectedDocument.DetectedPageCount:N0} páginas detectadas.";

            _logger.Info(
                $"PDF cargado: {SelectedDocument.FullPath}");
        }
        catch (Exception exception)
        {
            _logger.Error("No fue posible cargar el PDF.", exception);

            SelectedDocument = null;
            IsDocumentLoaded = false;
            StatusMessage = "No fue posible cargar el documento.";

            MessageBox.Show(
                exception.Message,
                "No se pudo abrir el PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ClearSelection()
    {
        SelectedDocument = null;
        IsDocumentLoaded = false;
        StatusMessage = "Selecciona un libro PDF para comenzar.";
        _logger.Info("Selección de PDF eliminada.");
    }
}
