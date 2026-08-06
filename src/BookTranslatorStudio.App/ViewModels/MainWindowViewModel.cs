using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BookTranslatorStudio.Commands;
using BookTranslatorStudio.Models;
using BookTranslatorStudio.Services;
using Microsoft.Win32;

namespace BookTranslatorStudio.ViewModels;

/// <summary>
/// Coordina la selección, lectura y previsualización del contenido PDF.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IPdfInspectionService _pdfInspectionService;
    private readonly IApplicationLogger _logger;

    private PdfDocumentInfo? _selectedDocument;
    private PdfPageContent? _selectedPage;
    private string _statusMessage =
        "Selecciona un libro PDF para comenzar.";
    private bool _isDocumentLoaded;
    private bool _isBusy;

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

        Pages = new ObservableCollection<PdfPageContent>();

        SelectPdfCommand = new RelayCommand(
            async _ => await SelectPdfAsync(),
            _ => !IsBusy);

        ClearSelectionCommand = new RelayCommand(
            _ => ClearSelection(),
            _ => IsDocumentLoaded && !IsBusy);
    }

    public ICommand SelectPdfCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ObservableCollection<PdfPageContent> Pages { get; }

    public PdfDocumentInfo? SelectedDocument
    {
        get => _selectedDocument;
        private set => SetProperty(ref _selectedDocument, value);
    }

    public PdfPageContent? SelectedPage
    {
        get => _selectedPage;
        set => SetProperty(ref _selectedPage, value);
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
            if (SetProperty(ref _isDocumentLoaded, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private async Task SelectPdfAsync()
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
            IsBusy = true;
            StatusMessage = "Analizando páginas y extrayendo texto...";

            var document = await _pdfInspectionService
                .InspectAsync(dialog.FileName);

            SelectedDocument = document;

            Pages.Clear();
            foreach (var page in document.Pages)
            {
                Pages.Add(page);
            }

            SelectedPage = Pages.FirstOrDefault();
            IsDocumentLoaded = true;

            StatusMessage = document.RequiresOcr
                ? $"Documento cargado: {document.PageCount:N0} páginas. No se detectó texto; necesitará OCR."
                : $"Documento cargado: {document.PageCount:N0} páginas, {document.PagesWithText:N0} con texto.";

            _logger.Info(
                $"PDF analizado: {document.FullPath}. " +
                $"Páginas: {document.PageCount}. " +
                $"Caracteres: {document.TotalCharacterCount}.");
        }
        catch (Exception exception)
        {
            _logger.Error(
                "No fue posible analizar el PDF.",
                exception);

            ClearSelection();
            StatusMessage = "No fue posible analizar el documento.";

            MessageBox.Show(
                BuildUserErrorMessage(exception),
                "No se pudo abrir el PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearSelection()
    {
        SelectedDocument = null;
        SelectedPage = null;
        Pages.Clear();
        IsDocumentLoaded = false;
        StatusMessage = "Selecciona un libro PDF para comenzar.";
        _logger.Info("Selección de PDF eliminada.");
    }

    private void RaiseCommandStates()
    {
        if (SelectPdfCommand is RelayCommand selectCommand)
        {
            selectCommand.RaiseCanExecuteChanged();
        }

        if (ClearSelectionCommand is RelayCommand clearCommand)
        {
            clearCommand.RaiseCanExecuteChanged();
        }
    }

    private static string BuildUserErrorMessage(Exception exception) =>
        exception switch
        {
            UnauthorizedAccessException =>
                "Windows no permitió acceder al archivo seleccionado.",
            InvalidOperationException =>
                "El documento está cifrado, dañado o utiliza una estructura PDF no compatible.",
            _ => exception.Message
        };
}
