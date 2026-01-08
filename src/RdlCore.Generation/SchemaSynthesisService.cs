using RdlCore.Abstractions.Enums;
using RdlCore.Generation.Components;

namespace RdlCore.Generation;

/// <summary>
/// Implementation of schema synthesis service
/// </summary>
public class SchemaSynthesisService : ISchemaSynthesisService
{
    private readonly ILogger<SchemaSynthesisService> _logger;
    private readonly RdlDocumentBuilder _documentBuilder;
    private readonly TextboxGenerator _textboxGenerator;
    private readonly TablixGenerator _tablixGenerator;
    private readonly ILogicTranslationService _translationService;

    public SchemaSynthesisService(
        ILogger<SchemaSynthesisService> logger,
        RdlDocumentBuilder documentBuilder,
        TextboxGenerator textboxGenerator,
        TablixGenerator tablixGenerator,
        ILogicTranslationService translationService)
    {
        _logger = logger;
        _documentBuilder = documentBuilder;
        _textboxGenerator = textboxGenerator;
        _tablixGenerator = tablixGenerator;
        _translationService = translationService;
    }

    /// <inheritdoc />
    public async Task<XDocument> GenerateRdlDocumentAsync(
        DocumentStructureModel documentStructure,
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating RDL document from structure");

        // Create base document
        var doc = _documentBuilder.CreateEmptyDocument("ReportData");

        // Collect all images for EmbeddedImages section
        var embeddedImages = new List<(string name, string mimeType, byte[] data)>();
        
        // Collect header images separately
        var headerImages = new List<ImageElement>();

        // Track position for elements
        double currentTop = 0;

        // Process each page
        foreach (var page in documentStructure.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var element in page.Elements)
            {
                if (element is ImageElement img)
                {
                    var imageName = $"Image_{img.Id}";
                    embeddedImages.Add((imageName, img.MimeType, img.ImageData));
                    
                    // Check if this is a header image
                    if (img.Id.StartsWith("header_img_"))
                    {
                        headerImages.Add(img);
                        continue; // Don't add to body, will be added to PageHeader
                    }
                }

                var rdlElement = await GenerateElementAsync(element, logic, cancellationToken);
                if (rdlElement != null)
                {
                    _documentBuilder.AddReportItem(doc, rdlElement);
                }
            }
        }

        // Add EmbeddedImages section if there are any images
        if (embeddedImages.Count > 0)
        {
            AddEmbeddedImages(doc, embeddedImages);
        }

        // Add headers and footers from logical elements (now with images)
        await AddHeadersAndFootersAsync(doc, documentStructure, headerImages, cancellationToken);

