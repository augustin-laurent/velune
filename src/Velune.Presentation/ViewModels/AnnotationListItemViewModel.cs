using Velune.Domain.Annotations;
using Velune.Presentation.Localization;

namespace Velune.Presentation.ViewModels;

/// <summary>
/// View model for displaying an annotation entry in the annotations panel list.
/// </summary>
public sealed class AnnotationListItemViewModel
{
    /// <summary>
    /// Initializes a new instance wrapping the given annotation.
    /// </summary>
    /// <param name="annotation">The document annotation.</param>
    /// <param name="localizationService">Localization service for kind labels.</param>
    public AnnotationListItemViewModel(DocumentAnnotation annotation, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(localizationService);

        Annotation = annotation;
        UpdateLocalization(localizationService);
    }

    /// <summary>
    /// Gets the underlying document annotation.
    /// </summary>
    public DocumentAnnotation Annotation
    {
        get;
    }

    /// <summary>
    /// Gets the annotation identifier.
    /// </summary>
    public Guid Id => Annotation.Id;

    /// <summary>
    /// Gets the annotation kind.
    /// </summary>
    public DocumentAnnotationKind Kind => Annotation.Kind;

    /// <summary>
    /// Gets the annotation text content, if any.
    /// </summary>
    public string? Text => Annotation.Text;

    /// <summary>
    /// Gets the associated asset identifier, if any.
    /// </summary>
    public string? AssetId => Annotation.AssetId;

    /// <summary>
    /// Gets the localized display label for the annotation kind.
    /// </summary>
    public string KindLabel
    {
        get; private set;
    } = string.Empty;

    /// <summary>
    /// Updates the kind label to reflect the current language.
    /// </summary>
    /// <param name="localizationService">The localization service.</param>
    public void UpdateLocalization(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        KindLabel = localizationService.GetString(GetKindKey(Annotation.Kind));
    }

    private static string GetKindKey(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => "annotation.kind.highlight",
            DocumentAnnotationKind.Ink => "annotation.kind.ink",
            DocumentAnnotationKind.Rectangle => "annotation.kind.rectangle",
            DocumentAnnotationKind.Text => "annotation.kind.text",
            DocumentAnnotationKind.Note => "annotation.kind.note",
            DocumentAnnotationKind.Stamp => "annotation.kind.stamp",
            DocumentAnnotationKind.Signature => "annotation.kind.signature",
            _ => "annotation.kind.note"
        };
    }
}
