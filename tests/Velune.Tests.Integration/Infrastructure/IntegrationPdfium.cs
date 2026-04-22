using Velune.Infrastructure.Pdf;

namespace Velune.Tests.Integration.Infrastructure;

internal static class IntegrationPdfium
{
    public static PdfiumInitializer Initializer { get; } = new();
}
