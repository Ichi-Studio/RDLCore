namespace RdlCore.Abstractions.Models;

/// <summary>
/// 表示转换请求
/// </summary>
public record ConversionRequest(
    Stream DocumentStream,
    DocumentType? DocumentType,
    string? OutputPath,
    ConversionOptions Options);

/// <summary>
/// 转换选项
/// </summary>
public record ConversionOptions(
    string? DataSetName,
    string? SchemaPath,
    string? StyleTemplate,
    bool ForceOverwrite,
    bool VerboseOutput,
    bool DryRun,
    bool OcrEnabled = true,
    string? OcrLanguage = null,
    double OcrConfidenceThreshold = 0.8);

/// <summary>
/// 表示转换结果
/// </summary>
public record ConversionResult(
    ConversionStatus Status,
    XDocument? RdlDocument,
    string? OutputPath,
    ConversionSummary Summary,
    IReadOnlyList<ValidationMessage> Messages,
    IReadOnlyList<InterventionRequest> InterventionRequests,
    TimeSpan ElapsedTime,
    IReadOnlyList<RdlDocumentInfo>? AdditionalDocuments = null)
{
    /// <summary>
    /// 获取所有 RDL 文档（主文档 + 附加文档）
    /// </summary>
    public IReadOnlyList<RdlDocumentInfo> GetAllDocuments()
    {
        var docs = new List<RdlDocumentInfo>();
        if (RdlDocument != null)
        {
            docs.Add(new RdlDocumentInfo("Report_1", RdlDocument, 1));
        }
        if (AdditionalDocuments != null)
        {
            docs.AddRange(AdditionalDocuments);
        }
        return docs.AsReadOnly();
    }
}

/// <summary>
/// 单个 RDL 文档的信息
/// </summary>
public record RdlDocumentInfo(
    string Name,
    XDocument Document,
    int PageNumber);

/// <summary>
/// 转换结果摘要
/// </summary>
public record ConversionSummary(
    int TextboxCount,
    int TablixCount,
    int ImageCount,
    int ChartCount,
    int ExpressionCount,
    int WarningCount,
    int ErrorCount);

/// <summary>
/// 表示人工干预请求
/// </summary>
public record InterventionRequest(
    InterventionType Type,
    string ElementPath,
    string SourceContent,
    string SuggestedAction,
    ConfidenceLevel Confidence,
    IReadOnlyList<InterventionOption> Options);

/// <summary>
/// 表示干预选项
/// </summary>
public record InterventionOption(
    string Id,
    string Description,
    string? Preview);

/// <summary>
/// 表示干预响应
/// </summary>
public record InterventionResponse(
    string RequestId,
    string SelectedOptionId,
    string? CustomValue);

/// <summary>
/// 表示模式验证结果
/// </summary>
public record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationMessage> Errors,
    IReadOnlyList<ValidationMessage> Warnings);

/// <summary>
/// 表示视觉比较结果
/// </summary>
public record VisualComparisonResult(
    bool IsMatch,
    double SsimScore,
    double Threshold,
    byte[]? DifferenceImage,
    IReadOnlyList<DifferenceRegion> Differences);

/// <summary>
/// 表示视觉差异区域
/// </summary>
public record DifferenceRegion(
    BoundingBox Bounds,
    double Severity,
    string Description);

/// <summary>
/// 表示管道进度
/// </summary>
public record PipelineProgress(
    PipelinePhase CurrentPhase,
    int TotalPhases,
    double PercentComplete,
    string StatusMessage,
    IReadOnlyList<PhaseResult> CompletedPhases);

/// <summary>
/// 表示管道阶段的结果
/// </summary>
public record PhaseResult(
    PipelinePhase Phase,
    bool Success,
    TimeSpan Duration,
    string? Message);

/// <summary>
/// RDL Core 系统的配置选项
/// </summary>
public record AxiomRdlCoreOptions
{
    /// <summary>解析选项</summary>
    public ParsingOptions Parsing { get; init; } = new();
    
    /// <summary>生成选项</summary>
    public GenerationOptions Generation { get; init; } = new();
    
    /// <summary>验证选项</summary>
    public ValidationOptions Validation { get; init; } = new();
}

