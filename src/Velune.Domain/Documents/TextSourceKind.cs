namespace Velune.Domain.Documents;

/// <summary>
/// Indicates how text was extracted from a document page.
/// </summary>
public enum TextSourceKind
{
    /// <summary>
    /// Text was extracted from the embedded PDF text layer.
    /// </summary>
    EmbeddedPdfText = 0,

    /// <summary>
    /// Text was obtained via optical character recognition.
    /// </summary>
    Ocr = 1
}
