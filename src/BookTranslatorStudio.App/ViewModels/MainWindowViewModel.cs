using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using BookTranslatorStudio.Commands;
using BookTranslatorStudio.Models;
using BookTranslatorStudio.Services;
using BookTranslatorStudio.Views;
using Microsoft.Win32;

namespace BookTranslatorStudio.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IPdfInspectionService _pdfInspectionService;
    private readonly IBookPreparationService _bookPreparationService;
    private readonly IBookProjectService _bookProjectService;
    private readonly IApplicationLogger _logger;
    private readonly TranslatedPdfExporter _pdfExporter;

    private BookProject? _project;
    private BookSection? _selectedSection;
    private BookBlock? _selectedBlock;
    private EngineProfile? _selectedEngineProfile;
    private string? _projectFilePath;
    private string _statusMessage =
        "Importa un PDF o abre un proyecto existente.";
    private bool _isBusy;
    private bool _isTranslating;
    private bool _isPaused;
    private bool _hasUnsavedChanges;
    private string _sessionApiKey = string.Empty;
    private CancellationTokenSource? _translationCancellation;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public MainWindowViewModel()
    {
        _pdfInspectionService = new PdfInspectionService();
        _bookPreparationService = new BookPreparationService();
        _bookProjectService = new BookProjectService();
        _logger = new FileApplicationLogger();
        _pdfExporter = new TranslatedPdfExporter();

        Sections = [];
        Blocks = [];
        EngineProfiles = [];

        OpenBookCommand = new RelayCommand(
            async _ => await OpenBookAsync(),
            _ => !IsBusy);
        ImportPdfCommand = new RelayCommand(
            async _ => await ImportPdfAsync(),
            _ => !IsBusy);
        OpenProjectCommand = new RelayCommand(
            async _ => await OpenProjectAsync(),
            _ => !IsBusy);
        SaveProjectCommand = new RelayCommand(
            async _ => await SaveProjectAsync(false),
            _ => Project is not null);
        SaveProjectAsCommand = new RelayCommand(
            async _ => await SaveProjectAsync(true),
            _ => Project is not null);
        StartTranslationCommand = new RelayCommand(
            async _ => await StartTranslationAsync(),
            _ => CanStartTranslation());
        PauseTranslationCommand = new RelayCommand(
            _ => PauseTranslation(),
            _ => IsTranslating);
        AddEngineProfileCommand = new RelayCommand(
            _ => AddEngineProfile(),
            _ => Project is not null);
        RemoveEngineProfileCommand = new RelayCommand(
            _ => RemoveEngineProfile(),
            _ => Project is not null && SelectedEngineProfile is not null);
        ResetFailedCommand = new RelayCommand(
            _ => ResetFailedBlocks(),
            _ => Project is not null);
    }

    public ICommand OpenBookCommand { get; }
    public ICommand ImportPdfCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand SaveProjectAsCommand { get; }
    public ICommand StartTranslationCommand { get; }
    public ICommand PauseTranslationCommand { get; }
    public ICommand AddEngineProfileCommand { get; }
    public ICommand RemoveEngineProfileCommand { get; }
    public ICommand ResetFailedCommand { get; }

    public ObservableCollection<BookSection> Sections { get; }
    public ObservableCollection<BookBlock> Blocks { get; }
    public ObservableCollection<EngineProfile> EngineProfiles { get; }

    public Array AvailableProtocols =>
        Enum.GetValues<TranslationProtocol>();

    public Array AvailableScopes =>
        Enum.GetValues<TranslationScope>();

    public BookProject? Project
    {
        get => _project;
        private set
        {
            if (SetProperty(ref _project, value))
            {
                OnPropertyChanged(nameof(HasProject));
                RefreshMetrics();
                RaiseCommands();
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

    public EngineProfile? SelectedEngineProfile
    {
        get => _selectedEngineProfile;
        set
        {
            if (SetProperty(ref _selectedEngineProfile, value) &&
                Project is not null)
            {
                Project.Translation.SelectedEngineProfileId =
                    value?.Id;
                SessionApiKey = value?.ApiKey ?? string.Empty;
                MarkChanged();
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
            SelectedBlock.TranslatedText = string.Empty;
            SelectedBlock.TranslationStatus =
                TranslationBlockStatus.Pending;
            MarkChanged();
            RefreshBlockList();
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
            SelectedBlock.TranslationStatus =
                string.IsNullOrWhiteSpace(value)
                    ? TranslationBlockStatus.Pending
                    : TranslationBlockStatus.Completed;
            SelectedBlock.TranslatedUtc =
                string.IsNullOrWhiteSpace(value)
                    ? null
                    : DateTime.UtcNow;
            MarkChanged();
            RefreshBlockList();
            RefreshMetrics();
        }
    }

    public string SourceLanguageCode
    {
        get => Project?.Translation.SourceLanguageCode ?? "auto";
        set
        {
            if (Project is null) return;
            Project.Translation.SourceLanguageCode = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string SourceLanguageName
    {
        get => Project?.Translation.SourceLanguageName
            ?? "Detección automática";
        set
        {
            if (Project is null) return;
            Project.Translation.SourceLanguageName = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string TargetLanguageCode
    {
        get => Project?.Translation.TargetLanguageCode ?? "es";
        set
        {
            if (Project is null) return;
            Project.Translation.TargetLanguageCode = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string TargetLanguageName
    {
        get => Project?.Translation.TargetLanguageName ?? "Español";
        set
        {
            if (Project is null) return;
            Project.Translation.TargetLanguageName = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string TranslationInstruction
    {
        get => Project?.Translation.Instruction ?? string.Empty;
        set
        {
            if (Project is null) return;
            Project.Translation.Instruction = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string ConcurrentRequestsText
    {
        get => (Project?.Translation.ConcurrentRequests ?? 1)
            .ToString();
        set
        {
            if (Project is null ||
                !int.TryParse(value, out var parsed))
            {
                return;
            }

            Project.Translation.ConcurrentRequests = parsed;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string RetryCountText
    {
        get => (Project?.Translation.RetryCount ?? 0)
            .ToString();
        set
        {
            if (Project is null ||
                !int.TryParse(value, out var parsed))
            {
                return;
            }

            Project.Translation.RetryCount = parsed;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string RetryDelayText
    {
        get => (Project?.Translation.RetryDelaySeconds ?? 2)
            .ToString();
        set
        {
            if (Project is null ||
                !int.TryParse(value, out var parsed))
            {
                return;
            }

            Project.Translation.RetryDelaySeconds = parsed;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public TranslationScope SelectedScope
    {
        get => Project?.Translation.Scope
            ?? TranslationScope.PendingBlocks;
        set
        {
            if (Project is null) return;
            Project.Translation.Scope = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string SessionApiKey
    {
        get => _sessionApiKey;
        set => SetProperty(ref _sessionApiKey, value);
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
                RaiseCommands();
            }
        }
    }

    public bool IsTranslating
    {
        get => _isTranslating;
        private set
        {
            if (SetProperty(ref _isTranslating, value))
            {
                RaiseCommands();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public double TranslationProgress =>
        Project?.TranslationProgress ?? 0;

    public string TranslationSummary =>
        Project is null
            ? "Sin proyecto"
            : $"{Project.TranslatedBlockCount:N0} traducidos · " +
              $"{Project.FailedBlockCount:N0} con error · " +
              $"{Project.BlockCount:N0} totales";


    private async Task OpenBookAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir PDF o recuperación",
            Filter =
                $"Libros y proyectos (*.pdf;*{BookProject.FileExtension})|" +
                $"*.pdf;*{BookProject.FileExtension}|" +
                "Documentos PDF (*.pdf)|*.pdf|" +
                $"Proyectos Book Translator Studio (*{BookProject.FileExtension})|" +
                $"*{BookProject.FileExtension}",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var extension = Path.GetExtension(dialog.FileName);

        if (string.Equals(
                extension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            await ImportPdfFromPathAsync(dialog.FileName);
            return;
        }

        if (string.Equals(
                extension,
                BookProject.FileExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            await OpenProjectFromPathAsync(dialog.FileName);
            return;
        }

        MessageBox.Show(
            "El archivo seleccionado no es un PDF ni un proyecto compatible.",
            "Book Translator Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task ImportPdfFromPathAsync(string filePath)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Preparando el libro completo...";

            var document = await _pdfInspectionService
                .InspectAsync(filePath);

            var project = _bookPreparationService.Prepare(document);

            LoadProject(
                project,
                BuildRecoveryPath(filePath));

            HasUnsavedChanges = true;
            await AutoSaveAsync();
            StatusMessage =
                "Libro preparado. Pulsa Traducir / continuar.";
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

    private async Task OpenProjectFromPathAsync(string filePath)
    {
        try
        {
            IsBusy = true;

            var project = await _bookProjectService
                .OpenAsync(filePath);

            NormalizeProject(project);
            LoadProject(project, filePath);
            HasUnsavedChanges = false;
            StatusMessage =
                "Proyecto abierto. Puedes continuar la traducción.";
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

    private async Task ImportPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar libro PDF",
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Preparando el libro completo...";
            var document = await _pdfInspectionService
                .InspectAsync(dialog.FileName);
            LoadProject(
                _bookPreparationService.Prepare(document),
                null);
            HasUnsavedChanges = true;
            StatusMessage = "Libro preparado y listo para traducir.";
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
                $"Proyecto (*{BookProject.FileExtension})|" +
                $"*{BookProject.FileExtension}",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsBusy = true;
            var project = await _bookProjectService
                .OpenAsync(dialog.FileName);
            NormalizeProject(project);
            LoadProject(project, dialog.FileName);
            HasUnsavedChanges = false;
            StatusMessage =
                "Proyecto abierto. Puedes continuar la traducción.";
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

    private async Task<bool> SaveProjectAsync(bool saveAs)
    {
        if (Project is null) return false;

        var path = _projectFilePath;

        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Guardar proyecto",
                Filter =
                    $"Proyecto (*{BookProject.FileExtension})|" +
                    $"*{BookProject.FileExtension}",
                DefaultExt = BookProject.FileExtension,
                AddExtension = true,
                FileName = Project.Name
            };

            if (dialog.ShowDialog() != true) return false;
            path = dialog.FileName;
        }

        await SaveInternalAsync(path, CancellationToken.None);
        _projectFilePath = path;
        StatusMessage =
            $"Proyecto guardado: {Path.GetFileName(path)}";
        return true;
    }

    private async Task StartTranslationAsync()
    {
        if (Project is null ||
            SelectedEngineProfile is null)
        {
            return;
        }

        if (!EnsureTranslationConfiguration())
        {
            StatusMessage = "Traducción cancelada.";
            return;
        }

        if (!await EnsureSavedAsync()) return;

        var blocks = ResolveScope();
        if (blocks.Count == 0)
        {
            StatusMessage =
                "No hay bloques aplicables al alcance seleccionado.";
            return;
        }

        if (SelectedEngineProfile.RememberApiKey)
        {
            SelectedEngineProfile.ApiKey = SessionApiKey;
        }
        else
        {
            SelectedEngineProfile.ApiKey = string.Empty;
        }

        _translationCancellation?.Dispose();
        _translationCancellation = new CancellationTokenSource();

        IsTranslating = true;
        IsBusy = true;
        IsPaused = false;
        StatusMessage =
            "Iniciando el motor y enviando el primer bloque...";

        try
        {
            var profile = SelectedEngineProfile.Clone();
            var engine = new ConfigurableTranslationEngine();
            var coordinator = new TranslationCoordinator(engine);

            await coordinator.RunAsync(
                blocks,
                block => new TranslationRequest(
                    block.OriginalText,
                    SourceLanguageCode,
                    SourceLanguageName,
                    TargetLanguageCode,
                    TargetLanguageName,
                    TranslationInstruction,
                    profile,
                    SessionApiKey),
                Math.Max(1, Project.Translation.ConcurrentRequests),
                1,
                Math.Max(0, Project.Translation.RetryDelaySeconds),
                async block =>
                {
                    await Application.Current.Dispatcher.InvokeAsync(
                        () =>
                        {
                            SelectedSection =
                                Project.Sections.First(
                                    section =>
                                        section.Blocks.Contains(block));
                            SelectedBlock = block;
                            RefreshBlockList();
                            RefreshMetrics();
                            StatusMessage =
                                $"Procesados: " +
                                $"{Project.TranslatedBlockCount:N0} de " +
                                $"{Project.BlockCount:N0}.";
                        });

                    await AutoSaveAsync();
                },
                _translationCancellation.Token);

            var failedBlocks = Project.Sections
                .SelectMany(section => section.Blocks)
                .Where(block =>
                    block.TranslationStatus ==
                    TranslationBlockStatus.Failed)
                .OrderBy(block => block.Order)
                .ToList();

            if (failedBlocks.Count > 0)
            {
                Project.Translation.IsConfigured = false;

                var firstFailure = failedBlocks[0];
                StatusMessage =
                    $"El motor no inició la traducción: " +
                    $"{firstFailure.TranslationError}";

                MessageBox.Show(
                    $"No fue posible traducir el primer bloque.\n\n" +
                    $"{firstFailure.TranslationError}\n\n" +
                    "Pulsa Traducir / continuar para corregir la conexión.",
                    "Motor de traducción",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                var outputPath = BuildTranslatedPdfPath(
                    Project.SourcePdfPath,
                    Project.Name);

                StatusMessage =
                    "Generando el PDF traducido...";

                await _pdfExporter.ExportAsync(
                    Project,
                    outputPath,
                    CancellationToken.None);

                StatusMessage =
                    $"PDF traducido generado: " +
                    $"{Path.GetFileName(outputPath)}";

                MessageBox.Show(
                    $"La traducción terminó correctamente.\n\n" +
                    $"PDF generado:\n{outputPath}",
                    "Book Translator Studio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (OperationCanceledException)
        {
            IsPaused = true;
            StatusMessage =
                "Traducción pausada. El progreso fue guardado.";
            await AutoSaveAsync();
        }
        finally
        {
            IsTranslating = false;
            IsBusy = false;
            RefreshMetrics();
        }
    }

    private bool EnsureTranslationConfiguration()
    {
        if (Project is null ||
            SelectedEngineProfile is null)
        {
            return false;
        }

        var apiKeyMissing =
            SelectedEngineProfile.Protocol ==
                TranslationProtocol.OpenAiChatCompletions &&
            string.IsNullOrWhiteSpace(SessionApiKey) &&
            string.IsNullOrWhiteSpace(
                SelectedEngineProfile.ApiKey);

        var modelMissing =
            SelectedEngineProfile.Protocol is
                TranslationProtocol.OpenAiChatCompletions or
                TranslationProtocol.OllamaGenerate &&
            string.IsNullOrWhiteSpace(
                SelectedEngineProfile.Model);

        var endpointMissing =
            string.IsNullOrWhiteSpace(
                SelectedEngineProfile.Endpoint);

        var firstConfiguration =
            !Project.Translation.IsConfigured;

        if (!firstConfiguration &&
            !apiKeyMissing &&
            !modelMissing &&
            !endpointMissing)
        {
            return true;
        }

        var dialog = new TranslationSetupWindow(
            Project,
            SelectedEngineProfile,
            SessionApiKey)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        SessionApiKey = dialog.SessionApiKey;
        Project.Translation.IsConfigured = true;
        HasUnsavedChanges = true;
        StatusMessage =
            "Configuración guardada. Iniciando traducción...";
        return true;
    }


    private static string BuildRecoveryPath(string sourcePdfPath)
    {
        var recoveryFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "BookTranslatorStudio",
            "Recovery");

        Directory.CreateDirectory(recoveryFolder);

        var baseName = string.IsNullOrWhiteSpace(sourcePdfPath)
            ? $"project-{Guid.NewGuid():N}"
            : Path.GetFileNameWithoutExtension(sourcePdfPath);

        return Path.Combine(
            recoveryFolder,
            $"{baseName}{BookProject.FileExtension}");
    }

    private static string BuildTranslatedPdfPath(
        string sourcePdfPath,
        string projectName)
    {
        var sourceFolder =
            Path.GetDirectoryName(sourcePdfPath);

        if (string.IsNullOrWhiteSpace(sourceFolder) ||
            !Directory.Exists(sourceFolder))
        {
            sourceFolder =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
        }

        var baseName = string.IsNullOrWhiteSpace(sourcePdfPath)
            ? projectName
            : Path.GetFileNameWithoutExtension(sourcePdfPath);

        var outputPath = Path.Combine(
            sourceFolder,
            $"{baseName}_traducido.pdf");

        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        return Path.Combine(
            sourceFolder,
            $"{baseName}_traducido_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    private void PauseTranslation()
    {
        StatusMessage = "Pausando solicitudes activas...";
        _translationCancellation?.Cancel();
    }

    private IReadOnlyList<BookBlock> ResolveScope()
    {
        if (Project is null)
        {
            return [];
        }

        return Project.Sections
            .SelectMany(section => section.Blocks)
            .Where(block => !block.IsTranslated)
            .OrderBy(block => block.Order)
            .ToList();
    }

    private async Task<bool> EnsureSavedAsync()
    {
        if (Project is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            _projectFilePath =
                BuildRecoveryPath(Project.SourcePdfPath);
        }

        await AutoSaveAsync();
        return true;
    }

    private async Task AutoSaveAsync()
    {
        if (Project is null ||
            string.IsNullOrWhiteSpace(_projectFilePath))
        {
            return;
        }

        await SaveInternalAsync(
            _projectFilePath,
            CancellationToken.None);
    }

    private async Task SaveInternalAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (Project is null) return;

        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await _bookProjectService.SaveAsync(
                Project,
                path,
                cancellationToken);
            HasUnsavedChanges = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void AddEngineProfile()
    {
        if (Project is null) return;

        var profile = new EngineProfile
        {
            Name =
                $"Motor personalizado " +
                $"{Project.Translation.EngineProfiles.Count + 1}"
        };

        Project.Translation.EngineProfiles.Add(profile);
        EngineProfiles.Add(profile);
        SelectedEngineProfile = profile;
        MarkChanged();
    }

    private void RemoveEngineProfile()
    {
        if (Project is null ||
            SelectedEngineProfile is null)
        {
            return;
        }

        Project.Translation.EngineProfiles.Remove(
            SelectedEngineProfile);
        EngineProfiles.Remove(SelectedEngineProfile);
        SelectedEngineProfile = EngineProfiles.FirstOrDefault();
        MarkChanged();
    }

    private void ResetFailedBlocks()
    {
        if (Project is null) return;

        foreach (var block in Project.Sections
                     .SelectMany(section => section.Blocks)
                     .Where(block =>
                         block.TranslationStatus ==
                         TranslationBlockStatus.Failed))
        {
            block.TranslationStatus =
                TranslationBlockStatus.Pending;
            block.TranslationError = string.Empty;
        }

        RefreshBlockList();
        RefreshMetrics();
        MarkChanged();
    }

    private void LoadProject(
        BookProject project,
        string? path)
    {
        Project = project;
        _projectFilePath = path;

        Sections.Clear();
        foreach (var section in project.Sections)
        {
            Sections.Add(section);
        }

        EngineProfiles.Clear();
        foreach (var profile in
                 project.Translation.EngineProfiles)
        {
            EngineProfiles.Add(profile);
        }

        SelectedEngineProfile =
            EngineProfiles.FirstOrDefault(profile =>
                profile.Id ==
                project.Translation.SelectedEngineProfileId)
            ?? EngineProfiles.FirstOrDefault();

        SelectedSection = Sections.FirstOrDefault();
        SelectedBlock = Blocks.FirstOrDefault();

        OnPropertyChanged(nameof(SourceLanguageCode));
        OnPropertyChanged(nameof(SourceLanguageName));
        OnPropertyChanged(nameof(TargetLanguageCode));
        OnPropertyChanged(nameof(TargetLanguageName));
        OnPropertyChanged(nameof(TranslationInstruction));
        OnPropertyChanged(nameof(ConcurrentRequestsText));
        OnPropertyChanged(nameof(RetryCountText));
        OnPropertyChanged(nameof(RetryDelayText));
        OnPropertyChanged(nameof(SelectedScope));
        RefreshMetrics();
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

    private static void NormalizeProject(BookProject project)
    {
        project.Translation ??= new TranslationConfiguration();
        project.Translation.Scope =
            TranslationScope.PendingBlocks;
        project.Translation.RetryCount = 1;

        if (project.Translation.EngineProfiles.Count == 0)
        {
            project.Translation =
                new TranslationConfiguration();
        }

        foreach (var block in project.Sections
                     .SelectMany(section => section.Blocks))
        {
            if (block.TranslationStatus ==
                TranslationBlockStatus.InProgress)
            {
                block.TranslationStatus =
                    TranslationBlockStatus.Pending;
            }

            if (!string.IsNullOrWhiteSpace(
                    block.TranslatedText))
            {
                block.TranslationStatus =
                    TranslationBlockStatus.Completed;
            }
        }
    }

    private bool CanStartTranslation() =>
        Project is not null &&
        SelectedEngineProfile is not null &&
        !IsTranslating;

    private void RefreshBlockList()
    {
        if (SelectedSection is null) return;

        var selectedId = SelectedBlock?.Id;
        LoadBlocks(SelectedSection);
        SelectedBlock = Blocks.FirstOrDefault(
            block => block.Id == selectedId)
            ?? Blocks.FirstOrDefault();
    }

    private void RefreshMetrics()
    {
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(TranslationProgress));
        OnPropertyChanged(nameof(TranslationSummary));
        RaiseCommands();
    }

    private void MarkChanged()
    {
        HasUnsavedChanges = true;
        StatusMessage =
            "Proyecto modificado. Hay cambios sin guardar.";
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        foreach (var command in new[]
                 {
                     OpenBookCommand,
                     ImportPdfCommand,
                     OpenProjectCommand,
                     SaveProjectCommand,
                     SaveProjectAsCommand,
                     StartTranslationCommand,
                     PauseTranslationCommand,
                     AddEngineProfileCommand,
                     RemoveEngineProfileCommand,
                     ResetFailedCommand
                 }.OfType<RelayCommand>())
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void HandleError(
        string message,
        Exception exception)
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
