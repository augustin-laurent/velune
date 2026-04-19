using Velune.Application.Text;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.Text;

public sealed class DocumentTextSelectionCoordinateMapperTests
{
    [Fact]
    public void TryMapVisualToDocument_ShouldScalePointsInAutoFitMode()
    {
        var mapped = DocumentTextSelectionCoordinateMapper.TryMapVisualToDocument(
            visualX: 300,
            visualY: 210,
            layerWidth: 600,
            layerHeight: 420,
            sourceWidth: 1000,
            sourceHeight: 1400,
            Rotation.Deg0,
            out var point);

        Assert.True(mapped);
        Assert.Equal(500, point.X, precision: 6);
        Assert.Equal(700, point.Y, precision: 6);
    }

    [Fact]
    public void TryMapVisualToDocument_ShouldPreservePointsInScrollableMode()
    {
        var mapped = DocumentTextSelectionCoordinateMapper.TryMapVisualToDocument(
            visualX: 250,
            visualY: 640,
            layerWidth: 1000,
            layerHeight: 1400,
            sourceWidth: 1000,
            sourceHeight: 1400,
            Rotation.Deg0,
            out var point);

        Assert.True(mapped);
        Assert.Equal(250, point.X, precision: 6);
        Assert.Equal(640, point.Y, precision: 6);
    }

    [Fact]
    public void TryMapVisualToDocument_ShouldInvertRotationForQuarterTurn()
    {
        var mapped = DocumentTextSelectionCoordinateMapper.TryMapVisualToDocument(
            visualX: 1400,
            visualY: 0,
            layerWidth: 1400,
            layerHeight: 1000,
            sourceWidth: 1000,
            sourceHeight: 1400,
            Rotation.Deg90,
            out var point);

        Assert.True(mapped);
        Assert.Equal(0, point.X, precision: 6);
        Assert.Equal(0, point.Y, precision: 6);
    }
}