/// <summary>
/// 解析配置选项
/// </summary>
public record ParsingOptions
{
    /// <summary>要处理的最大页数</summary>
    public int MaxPageCount { get; init; } = 100;
    
    /// <summary>为基于图像的 PDF 启用 OCR</summary>
    public bool OcrEnabled { get; init; } = true;
    
    /// <summary>OCR 语言</summary>
    public IReadOnlyList<string> OcrLanguages { get; init; } = ["en", "zh-Hans"];
    
    /// <summary>最低 OCR 置信度阈值</summary>
    public double OcrConfidenceThreshold { get; init; } = 0.8;
}

/// <summary>
/// 生成配置选项
/// </summary>
public record GenerationOptions
{
    /// <summary>默认页面宽度</summary>
    public string DefaultPageWidth { get; init; } = "8.5in";
    
    /// <summary>默认页面高度</summary>
    public string DefaultPageHeight { get; init; } = "11in";
    
    /// <summary>默认边距</summary>
    public MarginOptions DefaultMargins { get; init; } = new();
    
    /// <summary>在报表中嵌入图像</summary>
    public bool EmbedImages { get; init; } = true;
    
    /// <summary>最大图像分辨率 (DPI)</summary>
    public int MaxImageResolution { get; init; } = 300;

    /// <summary>
    /// 严格保真模式：尽可能避免任何“自适应/纠正/字体替换”等启发式行为
    /// </summary>
    public bool StrictFidelity { get; init; } = false;
    
    /// <summary>
    /// 获取可打印宽度（页面宽度 - 左边距 - 右边距）
    /// </summary>
    public string PrintableWidth
    {
        get
        {
            var pageWidth = MarginOptions.ParseInches(DefaultPageWidth);
            var leftMargin = MarginOptions.ParseInches(DefaultMargins.Left);
            var rightMargin = MarginOptions.ParseInches(DefaultMargins.Right);
            var printable = pageWidth - leftMargin - rightMargin;
            return $"{printable:F2}in";
        }
    }
    
    /// <summary>
    /// 以英寸为单位获取可打印宽度的双精度值
    /// </summary>
    public double PrintableWidthInches
    {
        get
        {
            var pageWidth = MarginOptions.ParseInches(DefaultPageWidth);
            var leftMargin = MarginOptions.ParseInches(DefaultMargins.Left);
            var rightMargin = MarginOptions.ParseInches(DefaultMargins.Right);
            return pageWidth - leftMargin - rightMargin;
        }
    }
}

/// <summary>
/// 边距配置
/// </summary>
public record MarginOptions
{
    /// <summary>上边距</summary>
    public string Top { get; init; } = "0.5in";
    
    /// <summary>下边距</summary>
    public string Bottom { get; init; } = "0.5in";
    
    /// <summary>左边距</summary>
    public string Left { get; init; } = "0.75in";
    
    /// <summary>右边距</summary>
    public string Right { get; init; } = "0.75in";
    
    /// <summary>
    /// 解析尺寸字符串（例如 "0.75in"）并返回以英寸为单位的数值
    /// </summary>
    public static double ParseInches(string dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
            return 0;
        
        var value = dimension.Replace("in", "").Trim();
        return double.TryParse(value, out var result) ? result : 0;
    }
}

/// <summary>
/// 验证配置选项
/// </summary>
public record ValidationOptions
{
    /// <summary>启用严格模式验证</summary>
    public bool StrictSchemaValidation { get; init; } = true;
    
    /// <summary>启用表达式沙箱模式</summary>
    public bool ExpressionSandboxMode { get; init; } = true;
    
    /// <summary>视觉比较阈值 (SSIM)</summary>
    public double VisualComparisonThreshold { get; init; } = 0.95;
}

/// <summary>
/// 表示数据集绑定信息
/// </summary>
public record DataSetBinding(
    string DataSetName,
    IReadOnlyList<DataFieldInfo> Fields);

/// <summary>
/// 表示数据字段信息
/// </summary>
public record DataFieldInfo(
    string Name,
    string DataType,
    string? DisplayName);

/// <summary>
/// 表示 RDLC 生成的表格结构
/// </summary>
public record TableStructure(
    string Id,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyList<double> RowHeights,
    bool HasHeaderRow,
    bool IsDynamic,
    string? GroupByField);
