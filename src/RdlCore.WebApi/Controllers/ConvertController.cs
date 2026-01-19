namespace RdlCore.WebApi.Controllers;

/// <summary>
/// 文档转换 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConvertController(
    IConversionPipelineService pipelineService,
    IDocumentPerceptionService perceptionService,
    ILogicDecompositionService decompositionService,
    ILogger<ConvertController> logger) : ControllerBase
{

    /// <summary>
    /// 将 Word/PDF 文档转换为 RDLC 报表定义
    /// </summary>
    /// <param name="file">要转换的文档文件 (.docx 或 .pdf)</param>
    /// <param name="request">转换选项</param>
    /// <returns>转换结果，包含生成的 RDLC 内容</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ConvertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConvertResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ConvertResponse), StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
    public async Task<ActionResult<ConvertResponse>> ConvertDocument(
        IFormFile file,
        [FromQuery] ConvertRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        request ??= new ConvertRequest();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new ConvertResponse
            {
                Success = false,
                Message = "请上传一个有效的文件",
                Errors = ["未提供文件或文件为空"]
            });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".docx" && extension != ".pdf")
        {
            return BadRequest(new ConvertResponse
            {
                Success = false,
                Message = $"不支持的文件格式: {extension}",
                Errors = ["仅支持 .docx 和 .pdf 格式"]
            });
        }

        try
        {
            logger.LogInformation("开始转换文档: {FileName}, 大小: {Size} bytes", 
                file.FileName, file.Length);

            await using var stream = file.OpenReadStream();
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var conversionRequest = new ConversionRequest(
                DocumentStream: memoryStream,
                DocumentType: extension == ".docx" ? RdlCore.Abstractions.Enums.DocumentType.Word : RdlCore.Abstractions.Enums.DocumentType.Pdf,
                OutputPath: null,
                Options: new ConversionOptions(
                    DataSetName: request.DataSetName,
                    SchemaPath: null,
                    StyleTemplate: request.StyleTemplate,
                    ForceOverwrite: true,
                    VerboseOutput: request.Verbose,
                    DryRun: false));

            var result = await pipelineService.ExecuteAsync(conversionRequest, null, cancellationToken);

            var elapsed = DateTime.UtcNow - startTime;
            var isSuccess = result.Status == ConversionStatus.Completed || result.Status == ConversionStatus.CompletedWithWarnings;

            if (isSuccess && result.RdlDocument != null)
            {
                var rdlcBytes = Encoding.UTF8.GetBytes(result.RdlDocument.ToString());
                var rdlcBase64 = System.Convert.ToBase64String(rdlcBytes);

                var errors = result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message).ToList();
                var warnings = result.Messages.Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.Message).ToList();

                return Ok(new ConvertResponse
                {
                    Success = true,
                    Message = "转换成功",
                    ElapsedMilliseconds = elapsed.TotalMilliseconds,
                    DocumentInfo = new DocumentInfo
                    {
                        FileName = file.FileName,
                        FileSizeBytes = file.Length,
                        DocumentType = extension == ".docx" ? "Word (OpenXML)" : "PDF",
                        PageCount = 1
                    },
                    Statistics = new ConversionStatistics
                    {
                        TextboxCount = result.Summary.TextboxCount,
                        TableCount = result.Summary.TablixCount,
                        ImageCount = result.Summary.ImageCount,
                        ExpressionCount = result.Summary.ExpressionCount,
                        FieldCodeCount = 0
                    },
                    RdlcContentBase64 = rdlcBase64,
                    Warnings = warnings,
                    Errors = errors
                });
            }
            else
            {
                var errors = result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message).ToList();
                var warnings = result.Messages.Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.Message).ToList();

                return Ok(new ConvertResponse
                {
                    Success = false,
                    Message = "转换失败",
                    ElapsedMilliseconds = elapsed.TotalMilliseconds,
                    Errors = errors,
                    Warnings = warnings
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "转换文档时发生错误: {FileName}", file.FileName);
            return StatusCode(500, new ConvertResponse
            {
                Success = false,
                Message = "服务器内部错误",
                Errors = [ex.Message]
            });
        }
    }

    /// <summary>
    /// 分析文档结构（不生成 RDLC）
    /// </summary>
    /// <param name="file">要分析的文档文件</param>
    /// <returns>文档分析结果</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AnalyzeResponse), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new AnalyzeResponse
            {
                Success = false,
                Message = "请上传一个有效的文件"
            });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".docx" && extension != ".pdf")
        {
            return BadRequest(new AnalyzeResponse
            {
                Success = false,
                Message = $"不支持的文件格式: {extension}"
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var docType = extension == ".docx" ? RdlCore.Abstractions.Enums.DocumentType.Word : RdlCore.Abstractions.Enums.DocumentType.Pdf;
            var structure = await perceptionService.AnalyzeAsync(memoryStream, docType, cancellationToken);

            memoryStream.Position = 0;
            var logicResult = await decompositionService.ExtractFieldCodesAsync(structure, cancellationToken);

            var elements = new List<DetectedElement>();
            foreach (var page in structure.Pages)
            {
                foreach (var element in page.Elements)
                {
                    elements.Add(new DetectedElement
                    {
                        ElementType = element.GetType().Name,
                        PageNumber = page.PageNumber,
                        BoundingBox = element.Bounds != null ? new BoundingBoxInfo
                        {
                            X = element.Bounds.Left,
                            Y = element.Bounds.Top,
                            Width = element.Bounds.Width,
                            Height = element.Bounds.Height
                        } : null
                    });
                }
            }

            var fieldCodes = logicResult.FieldCodes.Select(fr => new DetectedFieldCode
            {
                FieldType = "MERGEFIELD",
                OriginalContent = fr.RawCode,
                TranslatedExpression = $"=Fields!{fr.FieldName}.Value"
            }).ToList();

            return Ok(new AnalyzeResponse
            {
                Success = true,
                Message = "分析完成",
                DocumentInfo = new DocumentInfo
                {
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    DocumentType = extension == ".docx" ? "Word (OpenXML)" : "PDF",
                    PageCount = structure.Pages.Count
                },
                Elements = elements,
                FieldCodes = fieldCodes
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "分析文档时发生错误: {FileName}", file.FileName);
            return StatusCode(500, new AnalyzeResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }


    /// <summary>
    /// 下载转换后的 RDLC 文件（多页文档将返回 ZIP 压缩包）
    /// </summary>
    /// <param name="file">要转换的文档文件</param>
    /// <param name="dataSetName">数据集名称</param>
    /// <returns>RDLC 文件或 ZIP 压缩包</returns>
    [HttpPost("download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConvertResponse), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> DownloadRdlc(IFormFile file,[FromQuery] string dataSetName = "DefaultDataSet",CancellationToken cancellationToken = default)
    {

        //初始校验
        
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ConvertResponse
            {
                Success = false,
                Message = "请上传一个有效的文件"
            });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension != ".docx" && extension != ".pdf")
        {
            return BadRequest(new ConvertResponse
            {
                Success = false,
                Message = $"不支持的文件格式: {extension}"
            });
        }



        try
        {


            await using var stream = file.OpenReadStream();

            var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream, cancellationToken);

            memoryStream.Position = 0;

            var conversionRequest = new ConversionRequest(
                DocumentStream: memoryStream,
                DocumentType: extension == ".docx" ? DocumentType.Word : DocumentType.Pdf,
                OutputPath: null,
                Options: new (
                    DataSetName: dataSetName,
                    SchemaPath: null,
                    StyleTemplate: null,
                    ForceOverwrite: true,
                    VerboseOutput: false,
                    DryRun: false));



            //管道验证五个步骤执行转换
            var result = await pipelineService.ExecuteAsync(conversionRequest, null, cancellationToken);


            var isSuccess = result.Status == ConversionStatus.Completed || result.Status == ConversionStatus.CompletedWithWarnings;

            if (isSuccess && result.RdlDocument != null)
            {

                var baseFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));

                var allDocuments = result.GetAllDocuments();

                // If multiple documents, return as ZIP
                if (allDocuments.Count > 1)
                {
                    logger.LogInformation("生成 {Count} 个 RDLC 文件，打包为 ZIP", allDocuments.Count);

                    using var zipStream = new MemoryStream();

                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                    {
                        foreach (var doc in allDocuments)
                        {
                            var entryName = $"{baseFileName}_{doc.PageNumber}.rdlc";
                            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                            
                            using var entryStream = entry.Open();
                            var docBytes = Encoding.UTF8.GetBytes(doc.Document.ToString());
                            await entryStream.WriteAsync(docBytes, cancellationToken);
                        }
                    }

                    zipStream.Position = 0;
                    return File(zipStream.ToArray(), "application/zip", $"{baseFileName}.zip");
                }
                else
                {
                    // Single document, return directly
                    var rdlcBytes = Encoding.UTF8.GetBytes(result.RdlDocument.ToString());
                    var outputFileName = baseFileName + ".rdlc";
                    
                    return File(rdlcBytes, "application/xml", outputFileName);
                }
            }
            else
            {
                var errors = result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message).ToList();
                return BadRequest(new ConvertResponse
                {
                    Success = false,
                    Message = "转换失败",
                    Errors = errors
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "下载 RDLC 时发生错误: {FileName}", file.FileName);
            return StatusCode(500, new ConvertResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Sanitizes file name for HTTP Content-Disposition header.
    /// Removes non-ASCII characters and control characters.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "download";

        var sb = new StringBuilder();
        foreach (var ch in fileName)
        {
            // Keep only printable ASCII characters (0x20-0x7E), excluding problematic ones
            if (ch >= 0x20 && ch <= 0x7E && ch != '"' && ch != '*' && ch != ':' && ch != '<' && ch != '>' && ch != '?' && ch != '|' && ch != '/'  && ch != '\\')
            {
                sb.Append(ch);
            }
        }

        // Clean up: collapse multiple spaces, trim, remove leading/trailing dashes
        var result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim().Trim('-').Trim();
        return string.IsNullOrEmpty(result) ? "download" : result;
    }
}
