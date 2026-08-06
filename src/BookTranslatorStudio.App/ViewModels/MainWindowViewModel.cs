using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BookTranslatorStudio.Commands;
using BookTranslatorStudio.Models;
using BookTranslatorStudio.Services;
using Microsoft.Win32;

namespace BookTranslatorStudio.ViewModels;

/// <summary>
/// Gestiona el ciclo completo: importar, preparar, editar, guardar y abrir.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IPdfInspectionService _pdfInspectionService;
    private readonly IBookPreparationService _bookPreparationService;
    private readonly IBookProjectService _bookProjectService;
    private readonly IApplicationLogger _logger;

    private BookProject? _project;
    private BookSection? _selectedSection;
    private BookBlock? _selectedBlock;
    private string? _projectFilePath;
    private string _statusMessage =
        "Importa un PDF o abre un proyecto existente.";
    private bool _isBusy;
    private bool _hasUnsavedChanges;

    public MainWindowViewModel()
        : this(
            new PdfInspectionService(),
            new BookPreparationService(),
            new BookProjectService(),
            new FileApplicationLogger())
    {
    }

    internal MainWindowViewModel(
        IPdfInspectionService pdfInspectionService,
        IBookPreparationService bookPreparationService,
        IBookProjectService bookProjectService,
        IApplicationLogger logger)
    {
        _pdfInspectionService = pdfInspectionService;
        _bookPreparationService = bookPreparationService;
        _bookProjectService = bookProjectService;
        _logger = logger;

        Sections = [];
        Blocks = [];

        ImportPdfCommand = new RelayCommand(
            async _ => await ImportPdfAsync(),
            _ => !IsBusy);

        OpenProjectCommand = new RelayCommand(
            async _ => await OpenProjectAsync(),
            _ => !IsBusy);

        SaveProjectCommand = new RelayCommand(
            async _ => await SaveProjectAsync(false),
            _ => Project is not null && !IsBusy);

        SaveProjectAsCommand = new RelayCommand(
            async _ => await SaveProjectAsync(true),
            _ => Project is not null && !IsBusy);
    }

    public ICommand ImportPdfCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand SaveProjectAsCommand { get; }

    public ObservableCollection<BookSection> Sections { get; }

    public ObservableCollection<BookBlock> Blocks { get; }

    public BookProject? Project
    {
        get => _project;
        private set
        {
            if (SetProperty(ref _project, value))
            {
                OnPropertyChanged(nameof(HasProject));
                RaiseCommandStates();
            }
        }
    }

    public bool HasProject => Project is not null;

    public BookSection? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                LoadBlocks(value);
            }
        }
    }

    public BookBlock? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (SetProperty(ref _selectedBlock, value))
            {
                OnPropertyChanged(nameof(EditableOriginalText));
                OnPropertyChanged(nameof(EditableTranslatedText));
            }
        }
    }

    public string EditableOriginalText
    {
        get => SelectedBlock?.OriginalText ?? string.Empty;
        set
        {
            if (SelectedBlock is null ||
                SelectedBlock.OriginalText == value)
            {
                return;
            }

            SelectedBlock.OriginalText = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Project));
        }
    }

    public string EditableTranslatedText
    {
        get => SelectedBlock?.TranslatedText ?? string.Empty;
        set
        {
            if (SelectedBlock is null ||
                SelectedBlock.TranslatedText == value)
            {
                return;
            }

            SelectedBlock.TranslatedText = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetProperty(ref _hasUnsavedChanges, value);
    }

    private async Task ImportPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar libro PDF",
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage =
                "Importando y preparando el libro completo...";

            var document = await _pdfInspectionService
                .InspectAsync(dialog.FileName);

            var project = _bookPreparationService.Prepare(document);

            LoadProject(project, null);
            HasUnsavedChanges = true;

            StatusMessage =
                $"Libro preparado: {project.Sections.Count:N0} secciones, " +
                $"{project.BlockCount:N0} bloques y {project.WordCount:N0} palabras.";

            _logger.Info(
                $"Libro preparado: {dialog.FileName}. " +
                $"Secciones: {project.Sections.Count}. " +
                $"Bloques: {project.BlockCount}.");
        }
        catch (Exception exception)
        {
            HandleError("No fue posible importar el libro.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir proyecto",
            Filter =
                $"Proyecto Book Translator Studio (*{BookProject.FileExtension})|" +
                $"*{BookProject.FileExtension}",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Abriendo proyecto...";

            var project = await _bookProjectService
                .OpenAsync(dialog.FileName);

            LoadProject(project, dialog.FileName);
            HasUnsavedChanges = false;

            StatusMessage =
                $"Proyecto abierto: {project.Name}. " +
                $"{project.BlockCount:N0} bloques listos.";

            _logger.Info($"Proyecto abierto: {dialog.FileName}");
        }
        catch (Exception exception)
        {
            HandleError("No fue posible abrir el proyecto.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveProjectAsync(bool forceNewPath)
    {
        if (Project is null)
        {
            return;
        }

        var destination = _projectFilePath;

        if (forceNewPath || string.IsNullOrWhiteSpace(destination))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Guardar proyecto",
                Filter =
                    $"Proyecto Book Translator Studio (*{BookProject.FileExtension})|" +
                    $"*{BookProject.FileExtension}",
                DefaultExt = BookProject.FileExtension,
                AddExtension = true,
                FileName = Project.Name
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            destination = dialog.FileName;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Guardando proyecto...";

            await _bookProjectService.SaveAsync(
                Project,
                destination);

            _projectFilePath = destination;
            HasUnsavedChanges = false;

            StatusMessage =
                $"Proyecto guardado: {Path.GetFileName(destination)}";

            _logger.Info($"Proyecto guardado: {destination}");
        }
        catch (Exception exception)
        {
            HandleError("No fue posible guardar el proyecto.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadProject(BookProject project, string? filePath)
    {
        Project = project;
        _projectFilePath = filePath;

        Sections.Clear();
        foreach (var section in project.Sections)
        {
            Sections.Add(section);
        }

        SelectedSection = Sections.FirstOrDefault();
        SelectedBlock = Blocks.FirstOrDefault();

        OnPropertyChanged(nameof(Project));
    }

    private void LoadBlocks(BookSection? section)
    {
        Blocks.Clear();

        if (section is not null)
        {
            foreach (var block in section.Blocks)
            {
                Blocks.Add(block);
            }
        }

        SelectedBlock = Blocks.FirstOrDefault();
    }

    private void MarkChanged()
    {
        HasUnsavedChanges = true;
        StatusMessage = "Proyecto modificado. Hay cambios sin guardar.";
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[]
                 {
                     ImportPdfCommand,
                     OpenProjectCommand,
                     SaveProjectCommand,
                     SaveProjectAsCommand
                 }.OfType<RelayCommand>())
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void HandleError(string message, Exception exception)
    {
        _logger.Error(message, exception);
        StatusMessage = message;

        MessageBox.Show(
            exception.Message,
            "Book Translator Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
