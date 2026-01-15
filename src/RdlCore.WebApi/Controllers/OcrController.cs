namespace RdlCore.WebApi.Controllers;

/// <summary>
/// 图片 OCR 识别 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OcrController(OcrEngine ocrEngine, ILogger<OcrController> logger) : ControllerBase
{
    private readonly OcrEngine _ocrEngine = ocrEngine;
    private readonly ILogger<OcrController> _logger = logger;

    /// <summary>
    /// 对上传的图片进行 OCR 文字识别
    /// </summary>
    /// <param name="file">图片文件 (.png, .jpg, .jpeg, .bmp, .tiff)</param>
    /// <returns>识别结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB limit
    public async Task<ActionResult<OcrResponse>> RecognizeImage(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new OcrResponse
            {
                Success = false,
                Message = "请上传一个有效的图片文件"
            });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif" };
        if (!validExtensions.Contains(extension))
        {
            return BadRequest(new OcrResponse
            {
                Success = false,
                Message = $"不支持的图片格式: {extension}，支持: {string.Join(", ", validExtensions)}"
            });
        }

        try
        {
            _logger.LogInformation("开始 OCR 识别: {FileName}, 大小: {Size} bytes", file.FileName, file.Length);

            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var imageData = memoryStream.ToArray();

            var result = await _ocrEngine.RecognizeAsync(imageData, cancellationToken);

            return Ok(new OcrResponse
            {
                Success = true,
                Message = "识别完成",
                Text = result.Text,
                Confidence = result.Confidence,
                TextBlocks = result.Words.Select(w => new TextBlockInfo
                {
                    Text = w.Text,
                    Confidence = w.Confidence,
                    BoundingBox = new BoundingBoxInfo
                    {
                        X = w.Bounds.Left,
                        Y = w.Bounds.Top,
                        Width = w.Bounds.Width,
                        Height = w.Bounds.Height
                    }
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR 识别失败: {FileName}", file.FileName);
            return StatusCode(500, new OcrResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }
}

/// <summary>
/// OCR 识别响应
/// </summary>
public class OcrResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Text { get; set; }
    public double? Confidence { get; set; }
    public List<TextBlockInfo>? TextBlocks { get; set; }
}

/// <summary>
/// 文本块信息
/// </summary>
public class TextBlockInfo
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBoxInfo? BoundingBox { get; set; }
}
