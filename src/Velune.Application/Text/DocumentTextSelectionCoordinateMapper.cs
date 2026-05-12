using Velune.Application.DTOs;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Text;

/// <summary>Converts visual layer coordinates to document source coordinates accounting for rotation.</summary>
public static class DocumentTextSelectionCoordinateMapper
{
    /// <summary>Maps visual coordinates to document source coordinates.</summary>
    /// <param name="visualX">The X coordinate in the visual layer.</param>
    /// <param name="visualY">The Y coordinate in the visual layer.</param>
    /// <param name="layerWidth">The width of the visual layer.</param>
    /// <param name="layerHeight">The height of the visual layer.</param>
    /// <param name="sourceWidth">The width of the document source.</param>
    /// <param name="sourceHeight">The height of the document source.</param>
    /// <param name="rotation">The current document rotation.</param>
    /// <param name="point">The mapped document-space point when successful.</param>
    /// <returns><c>true</c> if mapping succeeded; <c>false</c> if dimensions are invalid.</returns>
    public static bool TryMapVisualToDocument(
        double visualX,
        double visualY,
        double layerWidth,
        double layerHeight,
        double sourceWidth,
        double sourceHeight,
        Rotation rotation,
        out DocumentTextSelectionPoint point)
    {
        point = new DocumentTextSelectionPoint(0, 0);

        if (layerWidth <= 0 ||
            layerHeight <= 0 ||
            sourceWidth <= 0 ||
            sourceHeight <= 0)
        {
            return false;
        }

        double rotatedWidth = rotation is Rotation.Deg90 or Rotation.Deg270
            ? sourceHeight
            : sourceWidth;
        double rotatedHeight = rotation is Rotation.Deg90 or Rotation.Deg270
            ? sourceWidth
            : sourceHeight;

        double rotatedX = Math.Clamp(visualX / layerWidth * rotatedWidth, 0, rotatedWidth);
        double rotatedY = Math.Clamp(visualY / layerHeight * rotatedHeight, 0, rotatedHeight);

        (double sourceX, double sourceY) = rotation switch
        {
            Rotation.Deg90 => (
                rotatedY,
                sourceHeight - rotatedX),
            Rotation.Deg180 => (
                sourceWidth - rotatedX,
                sourceHeight - rotatedY),
            Rotation.Deg270 => (
                sourceWidth - rotatedY,
                rotatedX),
            _ => (
                rotatedX,
                rotatedY)
        };

        point = new DocumentTextSelectionPoint(
            Math.Clamp(sourceX, 0, sourceWidth),
            Math.Clamp(sourceY, 0, sourceHeight));
        return true;
    }
}
