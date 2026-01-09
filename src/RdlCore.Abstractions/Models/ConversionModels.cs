using System.Xml.Linq;
using RdlCore.Abstractions.Enums;

namespace RdlCore.Abstractions.Models;

/// <summary>
/// Represents a conversion request
/// </summary>
public record ConversionRequest(
    Stream DocumentStream,
    DocumentType? DocumentType,
    string? OutputPath,
    ConversionOptions Options);

/// <summary>
/// Conversion options
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
/// Represents the result of a conversion
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
    /// Gets all RDL documents (main + additional)
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
/// Information about an individual RDL document
/// </summary>
public record RdlDocumentInfo(
    string Name,
    XDocument Document,
    int PageNumber);

/// <summary>
/// Summary of conversion results
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
/// Represents a request for human intervention
/// </summary>
public record InterventionRequest(
    InterventionType Type,
    string ElementPath,
    string SourceContent,
    string SuggestedAction,
    ConfidenceLevel Confidence,
    IReadOnlyList<InterventionOption> Options);

/// <summary>
/// Represents an intervention option
/// </summary>
public record InterventionOption(
    string Id,
    string Description,
    string? Preview);

/// <summary>
/// Represents intervention response
/// </summary>
public record InterventionResponse(
    string RequestId,
    string SelectedOptionId,
    string? CustomValue);

/// <summary>
/// Represents schema validation result
/// </summary>
public record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationMessage> Errors,
    IReadOnlyList<ValidationMessage> Warnings);

/// <summary>
/// Represents visual comparison result
/// </summary>
public record VisualComparisonResult(
    bool IsMatch,
    double SsimScore,
    double Threshold,
    byte[]? DifferenceImage,
    IReadOnlyList<DifferenceRegion> Differences);

/// <summary>
/// Represents a region of visual difference
/// </summary>
public record DifferenceRegion(
    BoundingBox Bounds,
    double Severity,
    string Description);

/// <summary>
/// Represents pipeline progress
/// </summary>
public record PipelineProgress(
    PipelinePhase CurrentPhase,
    int TotalPhases,
    double PercentComplete,
    string StatusMessage,
    IReadOnlyList<PhaseResult> CompletedPhases);

/// <summary>
/// Represents the result of a pipeline phase
/// </summary>
public record PhaseResult(
    PipelinePhase Phase,
    bool Success,
    TimeSpan Duration,
    string? Message);

/// <summary>
/// Configuration options for the RDL Core system
/// </summary>
public record AxiomRdlCoreOptions
{
    /// <summary>Parsing options</summary>
    public ParsingOptions Parsing { get; init; } = new();
    
    /// <summary>Generation options</summary>
    public GenerationOptions Generation { get; init; } = new();
    
    /// <summary>Validation options</summary>
    public ValidationOptions Validation { get; init; } = new();
}

/// <summary>
/// Parsing configuration options
/// </summary>
public record ParsingOptions
{
    /// <summary>Maximum number of pages to process</summary>
    public int MaxPageCount { get; init; } = 100;
    
    /// <summary>Enable OCR for image-based PDFs</summary>
    public bool OcrEnabled { get; init; } = true;
    
    /// <summary>OCR languages</summary>
    public IReadOnlyList<string> OcrLanguages { get; init; } = ["en", "zh-Hans"];
    
    /// <summary>Minimum OCR confidence threshold</summary>
    public double OcrConfidenceThreshold { get; init; } = 0.8;
}

/// <summary>
/// Generation configuration options
/// </summary>
public record GenerationOptions
{
    /// <summary>Default page width</summary>
    public string DefaultPageWidth { get; init; } = "8.5in";
    
    /// <summary>Default page height</summary>
    public string DefaultPageHeight { get; init; } = "11in";
    
    /// <summary>Default margins</summary>
    public MarginOptions DefaultMargins { get; init; } = new();
    
    /// <summary>Embed images in report</summary>
    public bool EmbedImages { get; init; } = true;
    
    /// <summary>Maximum image resolution (DPI)</summary>
    public int MaxImageResolution { get; init; } = 300;
    
    /// <summary>
    /// Gets the printable width (PageWidth - LeftMargin - RightMargin)
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
    /// Gets the printable width in inches as a double value
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
/// Margin configuration
/// </summary>
public record MarginOptions
{
    /// <summary>Top margin</summary>
    public string Top { get; init; } = "0.5in";
    
    /// <summary>Bottom margin</summary>
    public string Bottom { get; init; } = "0.5in";
    
    /// <summary>Left margin</summary>
    public string Left { get; init; } = "0.75in";
    
    /// <summary>Right margin</summary>
    public string Right { get; init; } = "0.75in";
    
    /// <summary>
    /// Parses a dimension string (e.g., "0.75in") and returns the numeric value in inches
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
/// Validation configuration options
/// </summary>
public record ValidationOptions
{
    /// <summary>Enable strict schema validation</summary>
    public bool StrictSchemaValidation { get; init; } = true;
    
    /// <summary>Enable expression sandbox mode</summary>
    public bool ExpressionSandboxMode { get; init; } = true;
    
    /// <summary>Visual comparison threshold (SSIM)</summary>
    public double VisualComparisonThreshold { get; init; } = 0.95;
}

/// <summary>
/// Represents data set binding information
/// </summary>
public record DataSetBinding(
    string DataSetName,
    IReadOnlyList<DataFieldInfo> Fields);

/// <summary>
/// Represents information about a data field
/// </summary>
public record DataFieldInfo(
    string Name,
    string DataType,
    string? DisplayName);

/// <summary>
/// Represents table structure for RDLC generation
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
