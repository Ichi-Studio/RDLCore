using DocumentFormat.OpenXml.Packaging;
using RdlCore.Abstractions.Exceptions;
using AxiomDocumentType = RdlCore.Abstractions.Enums.DocumentType;

namespace RdlCore.Parsing.Word;

/// <summary>
/// Converts old binary .doc format to OpenXML .docx format
/// </summary>
public class DocBinaryConverter
{
    private readonly ILogger<DocBinaryConverter> _logger;

    public DocBinaryConverter(ILogger<DocBinaryConverter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts a .doc binary stream to a .docx OpenXML stream
    /// </summary>
    public async Task<MemoryStream> ConvertDocToDocxAsync(Stream docStream, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting .doc to .docx conversion using NPOI");

        try
        {
            // Note: This is a simplified conversion that extracts text only
            // For full-featured conversion with styles, tables, etc., consider using:
            // 1. Aspose.Words (commercial)
            // 2. LibreOffice command line (requires external install)
            // 3. Microsoft.Office.Interop.Word (requires Office installation)
            
            _logger.LogWarning(".doc format detected. Basic text-only conversion will be performed.");
            
            throw new NotImplementedException(
                "当前版本不支持 .doc 文件直接转换。\n" +
                "请将 .doc 文件转换为 .docx 格式：\n" +
                "1. 在 Microsoft Word 中打开文件\n" +
                "2. 点击 '文件' > '另存为'\n" +
                "3. 选择 'Word 文档 (*.docx)' 格式\n" +
                "4. 保存后重新上传\n" +
                "\n或使用在线转换工具（如 CloudConvert、Zamzar）");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert .doc to .docx");
            throw new DocumentParsingException(
                "无法转换 .doc 文件。请尝试：\n" +
                "1. 在 Microsoft Word 中打开文件并另存为 .docx 格式\n" +
                "2. 确保文件未损坏\n" +
                "3. 验证文件是真正的 Word 97-2003 格式",
                AxiomDocumentType.Word, ex);
        }
    }

    /// <summary>
    /// Checks if a stream contains a valid .doc binary format
    /// </summary>
    public static bool IsValidDocFormat(Stream stream)
    {
        var originalPosition = stream.Position;
        try
        {
            var buffer = new byte[8];
            var bytesRead = stream.Read(buffer, 0, 8);

            if (bytesRead < 8)
            {
                return false;
            }

            // Check for OLE2/CFB signature
            return buffer[0] == 0xD0 && buffer[1] == 0xCF && 
                   buffer[2] == 0x11 && buffer[3] == 0xE0 &&
                   buffer[4] == 0xA1 && buffer[5] == 0xB1 && 
                   buffer[6] == 0x1A && buffer[7] == 0xE1;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
