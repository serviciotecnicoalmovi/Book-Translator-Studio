using System;
using System.Windows;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Views;

/// <summary>
/// Ventana que aparece únicamente cuando faltan datos para iniciar la traducción.
/// </summary>
public partial class TranslationSetupWindow : Window
{
    private readonly BookProject _project;
    private readonly EngineProfile _profile;

    public TranslationSetupWindow(
        BookProject project,
        EngineProfile profile,
        string sessionApiKey)
    {
        InitializeComponent();

        _project = project;
        _profile = profile;

        ProtocolBox.ItemsSource =
            Enum.GetValues<TranslationProtocol>();
        ProtocolBox.SelectedItem = profile.Protocol;

        EndpointBox.Text = profile.Endpoint;
        ModelBox.Text = profile.Model;
        ApiKeyBox.Password = sessionApiKey;
        SourceLanguageBox.Text =
            project.Translation.SourceLanguageCode;
        TargetLanguageBox.Text =
            project.Translation.TargetLanguageCode;
        RememberKeyBox.IsChecked = profile.RememberApiKey;
    }

    public string SessionApiKey => ApiKeyBox.Password;

    private void SaveButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EndpointBox.Text))
        {
            MessageBox.Show(
                "Debes indicar la URL del motor.",
                "Configuración",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _profile.Protocol =
            (TranslationProtocol)ProtocolBox.SelectedItem;
        _profile.Endpoint = EndpointBox.Text.Trim();
        _profile.Model = ModelBox.Text.Trim();
        _profile.RememberApiKey =
            RememberKeyBox.IsChecked == true;
        _profile.ApiKey = _profile.RememberApiKey
            ? ApiKeyBox.Password
            : string.Empty;

        _project.Translation.SourceLanguageCode =
            string.IsNullOrWhiteSpace(SourceLanguageBox.Text)
                ? "auto"
                : SourceLanguageBox.Text.Trim();

        _project.Translation.TargetLanguageCode =
            string.IsNullOrWhiteSpace(TargetLanguageBox.Text)
                ? "es"
                : TargetLanguageBox.Text.Trim();

        _project.Translation.IsConfigured = true;
        DialogResult = true;
    }
}
