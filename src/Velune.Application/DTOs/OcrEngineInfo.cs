namespace Velune.Application.DTOs;

/// <summary>Describes the capabilities and version of an OCR engine.</summary>
/// <param name="EngineName">Display name of the OCR engine.</param>
/// <param name="EngineVersion">Version string of the engine.</param>
/// <param name="AvailableLanguages">Languages supported by the engine.</param>
public sealed record OcrEngineInfo(
    string EngineName,
    string EngineVersion,
    IReadOnlyList<string> AvailableLanguages);
