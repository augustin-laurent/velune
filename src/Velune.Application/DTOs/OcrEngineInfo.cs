namespace Velune.Application.DTOs;

public sealed record OcrEngineInfo(
    string EngineName,
    string EngineVersion,
    IReadOnlyList<string> AvailableLanguages);
