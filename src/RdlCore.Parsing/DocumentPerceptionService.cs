using System.Runtime.CompilerServices;
using RdlCore.Abstractions.Exceptions;
using RdlCore.Parsing.Common;

namespace RdlCore.Parsing;

/// <summary>
/// Implementation of document perception service
/// </summary>
public class DocumentPerceptionService : IDocumentPerceptionService, IPageAnalyzer
{
    private readonly ILogger<DocumentPerceptionService> _logger;
    private readonly DocumentTypeDetector _typeDetector;
    private readonly IEnumerable<IDocumentParser> _parsers;

    public DocumentPerceptionService(
        ILogger<DocumentPerceptionService> logger,
        DocumentTypeDetector typeDetector,
        IEnumerable<IDocumentParser> parsers)
    {
        _logger = logger;
        _typeDetector = typeDetector;
        _parsers = parsers;
    }

    /// <inheritdoc />
    public async Task<DocumentStructureModel> AnalyzeAsync(
        Stream documentStream, 
        DocumentType type,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document analysis for type: {Type}", type);

        // Auto-detect type if unknown
        if (type == DocumentType.Unknown)
        {
            type = await _typeDetector.DetectTypeAsync(documentStream, cancellationToken);
            _logger.LogInformation("Auto-detected document type: {Type}", type);
        }

        // Find appropriate parser
        var parser = _parsers.FirstOrDefault(p => p.SupportedType == type)
            ?? throw new DocumentParsingException($"No parser available for document type: {type}", type);

        var structure = parser is IConfigurableDocumentParser configurable
            ? await configurable.ParseAsync(documentStream, options, cancellationToken)
            : await parser.ParseAsync(documentStream, cancellationToken);

        _logger.LogInformation(
            "Document analysis complete: {Pages} pages, {Elements} logical elements",
            structure.Pages.Count,
            structure.LogicalElements.Count);

        return structure;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LogicalElement>> IdentifyLogicalRolesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Identifying logical roles in document");

        var elements = new List<LogicalElement>(model.LogicalElements);

        // Analyze content to identify additional logical elements
        foreach (var page in model.Pages)
        {
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var role = IdentifyRole(element, page);
                if (role != LogicalRole.Unknown && role != LogicalRole.Body)
                {
                    elements.Add(new LogicalElement(
                        Id: $"logical_{element.Id}",
                        Role: role,
                        Bounds: element.Bounds,
                        Content: GetElementContent(element),
                        Properties: null));
                }
            }
        }

        return elements;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BoundingBox>> ExtractLayoutAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Extracting layout information");

        var bounds = new List<BoundingBox>();

        foreach (var page in model.Pages)
        {
            foreach (var element in page.Elements)
            {
                bounds.Add(element.Bounds);
            }
        }

        return bounds;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PageAnalysisResult> AnalyzePagesAsync(
        Stream documentStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var type = await _typeDetector.DetectTypeAsync(documentStream, cancellationToken);
        var defaultOptions = new ConversionOptions(
            DataSetName: null,
            SchemaPath: null,
            StyleTemplate: null,
            ForceOverwrite: false,
            VerboseOutput: false,
            DryRun: false);
        var structure = await AnalyzeAsync(documentStream, type, defaultOptions, cancellationToken);

        foreach (var page in structure.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var logicalElements = structure.LogicalElements
                .Where(le => BoundingBoxCalculator.Intersects(le.Bounds, 
                    new BoundingBox(0, 0, page.Width, page.Height)))
                .ToList();

            yield return new PageAnalysisResult(
                PageNumber: page.PageNumber,
                Elements: page.Elements,
                LogicalElements: logicalElements,
                Confidence: 0.95);
        }
    }

    private LogicalRole IdentifyRole(ContentElement element, PageElement page)
    {
        // Header detection: top 10% of page
        if (element.Bounds.Top < page.Height * 0.1)
            return LogicalRole.Header;

        // Footer detection: bottom 10% of page
        if (element.Bounds.Bottom > page.Height * 0.9)
            return LogicalRole.Footer;

        // Title detection: large text near top
        if (element is TextElement text && text.Style.FontSize > 14 && element.Bounds.Top < page.Height * 0.2)
            return LogicalRole.Title;

        // Table detection
        if (element is TableElement)
            return LogicalRole.Table;

        // Image detection
        if (element is ImageElement)
            return LogicalRole.Image;

        return LogicalRole.Body;
    }

    private string? GetElementContent(ContentElement element) => element switch
    {
        TextElement text => text.Text,
        ParagraphElement para => string.Concat(para.Runs.Select(r => r.Text)),
        _ => null
    };
}
