using Velune.Domain.Annotations;
using Velune.Presentation.Localization;

namespace Velune.Presentation.ViewModels;

public sealed class AnnotationListItemViewModel
{
    public AnnotationListItemViewModel(DocumentAnnotation annotation, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(localizationService);

        Annotation = annotation;
        UpdateLocalization(localizationService);
    }

    public DocumentAnnotation Annotation
    {
        get;
    }

    public Guid Id => Annotation.Id;

    public DocumentAnnotationKind Kind => Annotation.Kind;

    public string? Text => Annotation.Text;

    public string? AssetId => Annotation.AssetId;

    public string KindLabel
    {
        get; private set;
    } = string.Empty;

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
