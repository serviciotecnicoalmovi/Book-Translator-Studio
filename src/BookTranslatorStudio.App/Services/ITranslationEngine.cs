namespace BookTranslatorStudio.Services;

public interface ITranslationEngine
{
    Task<string> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken);
}
