using Velune.Application.DTOs;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Text;

public static class DocumentTextSelectionCoordinateMapper
{
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

        var rotatedWidth = rotation is Rotation.Deg90 or Rotation.Deg270
            ? sourceHeight
            : sourceWidth;
        var rotatedHeight = rotation is Rotation.Deg90 or Rotation.Deg270
            ? sourceWidth
            : sourceHeight;

        var rotatedX = Math.Clamp(visualX / layerWidth * rotatedWidth, 0, rotatedWidth);
        var rotatedY = Math.Clamp(visualY / layerHeight * rotatedHeight, 0, rotatedHeight);

        var (sourceX, sourceY) = rotation switch
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
