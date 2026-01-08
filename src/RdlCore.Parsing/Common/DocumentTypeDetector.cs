using RdlCore.Abstractions.Exceptions;

namespace RdlCore.Parsing.Common;

/// <summary>
/// Detects the type of document based on content analysis
/// </summary>
public class DocumentTypeDetector
{
    private readonly ILogger<DocumentTypeDetector> _logger;

    // Magic bytes for file type detection
    private static readonly byte[] DocxMagicBytes = { 0x50, 0x4B, 0x03, 0x04 }; // PK (ZIP format)
    private static readonly byte[] DocMagicBytes = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2/CFB (old .doc format)
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46 };  // %PDF
    private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG signature
    private static readonly byte[] JpegMagicBytes = { 0xFF, 0xD8, 0xFF }; // JPEG/JPG
    private static readonly byte[] BmpMagicBytes = { 0x42, 0x4D }; // BM
    private static readonly byte[] TiffLittleEndianMagicBytes = { 0x49, 0x49, 0x2A, 0x00 }; // II (little-endian)
    private static readonly byte[] TiffBigEndianMagicBytes = { 0x4D, 0x4D, 0x00, 0x2A }; // MM (big-endian)

    /// <summary>
    /// Creates a new DocumentTypeDetector
    /// </summary>
    public DocumentTypeDetector(ILogger<DocumentTypeDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detects the document type from a stream
    /// </summary>
    public async Task<DocumentType> DetectTypeAsync(Stream stream, CancellationToken ct = default)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        var originalPosition = stream.Position;
        
        try
        {
            var buffer = new byte[8];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 8), ct);

            if (bytesRead < 4)
            {
                _logger.LogWarning("Stream too short to detect document type");
                return DocumentType.Unknown;
            }

            if (StartsWith(buffer, PdfMagicBytes))
            {
                _logger.LogDebug("Detected PDF document");
                return DocumentType.Pdf;
            }

            // Check for old .doc format (OLE2/CFB)
            if (StartsWith(buffer, DocMagicBytes))
            {
                _logger.LogDebug("Detected Word document (.doc binary format)");
                return DocumentType.Word;
            }

            // Check for image formats
            if (StartsWith(buffer, PngMagicBytes))
            {
                _logger.LogDebug("Detected PNG image - OCR required");
                return DocumentType.Image;
            }

            if (StartsWith(buffer, JpegMagicBytes))
            {
                _logger.LogDebug("Detected JPEG image - OCR required");
                return DocumentType.Image;
            }

            if (StartsWith(buffer, BmpMagicBytes))
            {
                _logger.LogDebug("Detected BMP image - OCR required");
                return DocumentType.Image;
            }

            if (StartsWith(buffer, TiffLittleEndianMagicBytes) || StartsWith(buffer, TiffBigEndianMagicBytes))
            {
                _logger.LogDebug("Detected TIFF image - OCR required");
                return DocumentType.Image;
            }

            if (StartsWith(buffer, DocxMagicBytes))
            {
                // Further check for Word document (DOCX is a ZIP with specific content)
                stream.Position = originalPosition;
                if (await IsWordDocumentAsync(stream, ct))
                {
                    _logger.LogDebug("Detected Word document");
                    return DocumentType.Word;
                }
            }

            _logger.LogWarning("Unable to detect document type");
            return DocumentType.Unknown;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Detects document type from file extension
    /// </summary>
    public DocumentType DetectFromExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        
        return extension switch
        {
            ".docx" => DocumentType.Word,
            ".doc" => DocumentType.Word,
            ".pdf" => DocumentType.Pdf,
            ".rtf" => DocumentType.Rtf,
            ".png" => DocumentType.Image,
            ".jpg" => DocumentType.Image,
            ".jpeg" => DocumentType.Image,
            ".bmp" => DocumentType.Image,
            ".tiff" => DocumentType.Image,
            ".tif" => DocumentType.Image,
            _ => DocumentType.Unknown
        };
    }

    private static bool StartsWith(byte[] buffer, byte[] magic)
    {
        if (buffer.Length < magic.Length) return false;
        
        for (int i = 0; i < magic.Length; i++)
        {
            if (buffer[i] != magic[i]) return false;
        }
        
        return true;
    }

    private async Task<bool> IsWordDocumentAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var archive = new System.IO.Compression.ZipArchive(stream, 
                System.IO.Compression.ZipArchiveMode.Read, 
                leaveOpen: true);
            
            // Word documents contain [Content_Types].xml and word/document.xml
            var hasContentTypes = archive.GetEntry("[Content_Types].xml") != null;
            var hasDocument = archive.GetEntry("word/document.xml") != null;
            
            return hasContentTypes && hasDocument;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Calculates bounding boxes for document elements
/// </summary>
public class BoundingBoxCalculator
{
    /// <summary>
    /// Converts EMU (English Metric Units) to points
    /// 1 inch = 914400 EMU, 1 inch = 72 points
    /// </summary>
    public static double EmuToPoints(long emu)
    {
        return emu / 914400.0 * 72.0;
    }

    /// <summary>
    /// Converts twips to points (20 twips = 1 point)
    /// </summary>
    public static double TwipsToPoints(int twips)
    {
        return twips / 20.0;
    }

    /// <summary>
    /// Converts points to inches
    /// </summary>
    public static double PointsToInches(double points)
    {
        return points / 72.0;
    }

    /// <summary>
    /// Converts inches to points
    /// </summary>
    public static double InchesToPoints(double inches)
    {
        return inches * 72.0;
    }

    /// <summary>
    /// Calculates the union of multiple bounding boxes
    /// </summary>
    public static BoundingBox Union(IEnumerable<BoundingBox> boxes)
    {
        var boxList = boxes.ToList();
        if (boxList.Count == 0)
        {
            return new BoundingBox(0, 0, 0, 0);
        }

        var minLeft = boxList.Min(b => b.Left);
        var minTop = boxList.Min(b => b.Top);
        var maxRight = boxList.Max(b => b.Right);
        var maxBottom = boxList.Max(b => b.Bottom);

        return new BoundingBox(
            minLeft,
            minTop,
            maxRight - minLeft,
            maxBottom - minTop);
    }

    /// <summary>
    /// Checks if two bounding boxes intersect
    /// </summary>
    public static bool Intersects(BoundingBox a, BoundingBox b)
    {
        return a.Left < b.Right &&
               a.Right > b.Left &&
               a.Top < b.Bottom &&
               a.Bottom > b.Top;
    }
}
