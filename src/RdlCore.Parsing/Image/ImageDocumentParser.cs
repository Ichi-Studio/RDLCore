using RdlCore.Abstractions.Exceptions;
using RdlCore.Abstractions.Models;
using RdlCore.Parsing.Ocr;
using OpenCvSharp;
using AxiomDocumentType = RdlCore.Abstractions.Enums.DocumentType;

namespace RdlCore.Parsing.Image;

public sealed class ImageDocumentParser : IConfigurableDocumentParser
{
    private readonly ILogger<ImageDocumentParser> _logger;
    private readonly IOcrService _ocrService;

    public AxiomDocumentType SupportedType => AxiomDocumentType.Image;

    public ImageDocumentParser(ILogger<ImageDocumentParser> logger, IOcrService ocrService)
    {
        _logger = logger;
        _ocrService = ocrService;
    }

    public Task<DocumentStructureModel> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var defaultOptions = new ConversionOptions(
            DataSetName: null,
            SchemaPath: null,
            StyleTemplate: null,
            ForceOverwrite: false,
            VerboseOutput: false,
            DryRun: false);

        return ParseAsync(stream, defaultOptions, cancellationToken);
    }

    public async Task<DocumentStructureModel> ParseAsync(
        Stream stream,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        stream.Position = 0;
        var imageBytes = await ReadAllBytesAsync(stream, cancellationToken);

        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat.Empty())
        {
            throw new DocumentParsingException("Failed to decode image.", SupportedType);
        }

        var (dpi, pageWidthPoints, pageHeightPoints) = InferPageSize(mat.Width, mat.Height);
        var mimeType = DetectMimeType(imageBytes);

        var elements = new List<ContentElement>();
        var elementId = 0;

        elements.Add(new ImageElement(
            Id: "img_1",
            Bounds: new BoundingBox(0, 0, pageWidthPoints, pageHeightPoints),
            ZIndex: elementId++,
            ImageData: imageBytes,
            MimeType: mimeType,
            AlternateText: "Source Image"));

        if (options.OcrEnabled)
        {
            var ocrRegions = await _ocrService.RecognizeAsync(
                imageBytes,
                options.OcrLanguage,
                options.OcrConfidenceThreshold,
                cancellationToken);

            foreach (var region in ocrRegions
                         .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                         .OrderBy(r => r.BoundsPixels.Top)
                         .ThenBy(r => r.BoundsPixels.Left))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bounds = PixelsToPoints(region.BoundsPixels, dpi);
                var fontSize = Math.Max(6.0, bounds.Height * 0.75);

                var run = new TextRun(
                    Text: region.Text,
                    Style: new TextStyle(
                        FontFamily: "Microsoft YaHei",
                        FontSize: fontSize,
                        IsBold: false,
                        IsItalic: false,
                        IsUnderline: false,
                        Color: null,
                        BackgroundColor: null,
                        HorizontalAlignment: null,
                        VerticalAlignment: null),
                    FieldCode: null);

                elements.Add(new ParagraphElement(
                    Id: $"ocr_{++elementId}",
                    Bounds: bounds,
                    ZIndex: elementId,
                    Runs: new List<TextRun> { run }.AsReadOnly(),
                    Style: new ParagraphStyle(
                        LineSpacing: null,
                        SpaceBefore: null,
                        SpaceAfter: null,
                        FirstLineIndent: null,
                        Alignment: null)));
            }

            _logger.LogInformation("OCR completed: Regions={Count}, Dpi={Dpi}", ocrRegions.Count, dpi);
        }

        var page = new PageElement(
            PageNumber: 1,
            Width: pageWidthPoints,
            Height: pageHeightPoints,
            Elements: elements.AsReadOnly());

        return new DocumentStructureModel(
            Type: SupportedType,
            Pages: new List<PageElement> { page }.AsReadOnly(),
            LogicalElements: Array.Empty<LogicalElement>(),
            Metadata: new DocumentMetadata(
                Title: null,
                Author: null,
                Subject: null,
                CreatedDate: null,
                ModifiedDate: null,
                PageCount: 1,
                FileName: null));
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static (int dpi, double widthPoints, double heightPoints) InferPageSize(int widthPixels, int heightPixels)
    {
        var dpi = Math.Max(widthPixels, heightPixels) >= 2000 ? 300 : 96;
        var widthPoints = widthPixels * 72.0 / dpi;
        var heightPoints = heightPixels * 72.0 / dpi;
        return (dpi, widthPoints, heightPoints);
    }

    private static BoundingBox PixelsToPoints(BoundingBox boundsPixels, int dpi)
    {
        var scale = 72.0 / dpi;
        return new BoundingBox(
            Left: boundsPixels.Left * scale,
            Top: boundsPixels.Top * scale,
            Width: boundsPixels.Width * scale,
            Height: boundsPixels.Height * scale);
    }

    private static string DetectMimeType(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return "image/bmp";
        }

        if (bytes.Length >= 4 &&
            ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) ||
             (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A)))
        {
            return "image/tiff";
        }

        return "application/octet-stream";
    }
}
