using System.Runtime.CompilerServices;
using System.Xml.Linq;
using RdlCore.Abstractions.Enums;
using RdlCore.Abstractions.Models;

namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// Service for document perception - Phase 1 of the conversion pipeline
/// </summary>
public interface IDocumentPerceptionService
{
    /// <summary>
    /// Analyzes a document stream and extracts its structure
    /// </summary>
    /// <param name="documentStream">The document stream to analyze</param>
    /// <param name="type">The document type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document structure model</returns>
    Task<DocumentStructureModel> AnalyzeAsync(
        Stream documentStream, 
        DocumentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies logical roles of elements in the document
    /// </summary>
    /// <param name="model">The document structure model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The identified logical elements</returns>
    Task<IEnumerable<LogicalElement>> IdentifyLogicalRolesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts layout information from the document
    /// </summary>
    /// <param name="model">The document structure model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The bounding boxes of elements</returns>
    Task<IEnumerable<BoundingBox>> ExtractLayoutAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for logic decomposition - Phase 2 of the conversion pipeline
/// </summary>
public interface ILogicDecompositionService
{
    /// <summary>
    /// Extracts field codes from the document structure
    /// </summary>
    /// <param name="model">The document structure model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The logic extraction result</returns>
    Task<LogicExtractionResult> ExtractFieldCodesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds an Abstract Syntax Tree from an expression
    /// </summary>
    /// <param name="expression">The expression to parse</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parsed AST</returns>
    Task<AbstractSyntaxTree> BuildAstAsync(
        string expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies conditional branches in the logic
    /// </summary>
    /// <param name="result">The logic extraction result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The identified conditional branches</returns>
    Task<IEnumerable<ConditionalBranch>> IdentifyConditionsAsync(
        LogicExtractionResult result,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for schema synthesis - Phase 3 of the conversion pipeline
/// </summary>
public interface ISchemaSynthesisService
{
    /// <summary>
    /// Generates an RDL document from the document structure and logic
    /// </summary>
    /// <param name="documentStructure">The document structure model</param>
    /// <param name="logic">The logic extraction result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated RDL document</returns>
    Task<XDocument> GenerateRdlDocumentAsync(
        DocumentStructureModel documentStructure, 
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates multiple RDL documents, one for each page/section in the source document
    /// </summary>
    /// <param name="documentStructure">The document structure model</param>
    /// <param name="logic">The logic extraction result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of RDL document info objects</returns>
    Task<IReadOnlyList<RdlDocumentInfo>> GenerateMultipleRdlDocumentsAsync(
        DocumentStructureModel documentStructure,
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Tablix element for a table
    /// </summary>
    /// <param name="table">The table structure</param>
    /// <param name="binding">The dataset binding</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Tablix XML element</returns>
    Task<XElement> CreateTablixAsync(
        TableStructure table, 
        DataSetBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Textbox element for a paragraph
    /// </summary>
    /// <param name="paragraph">The paragraph element</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Textbox XML element</returns>
    Task<XElement> CreateTextboxAsync(
        ParagraphElement paragraph,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the RDL document against the schema
    /// </summary>
    /// <param name="rdlDocument">The RDL document to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ValidateAgainstSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for logic translation - Phase 4 of the conversion pipeline
/// </summary>
public interface ILogicTranslationService
{
    /// <summary>
    /// Translates an AST to a VB expression
    /// </summary>
    /// <param name="ast">The AST to translate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The VB expression string</returns>
    Task<string> TranslateToVbExpressionAsync(
        AbstractSyntaxTree ast,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an expression
    /// </summary>
    /// <param name="expression">The expression to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The validation result</returns>
    Task<ExpressionValidationResult> ValidateExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes an expression
    /// </summary>
    /// <param name="expression">The expression to optimize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The optimized expression</returns>
    Task<string> OptimizeExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for validation - Phase 5 of the conversion pipeline
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates the RDL document schema
    /// </summary>
    /// <param name="rdlDocument">The RDL document to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The schema validation result</returns>
    Task<SchemaValidationResult> ValidateSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates all expressions in the RDL document
    /// </summary>
    /// <param name="rdlDocument">The RDL document to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The expression validation result</returns>
    Task<ExpressionValidationResult> ValidateExpressionsAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares the visual output of the source and rendered documents
    /// </summary>
    /// <param name="sourceImage">The source document image</param>
    /// <param name="renderedImage">The rendered report image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The visual comparison result</returns>
    Task<VisualComparisonResult> CompareVisualsAsync(
        byte[] sourceImage, 
        byte[] renderedImage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the RDL document to PDF
    /// </summary>
    /// <param name="rdlDocument">The RDL document to render</param>
    /// <param name="dataSet">The data set to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The rendered PDF bytes</returns>
    Task<byte[]> RenderToPdfAsync(
        XDocument rdlDocument, 
        System.Data.DataSet dataSet,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for orchestrating the conversion pipeline
/// </summary>
public interface IConversionPipelineService
{
    /// <summary>
    /// Executes the full conversion pipeline
    /// </summary>
    /// <param name="request">The conversion request</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The conversion result</returns>
    Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for handling human intervention requests
/// </summary>
public interface IHumanInterventionHandler
{
    /// <summary>
    /// Requests human intervention
    /// </summary>
    /// <param name="request">The intervention request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The intervention response</returns>
    Task<InterventionResponse> RequestInterventionAsync(
        InterventionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for document parsers
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Gets the supported document type
    /// </summary>
    DocumentType SupportedType { get; }

    /// <summary>
    /// Parses a document stream
    /// </summary>
    /// <param name="stream">The document stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document structure model</returns>
    Task<DocumentStructureModel> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for async page analysis
/// </summary>
public interface IPageAnalyzer
{
    /// <summary>
    /// Analyzes pages asynchronously using async streams
    /// </summary>
    /// <param name="documentStream">The document stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of page analysis results</returns>
    IAsyncEnumerable<PageAnalysisResult> AnalyzePagesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
