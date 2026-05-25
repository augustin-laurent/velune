using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels;

public sealed record WindowsTextAnnotationStyle(
    string FontFamily,
    double FontSize,
    bool IsBold,
    bool IsItalic,
    bool IsUnderline,
    TextAnnotationAlignment TextAlignment,
    string TextHex,
    string? BackgroundHex,
    string? BorderHex,
    double BorderWidth,
    double Opacity);
