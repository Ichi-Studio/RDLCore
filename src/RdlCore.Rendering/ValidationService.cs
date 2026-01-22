namespace RdlCore.Rendering;

/// <summary>
/// Implementation of validation service
/// </summary>
internal sealed class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;
    private readonly VisualComparer _visualComparer;
    private readonly ILogicTranslationService _translationService;
    private readonly IRdlReportRenderer _reportRenderer;

    public ValidationService(
        ILogger<ValidationService> logger,
        VisualComparer visualComparer,
        ILogicTranslationService translationService,
        IRdlReportRenderer reportRenderer)
    {
        _logger = logger;
        _visualComparer = visualComparer;
        _translationService = translationService;
        _reportRenderer = reportRenderer;
    }

    /// <inheritdoc />
    public async Task<SchemaValidationResult> ValidateSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating RDL schema");

        var errors = new List<ValidationMessage>();
        var warnings = new List<ValidationMessage>();

        // Check root element
        var root = rdlDocument.Root;
        if (root == null)
        {
            errors.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "SCHEMA001",
                "Document has no root element",
                null));
            return new SchemaValidationResult(false, errors.AsReadOnly(), warnings.AsReadOnly());
        }

        if (root.Name.LocalName != "Report")
        {
            errors.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "SCHEMA002",
                $"Root element must be 'Report', found '{root.Name.LocalName}'",
                null));
        }

        // Check namespace
        if (root.Name.Namespace != RdlNamespaces.Rdl)
        {
            warnings.Add(new ValidationMessage(
                ValidationSeverity.Warning,
                "SCHEMA003",
                "Document namespace does not match RDL 2016",
                null));
        }

        // Check required elements
        ValidateRequiredElements(root, errors);

        // Validate structure
        ValidateReportStructure(root, errors, warnings);

        var isValid = errors.Count == 0;
        _logger.LogInformation("Schema validation complete: IsValid={IsValid}, Errors={Errors}, Warnings={Warnings}",
            isValid, errors.Count, warnings.Count);

        return new SchemaValidationResult(isValid, errors.AsReadOnly(), warnings.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<ExpressionValidationResult> ValidateExpressionsAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating expressions in RDL document");

        var messages = new List<ValidationMessage>();
        var violations = new List<string>();

        // Find all Value elements that may contain expressions
        var valueElements = rdlDocument.Descendants()
            .Where(e => e.Name.LocalName == "Value")
            .Select(e => e.Value)
            .Where(v => v.StartsWith("="))
            .ToList();

        _logger.LogDebug("Found {Count} expressions to validate", valueElements.Count);

        foreach (var expression in valueElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _translationService.ValidateExpressionAsync(expression, cancellationToken);
            
            messages.AddRange(result.Messages);
            violations.AddRange(result.SandboxViolations);
        }

        var isValid = violations.Count == 0 && 
            !messages.Exists(m => m.Severity == ValidationSeverity.Error);

        _logger.LogInformation("Expression validation complete: IsValid={IsValid}, Messages={Messages}",
            isValid, messages.Count);

        return new ExpressionValidationResult(isValid, messages.AsReadOnly(), violations.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<VisualComparisonResult> CompareVisualsAsync(
        byte[] sourceImage,
        byte[] renderedImage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing visual output");
        return await Task.FromResult(_visualComparer.Compare(sourceImage, renderedImage));
    }

    /// <inheritdoc />
    public async Task<byte[]> RenderToPdfAsync(
        XDocument rdlDocument,
        System.Data.DataSet dataSet,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendering RDL to PDF");
        return await Task.FromResult(_reportRenderer.Render(rdlDocument, dataSet, "PDF"));
    }

    /// <inheritdoc />
    public async Task<byte[]> RenderToWordOpenXmlAsync(
        XDocument rdlDocument,
        System.Data.DataSet dataSet,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendering RDL to WordOpenXml");
        return await Task.FromResult(_reportRenderer.Render(rdlDocument, dataSet, "WORDOPENXML"));
    }

    private void ValidateRequiredElements(XElement root, List<ValidationMessage> errors)
    {
        var requiredElements = new[] { "ReportSections" };

        foreach (var elementName in requiredElements)
        {
            if (root.Element(RdlNamespaces.Rdl + elementName) == null)
            {
                errors.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "SCHEMA010",
                    $"Required element '{elementName}' is missing",
                    "Report"));
            }
        }
    }

    private void ValidateReportStructure(XElement root, List<ValidationMessage> errors, List<ValidationMessage> warnings)
    {
        // Validate ReportSection structure
        var sections = root.Elements(RdlNamespaces.Rdl + "ReportSections")
            .Elements(RdlNamespaces.Rdl + "ReportSection")
            .ToList();

        if (sections.Count == 0)
        {
            errors.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "SCHEMA020",
                "Report must contain at least one ReportSection",
                "ReportSections"));
        }

        foreach (var section in sections)
        {
            // Check Body
            if (section.Element(RdlNamespaces.Rdl + "Body") == null)
            {
                errors.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "SCHEMA021",
                    "ReportSection must contain a Body element",
                    "ReportSection"));
            }

            // Check Width
            if (section.Element(RdlNamespaces.Rdl + "Width") == null)
            {
                warnings.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    "SCHEMA022",
                    "ReportSection should specify Width",
                    "ReportSection"));
            }
        }

        // Validate DataSets if present
        var dataSets = root.Elements(RdlNamespaces.Rdl + "DataSets")
            .Elements(RdlNamespaces.Rdl + "DataSet")
            .ToList();

        foreach (var dataSet in dataSets)
        {
            var name = dataSet.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                errors.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "SCHEMA030",
                    "DataSet must have a Name attribute",
                    "DataSet"));
            }
        }
    }
}
