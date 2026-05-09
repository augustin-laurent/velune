using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Velune.Application.Abstractions;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Annotations;

/// <summary>
/// Stores document annotations as a JSON attachment embedded within the PDF file.
/// </summary>
public sealed class PdfAttachmentAnnotationStore : IPdfAnnotationStore
{
    private const string AttachmentName = "velune-annotations.json";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly PdfiumInitializer _pdfiumInitializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAttachmentAnnotationStore"/> class.
    /// </summary>
    /// <param name="pdfiumInitializer">Ensures PDFium is initialized before use.</param>
    public PdfAttachmentAnnotationStore(PdfiumInitializer pdfiumInitializer)
    {
        ArgumentNullException.ThrowIfNull(pdfiumInitializer);
        _pdfiumInitializer = pdfiumInitializer;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentAnnotation>> LoadAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfFilePath);

        if (!File.Exists(pdfFilePath))
        {
            return Task.FromResult<IReadOnlyList<DocumentAnnotation>>([]);
        }

        return Task.Run<IReadOnlyList<DocumentAnnotation>>(() => LoadCore(pdfFilePath), cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(
        string pdfFilePath,
        IReadOnlyList<DocumentAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfFilePath);
        ArgumentNullException.ThrowIfNull(annotations);

        if (annotations.Count == 0)
        {
            return RemoveAsync(pdfFilePath, cancellationToken);
        }

        return Task.Run(() => SaveCore(pdfFilePath, annotations), cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string pdfFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfFilePath);

        if (!File.Exists(pdfFilePath))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => RemoveCore(pdfFilePath), cancellationToken);
    }

    private DocumentAnnotation[] LoadCore(string pdfFilePath)
    {
        _pdfiumInitializer.EnsureInitialized();

        var handle = PdfiumNative.FPDF_LoadDocument(pdfFilePath, null);
        if (handle == nint.Zero)
        {
            return [];
        }

        try
        {
            var json = ReadAttachment(handle);
            if (json is null)
            {
                return [];
            }

            var dtos = JsonSerializer.Deserialize<AnnotationDto[]>(json, SerializerOptions);
            if (dtos is null || dtos.Length == 0)
            {
                return [];
            }

            return dtos
                .Select(DeserializeAnnotation)
                .Where(a => a is not null)
                .Cast<DocumentAnnotation>()
                .ToArray();
        }
        finally
        {
            PdfiumNative.FPDF_CloseDocument(handle);
        }
    }

    private void SaveCore(string pdfFilePath, IReadOnlyList<DocumentAnnotation> annotations)
    {
        _pdfiumInitializer.EnsureInitialized();

        var handle = PdfiumNative.FPDF_LoadDocument(pdfFilePath, null);
        if (handle == nint.Zero)
        {
            return;
        }

        try
        {
            RemoveExistingAttachment(handle);

            var dtos = annotations.Select(SerializeAnnotation).ToArray();
            var json = JsonSerializer.SerializeToUtf8Bytes(dtos, SerializerOptions);

            var attachment = PdfiumNative.FPDFDoc_AddAttachment(handle, AttachmentName);
            if (attachment == nint.Zero)
            {
                return;
            }

            PdfiumNative.FPDFAttachment_SetFile(attachment, handle, json, (uint)json.Length);

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using var writer = new PdfiumFileWriter(stream);
                    PdfiumNative.FPDF_SaveAsCopy(handle, writer.Handle, 0);
                }

                PdfiumNative.FPDF_CloseDocument(handle);
                handle = nint.Zero;

                File.Copy(tempFile, pdfFilePath, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }
        finally
        {
            if (handle != nint.Zero)
            {
                PdfiumNative.FPDF_CloseDocument(handle);
            }
        }
    }

    private void RemoveCore(string pdfFilePath)
    {
        _pdfiumInitializer.EnsureInitialized();

        var handle = PdfiumNative.FPDF_LoadDocument(pdfFilePath, null);
        if (handle == nint.Zero)
        {
            return;
        }

        try
        {
            if (!RemoveExistingAttachment(handle))
            {
                return;
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using var writer = new PdfiumFileWriter(stream);
                    PdfiumNative.FPDF_SaveAsCopy(handle, writer.Handle, 0);
                }

                PdfiumNative.FPDF_CloseDocument(handle);
                handle = nint.Zero;

                File.Copy(tempFile, pdfFilePath, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }
        finally
        {
            if (handle != nint.Zero)
            {
                PdfiumNative.FPDF_CloseDocument(handle);
            }
        }
    }

    private static string? ReadAttachment(nint document)
    {
        var count = PdfiumNative.FPDFDoc_GetAttachmentCount(document);
        for (var i = 0; i < count; i++)
        {
            var attachment = PdfiumNative.FPDFDoc_GetAttachment(document, i);
            if (attachment == nint.Zero)
            {
                continue;
            }

            var name = GetAttachmentName(attachment);
            if (!string.Equals(name, AttachmentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!PdfiumNative.FPDFAttachment_GetFile(attachment, null, 0, out var fileLen) || fileLen == 0)
            {
                return null;
            }

            var buffer = new byte[fileLen];
            if (!PdfiumNative.FPDFAttachment_GetFile(attachment, buffer, fileLen, out _))
            {
                return null;
            }

            return Encoding.UTF8.GetString(buffer, 0, (int)fileLen);
        }

        return null;
    }

    private static bool RemoveExistingAttachment(nint document)
    {
        var count = PdfiumNative.FPDFDoc_GetAttachmentCount(document);
        for (var i = 0; i < count; i++)
        {
            var attachment = PdfiumNative.FPDFDoc_GetAttachment(document, i);
            if (attachment == nint.Zero)
            {
                continue;
            }

            var name = GetAttachmentName(attachment);
            if (string.Equals(name, AttachmentName, StringComparison.OrdinalIgnoreCase))
            {
                PdfiumNative.FPDFDoc_DeleteAttachment(document, i);
                return true;
            }
        }

        return false;
    }

    private static string? GetAttachmentName(nint attachment)
    {
        var nameLen = PdfiumNative.FPDFAttachment_GetName(attachment, null, 0);
        if (nameLen <= 2)
        {
            return null;
        }

        var nameBuffer = new byte[nameLen];
        PdfiumNative.FPDFAttachment_GetName(attachment, nameBuffer, nameLen);
        return Encoding.Unicode.GetString(nameBuffer, 0, (int)nameLen - 2);
    }

    private static AnnotationDto SerializeAnnotation(DocumentAnnotation annotation)
    {
        return new AnnotationDto
        {
            Id = annotation.Id,
            Kind = annotation.Kind,
            PageIndex = annotation.PageIndex.Value,
            StrokeHex = annotation.Appearance.StrokeHex,
            FillHex = annotation.Appearance.FillHex,
            StrokeThickness = annotation.Appearance.StrokeThickness,
            Opacity = annotation.Appearance.Opacity,
            FontSize = annotation.Appearance.FontSize,
            FontFamily = annotation.Appearance.FontFamily,
            BoundsX = annotation.Bounds?.X,
            BoundsY = annotation.Bounds?.Y,
            BoundsW = annotation.Bounds?.Width,
            BoundsH = annotation.Bounds?.Height,
            Points = annotation.Points.Count > 0
                ? annotation.Points.Select(p => new PointDto { X = p.X, Y = p.Y }).ToArray()
                : null,
            Text = annotation.Text,
            AssetId = annotation.AssetId,
            CreatedAt = annotation.CreatedAt
        };
    }

    private static DocumentAnnotation? DeserializeAnnotation(AnnotationDto dto)
    {
        try
        {
            NormalizedTextRegion? bounds = null;
            if (dto.BoundsX.HasValue && dto.BoundsY.HasValue &&
                dto.BoundsW.HasValue && dto.BoundsH.HasValue)
            {
                bounds = new NormalizedTextRegion(
                    dto.BoundsX.Value,
                    dto.BoundsY.Value,
                    dto.BoundsW.Value,
                    dto.BoundsH.Value);
            }

            var points = dto.Points?.Select(p => new NormalizedPoint(p.X, p.Y)).ToArray();
            var appearance = new AnnotationAppearance(
                dto.StrokeHex ?? "#FFE600",
                dto.FillHex,
                dto.StrokeThickness > 0 ? dto.StrokeThickness : 2,
                dto.Opacity is > 0 and <= 1 ? dto.Opacity : 1.0,
                dto.FontSize is >= 6 and <= 200 ? dto.FontSize : 14,
                dto.FontFamily);

            return new DocumentAnnotation(
                dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                dto.Kind,
                new PageIndex(dto.PageIndex),
                appearance,
                bounds,
                points,
                dto.Text,
                dto.AssetId,
                dto.CreatedAt);
        }
        catch
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class AnnotationDto
    {
        public Guid Id
        {
            get; set;
        }

        public DocumentAnnotationKind Kind
        {
            get; set;
        }

        public int PageIndex
        {
            get; set;
        }

        public string? StrokeHex
        {
            get; set;
        }

        public string? FillHex
        {
            get; set;
        }

        public double StrokeThickness
        {
            get; set;
        }

        public double Opacity
        {
            get; set;
        }

        public double FontSize
        {
            get; set;
        }

        public string? FontFamily
        {
            get; set;
        }

        public double? BoundsX
        {
            get; set;
        }

        public double? BoundsY
        {
            get; set;
        }

        public double? BoundsW
        {
            get; set;
        }

        public double? BoundsH
        {
            get; set;
        }

        public PointDto[]? Points
        {
            get; set;
        }

        public string? Text
        {
            get; set;
        }

        public string? AssetId
        {
            get; set;
        }

        public DateTimeOffset? CreatedAt
        {
            get; set;
        }
    }

    private sealed class PointDto
    {
        public double X
        {
            get; set;
        }

        public double Y
        {
            get; set;
        }
    }

    private sealed class PdfiumFileWriter : IDisposable
    {
        private static readonly Dictionary<nint, PdfiumFileWriter> Writers = [];
        private static readonly object WritersGate = new();
        private static readonly PdfiumWriteBlockDelegate WriteBlockCallbackInstance = WriteBlock;

        private readonly Stream _stream;
        private readonly nint _nativeHandle;
        private readonly System.Runtime.InteropServices.GCHandle _callbackHandle;
        private bool _disposed;

        public PdfiumFileWriter(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _callbackHandle = System.Runtime.InteropServices.GCHandle.Alloc(WriteBlockCallbackInstance);
            var nativeStruct = new PdfiumNative.FpdfFileWrite
            {
                Version = 1,
                WriteBlock = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(WriteBlockCallbackInstance)
            };

            _nativeHandle = System.Runtime.InteropServices.Marshal.AllocHGlobal(
                System.Runtime.InteropServices.Marshal.SizeOf<PdfiumNative.FpdfFileWrite>());
            System.Runtime.InteropServices.Marshal.StructureToPtr(nativeStruct, _nativeHandle, fDeleteOld: false);

            lock (WritersGate)
            {
                Writers[_nativeHandle] = this;
            }
        }

        public nint Handle => _nativeHandle;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (WritersGate)
            {
                Writers.Remove(_nativeHandle);
            }

            if (_nativeHandle != nint.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(_nativeHandle);
            }

            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }

            _stream.Dispose();
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int PdfiumWriteBlockDelegate(nint self, nint data, nuint size);

        private static int WriteBlock(nint self, nint data, nuint size)
        {
            PdfiumFileWriter? writer;
            lock (WritersGate)
            {
                Writers.TryGetValue(self, out writer);
            }

            if (writer is null)
            {
                return 0;
            }

            try
            {
                checked
                {
                    var length = (int)size;
                    var buffer = new byte[length];
                    System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, length);
                    writer._stream.Write(buffer, 0, length);
                    return 1;
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
