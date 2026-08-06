using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

public sealed record TranslationRequest(
    string Text,
    string SourceCode,
    string SourceName,
    string TargetCode,
    string TargetName,
    string Instruction,
    EngineProfile Profile,
    string SessionApiKey);