        _logger.LogInformation("RDL document generation complete");
        return doc;
    }

    private void AddEmbeddedImages(XDocument doc, List<(string name, string mimeType, byte[] data)> images)
    {
        var embeddedImagesElement = RdlNamespaces.RdlElement("EmbeddedImages",
            images.Select(img => 
                RdlNamespaces.RdlElement("EmbeddedImage",
                    new XAttribute("Name", img.name),
                    RdlNamespaces.RdlElement("MIMEType", img.mimeType),
                    RdlNamespaces.RdlElement("ImageData", Convert.ToBase64String(img.data))
                )
            ).ToArray()
        );

        // Insert EmbeddedImages before ReportSections
        var reportSections = doc.Root?.Element(RdlNamespaces.Rdl + "ReportSections");
        if (reportSections != null)
        {
            reportSections.AddBeforeSelf(embeddedImagesElement);
        }
    }

    /// <inheritdoc />
    public async Task<XElement> CreateTablixAsync(
        TableStructure table,
        DataSetBinding binding,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating Tablix for table '{Id}'", table.Id);
        return await Task.FromResult(_tablixGenerator.CreateTablixFromStructure(table, binding, 0, 0));
    }

    /// <inheritdoc />
    public async Task<XElement> CreateTextboxAsync(
        ParagraphElement paragraph,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating Textbox for paragraph '{Id}'", paragraph.Id);
        var bounds = paragraph.Bounds.ToInches();
        return await Task.FromResult(_textboxGenerator.CreateTextbox(paragraph, bounds.Left, bounds.Top));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RdlDocumentInfo>> GenerateMultipleRdlDocumentsAsync(
        DocumentStructureModel documentStructure,
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<RdlDocumentInfo>();

        // If only one page, generate single document
        if (documentStructure.Pages.Count <= 1)
        {
            var singleDoc = await GenerateRdlDocumentAsync(documentStructure, logic, cancellationToken);
            documents.Add(new RdlDocumentInfo("Report_1", singleDoc, 1));
            return documents.AsReadOnly();
        }

        _logger.LogInformation("Generating {Count} separate RDL documents", documentStructure.Pages.Count);

        // Generate a separate document for each page
        var pageNumber = 0;
        foreach (var page in documentStructure.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageNumber++;

            // Create a single-page structure for this page
            var singlePageStructure = new DocumentStructureModel(
                documentStructure.Type,
                new List<PageElement> { page }.AsReadOnly(),
                documentStructure.LogicalElements,
                documentStructure.Metadata);

            var doc = await GenerateRdlDocumentAsync(singlePageStructure, logic, cancellationToken);
            
            var reportName = $"Report_{pageNumber}";
            documents.Add(new RdlDocumentInfo(reportName, doc, pageNumber));

            _logger.LogInformation("Generated document {Name} for page {Page}", reportName, pageNumber);
        }

        return documents.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task ValidateAgainstSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating RDL document against schema");

        // Basic structure validation
        var root = rdlDocument.Root;
        if (root == null || root.Name.LocalName != "Report")
        {
            throw new Abstractions.Exceptions.SchemaValidationException(
                [new ValidationMessage(
                    Abstractions.Enums.ValidationSeverity.Error,
                    "ROOT001",
                    "Document must have a Report root element",
                    null)]);
        }

        // Check required elements
        var requiredElements = new[] { "ReportSections" };
        var errors = new List<ValidationMessage>();

        foreach (var element in requiredElements)
        {
            if (root.Element(RdlNamespaces.Rdl + element) == null)
            {
                errors.Add(new ValidationMessage(
                    Abstractions.Enums.ValidationSeverity.Error,
                    $"REQ001",
                    $"Required element '{element}' is missing",
                    null));
            }
        }

        if (errors.Count > 0)
        {
            throw new Abstractions.Exceptions.SchemaValidationException(errors.AsReadOnly());
        }
    }

    private async Task<XElement?> GenerateElementAsync(
        ContentElement element,
        LogicExtractionResult logic,
        CancellationToken cancellationToken)
    {
        return element switch
        {
            ParagraphElement para => await CreateTextboxAsync(para, cancellationToken),
            TableElement table => _tablixGenerator.CreateTablix(table, null, 
                table.Bounds.ToInches().Left, table.Bounds.ToInches().Top),
            ImageElement img => CreateImage(img),
            _ => null
        };
    }

    private XElement CreateImage(ImageElement image)
    {
        var bounds = image.Bounds.ToInches();
        var name = $"Image_{image.Id}";
        var imageData = Convert.ToBase64String(image.ImageData);

        return RdlNamespaces.RdlElement("Image",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("Source", "Embedded"),
            RdlNamespaces.RdlElement("Value", name),
            RdlNamespaces.RdlElement("MIMEType", image.MimeType),
            RdlNamespaces.RdlElement("Sizing", "FitProportional"),
            RdlNamespaces.RdlElement("Top", $"{bounds.Top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{bounds.Left:F5}in"),
            RdlNamespaces.RdlElement("Height", $"{bounds.Height:F5}in"),
            RdlNamespaces.RdlElement("Width", $"{bounds.Width:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
    }

    private async Task AddHeadersAndFootersAsync(
        XDocument doc,
        DocumentStructureModel structure,
        List<ImageElement> headerImages,
        CancellationToken cancellationToken)
    {
        // Build PageHeader content with images
        var headerItems = new List<XElement>();
        double headerHeight = 0.5; // Default header height in inches

        // Add header images first (only use the first unique one to avoid duplicates)
        if (headerImages.Count > 0)
        {
            var img = headerImages.First(); // Use first image, they're duplicates
            var bounds = img.Bounds.ToInches();
            var imageName = $"Image_{img.Id}";
            
            // Calculate proper dimensions
            double imgWidth = bounds.Width > 0 ? bounds.Width : 2.0;
            double imgHeight = bounds.Height > 0 ? bounds.Height : 0.7;
            
            // Ensure header is tall enough for the image
            headerHeight = Math.Max(headerHeight, imgHeight + 0.1);

            var imageElement = RdlNamespaces.RdlElement("Image",
                new XAttribute("Name", imageName.Replace("header_img_", "HeaderImage")),
                RdlNamespaces.RdlElement("Source", "Embedded"),
                RdlNamespaces.RdlElement("Value", imageName),
                RdlNamespaces.RdlElement("MIMEType", img.MimeType),
                RdlNamespaces.RdlElement("Sizing", "FitProportional"),
                RdlNamespaces.RdlElement("Top", "0in"),
                RdlNamespaces.RdlElement("Left", "0in"),
                RdlNamespaces.RdlElement("Height", $"{imgHeight:F2}in"),
                RdlNamespaces.RdlElement("Width", $"{imgWidth:F2}in"),
                RdlNamespaces.RdlElement("Style")
            );
            headerItems.Add(imageElement);
            
            _logger.LogInformation("Added header image: {Width}x{Height} inches", imgWidth, imgHeight);
        }

        // Find header text from logical elements
        var headers = structure.LogicalElements
            .Where(e => e.Role == Abstractions.Enums.LogicalRole.Header)
            .ToList();

        if (headers.Any() && !string.IsNullOrWhiteSpace(headers.First().Content))
        {
            // Position text to the right of the image if there is one
            double textLeft = headerImages.Count > 0 ? 2.5 : 0;
            
            var headerTextbox = RdlNamespaces.RdlElement("Textbox",
                new XAttribute("Name", "HeaderTextbox"),
                RdlNamespaces.RdlElement("CanGrow", "true"),
                RdlNamespaces.RdlElement("Paragraphs",
                    RdlNamespaces.RdlElement("Paragraph",
                        RdlNamespaces.RdlElement("TextRuns",
                            RdlNamespaces.RdlElement("TextRun",
                                RdlNamespaces.RdlElement("Value", headers.First().Content ?? ""),
                                RdlNamespaces.RdlElement("Style")
                            )
                        ),
                        RdlNamespaces.RdlElement("Style")
                    )
                ),
                RdlNamespaces.RdlElement("Top", "0in"),
                RdlNamespaces.RdlElement("Left", $"{textLeft:F2}in"),
                RdlNamespaces.RdlElement("Height", $"{headerHeight:F2}in"),
                RdlNamespaces.RdlElement("Width", "4in"),
                RdlNamespaces.RdlElement("Style")
            );
            headerItems.Add(headerTextbox);
        }

        // Set PageHeader if we have any items
        if (headerItems.Count > 0)
        {
            _documentBuilder.SetPageHeader(doc, headerItems.ToArray(), headerHeight);
        }

        // Find footer logical elements
        var footers = structure.LogicalElements
            .Where(e => e.Role == Abstractions.Enums.LogicalRole.Footer)
            .ToList();

        if (footers.Any())
        {
            // Create page number expression
            var footerContent = RdlNamespaces.RdlElement("Textbox",
                new XAttribute("Name", "FooterTextbox"),
                RdlNamespaces.RdlElement("CanGrow", "true"),
                RdlNamespaces.RdlElement("Paragraphs",
                    RdlNamespaces.RdlElement("Paragraph",
                        RdlNamespaces.RdlElement("TextRuns",
                            RdlNamespaces.RdlElement("TextRun",
                                RdlNamespaces.RdlElement("Value", 
                                    "=\"Page \" & Globals!PageNumber & \" of \" & Globals!TotalPages"),
                                RdlNamespaces.RdlElement("Style")
                            )
                        ),
                        RdlNamespaces.RdlElement("Style",
                            RdlNamespaces.RdlElement("TextAlign", "Center")
                        )
                    )
                ),
                RdlNamespaces.RdlElement("Top", "0in"),
                RdlNamespaces.RdlElement("Left", "0in"),
                RdlNamespaces.RdlElement("Height", "0.25in"),
                RdlNamespaces.RdlElement("Width", "6.5in"),
                RdlNamespaces.RdlElement("Style")
            );

            _documentBuilder.SetPageFooter(doc, footerContent);
        }
    }
}
