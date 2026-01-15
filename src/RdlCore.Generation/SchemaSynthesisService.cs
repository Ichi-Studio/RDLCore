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
    private readonly AxiomRdlCoreOptions _options;

    public SchemaSynthesisService(
        ILogger<SchemaSynthesisService> logger,
        RdlDocumentBuilder documentBuilder,
        TextboxGenerator textboxGenerator,
        TablixGenerator tablixGenerator,
        ILogicTranslationService translationService,
        IOptions<AxiomRdlCoreOptions> options)
    {
        _logger = logger;
        _documentBuilder = documentBuilder;
        _textboxGenerator = textboxGenerator;
        _tablixGenerator = tablixGenerator;
        _translationService = translationService;
        _options = options.Value;
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
            [.. images.Select(img => 
                RdlNamespaces.RdlElement("EmbeddedImage",
                    new XAttribute("Name", img.name),
                    RdlNamespaces.RdlElement("MIMEType", img.mimeType),
                    RdlNamespaces.RdlElement("ImageData", Convert.ToBase64String(img.data))
                )
            )]
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
        var result = element switch
        {
            ParagraphElement para => await CreateTextboxAsync(para, cancellationToken),
            TableElement table => CreateTableWithExtractedDateRow(table),
            ImageElement img => CreateImage(img),
            _ => null
        };
        
        // Ensure element fits within printable width
        if (result != null)
        {
            EnsureElementFitsWithinPrintableWidth(result);
        }
        
        return result;
    }
    
    /// <summary>
    /// Ensures that an element's Left + Width does not exceed the printable width.
    /// If it does, adjusts the Width to fit within bounds.
    /// Also recursively checks child elements in containers.
    /// </summary>
    private void EnsureElementFitsWithinPrintableWidth(XElement element)
    {
        var printableWidth = _options.Generation.PrintableWidthInches;
        
        // Check if this is a container with ReportItems (like Rectangle)
        var reportItems = element.Element(RdlNamespaces.Rdl + "ReportItems");
        if (reportItems != null)
        {
            // Recursively check all child elements
            foreach (var child in reportItems.Elements())
            {
                EnsureElementFitsWithinPrintableWidth(child);
            }
        }
        
        // Special handling for Tablix - check column widths
        if (element.Name.LocalName == "Tablix")
        {
            EnsureTablixFitsWithinPrintableWidth(element, printableWidth);
        }
        
        var leftElement = element.Element(RdlNamespaces.Rdl + "Left");
        var widthElement = element.Element(RdlNamespaces.Rdl + "Width");
        
        if (leftElement == null || widthElement == null)
            return;
        
        var left = MarginOptions.ParseInches(leftElement.Value);
        var width = MarginOptions.ParseInches(widthElement.Value);
        var rightEdge = left + width;
        
        if (rightEdge > printableWidth)
        {
            // Calculate the maximum allowed width
            var maxWidth = printableWidth - left;
            
            if (maxWidth < 0.5)
            {
                // If left is too far right, reset left to 0 and use full printable width
                leftElement.Value = "0in";
                widthElement.Value = $"{printableWidth:F5}in";
                _logger.LogDebug("Reset element position: Left=0in, Width={Width:F2}in", printableWidth);
            }
            else
            {
                // Reduce width to fit within bounds
                widthElement.Value = $"{maxWidth:F5}in";
                _logger.LogDebug("Adjusted element width from {OldWidth:F2}in to {NewWidth:F2}in to fit printable area", 
                    width, maxWidth);
            }
        }
    }
    
    /// <summary>
    /// Ensures Tablix column widths fit within printable area
    /// </summary>
    private void EnsureTablixFitsWithinPrintableWidth(XElement tablix, double printableWidth)
    {
        var leftElement = tablix.Element(RdlNamespaces.Rdl + "Left");
        var left = leftElement != null ? MarginOptions.ParseInches(leftElement.Value) : 0;
        var availableWidth = printableWidth - left;
        
        var tablixBody = tablix.Element(RdlNamespaces.Rdl + "TablixBody");
        var tablixColumns = tablixBody?.Element(RdlNamespaces.Rdl + "TablixColumns");
        
        if (tablixColumns == null)
            return;
        
        var columns = tablixColumns.Elements(RdlNamespaces.Rdl + "TablixColumn").ToList();
        if (columns.Count == 0)
            return;
        
        // Calculate total current width
        double totalWidth = 0;
        var columnWidths = new List<double>();
        foreach (var col in columns)
        {
            var widthEl = col.Element(RdlNamespaces.Rdl + "Width");
            var w = widthEl != null ? MarginOptions.ParseInches(widthEl.Value) : 1.0;
            columnWidths.Add(w);
            totalWidth += w;
        }
        
        // If total width exceeds available width, scale down proportionally
        if (totalWidth > availableWidth)
        {
            var scaleFactor = availableWidth / totalWidth;
            _logger.LogDebug("Scaling Tablix columns by factor {Factor:F3} to fit within {Available:F2}in", 
                scaleFactor, availableWidth);
            
            for (int i = 0; i < columns.Count; i++)
            {
                var widthEl = columns[i].Element(RdlNamespaces.Rdl + "Width");
                if (widthEl != null)
                {
                    var newWidth = columnWidths[i] * scaleFactor;
                    widthEl.Value = $"{newWidth:F5}in";
                }
            }
            
            // Also update the Tablix Width element if present
            var tablixWidthElement = tablix.Element(RdlNamespaces.Rdl + "Width");
            if (tablixWidthElement != null)
            {
                tablixWidthElement.Value = $"{availableWidth:F5}in";
            }
        }
    }

    /// <summary>
    /// Creates table elements, extracting date rows as separate textboxes positioned to the right
    /// </summary>
    private XElement CreateTableWithExtractedDateRow(TableElement table)
    {
        var bounds = table.Bounds.ToInches();
        var elements = new List<XElement>();
        
        // Check if first row contains date-related content that should be extracted
        var extractedRows = new List<int>();
        double dateRowHeight = 0;
        
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            // Check if this row should be extracted (contains "日期" and is a simple row)
            if (ShouldExtractAsDateRow(row))
            {
                var dateContent = GetRowContent(row);
                var rowHeight = row.Height / 72.0;
                
                // Create a right-aligned textbox for the date
                var dateTextbox = CreateDateTextbox(dateContent, bounds.Left, bounds.Top + dateRowHeight, bounds.Width);
                elements.Add(dateTextbox);
                
                extractedRows.Add(rowIndex);
                dateRowHeight += rowHeight;
                
                _logger.LogInformation("Extracted date row: {Content}", dateContent);
            }
        }
        
        // If we extracted any rows, create a modified table without those rows
        if (extractedRows.Count > 0 && extractedRows.Count < table.Rows.Count)
        {
            var modifiedTable = CreateTableWithoutRows(table, extractedRows);
            var tablix = _tablixGenerator.CreateTablix(modifiedTable, null, 
                bounds.Left, bounds.Top + dateRowHeight);
            elements.Add(tablix);
        }
        else if (extractedRows.Count == 0)
        {
            // No rows extracted, create normal tablix
            return _tablixGenerator.CreateTablix(table, null, bounds.Left, bounds.Top);
        }
        
        // If we have multiple elements, wrap them in a container or return them individually
        // Since RDLC doesn't have a container, we'll return a Rectangle containing all elements
        if (elements.Count == 1)
        {
            return elements[0];
        }
        
        // Return as a Rectangle container
        return CreateRectangleContainer(elements, bounds);
    }
    
    /// <summary>
    /// Determines if a row should be extracted as a separate date element
    /// </summary>
    private bool ShouldExtractAsDateRow(Abstractions.Models.TableRow row)
    {
        // Only extract if it's the first few rows and contains "日期"
        if (row.Cells.Count == 0) return false;
        
        var content = GetRowContent(row);
        var trimmed = content.Trim();
        
        // Check if it contains date-related content
        // "日期" should be at the start or alone, not in a complex cell
        return trimmed.Contains("日期") && 
               !trimmed.Contains('(') && 
               !trimmed.Contains('（') &&
               trimmed.Length < 30 &&
               row.Cells.Count <= 2; // Simple row with 1-2 cells
    }
    
    /// <summary>
    /// Gets the combined text content of a row
    /// </summary>
    private string GetRowContent(Abstractions.Models.TableRow row)
    {
        var contents = new List<string>();
        foreach (var cell in row.Cells)
        {
            foreach (var element in cell.Content)
            {
                var text = element switch
                {
                    TextElement t => t.Text,
                    ParagraphElement p => string.Join("", p.Runs.Select(r => r.Text)),
                    _ => string.Empty
                };
                if (!string.IsNullOrWhiteSpace(text))
                {
                    contents.Add(text);
                }
            }
        }
        return string.Join(" ", contents);
    }
    
    /// <summary>
    /// Creates a right-aligned textbox for date content
    /// </summary>
    private XElement CreateDateTextbox(string content, double left, double top, double tableWidth)
    {
        var name = $"DateTextbox_{Guid.NewGuid():N}".Substring(0, 20);
        var sanitizedContent = RdlNamespaces.SanitizeXmlString(content);
        
        return RdlNamespaces.RdlElement("Textbox",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("CanGrow", "true"),
            RdlNamespaces.RdlElement("KeepTogether", "true"),
            RdlNamespaces.RdlElement("Paragraphs",
                RdlNamespaces.RdlElement("Paragraph",
                    RdlNamespaces.RdlElement("TextRuns",
                        RdlNamespaces.RdlElement("TextRun",
                            RdlNamespaces.RdlElement("Value", sanitizedContent),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdlElement("Style",
                        RdlNamespaces.RdlElement("TextAlign", "Right")
                    )
                )
            ),
            RdlNamespaces.RdElement("DefaultName", name),
            RdlNamespaces.RdlElement("Top", $"{top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{left:F5}in"),
            RdlNamespaces.RdlElement("Height", "0.25in"),
            RdlNamespaces.RdlElement("Width", $"{tableWidth:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
    }
    
    /// <summary>
    /// Creates a table without the specified rows
    /// </summary>
    private TableElement CreateTableWithoutRows(TableElement original, List<int> rowsToRemove)
    {
        var newRows = original.Rows
            .Where((row, index) => !rowsToRemove.Contains(index))
            .ToList();
        
        // Calculate new bounds
        var removedHeight = rowsToRemove.Sum(i => original.Rows[i].Height);
        var newBounds = new BoundingBox(
            original.Bounds.Left,
            original.Bounds.Top,
            original.Bounds.Width,
            original.Bounds.Height - removedHeight);
        
        return new TableElement(
            original.Id,
            newBounds,
            original.ZIndex,
            newRows.Count,
            original.ColumnCount,
            newRows.AsReadOnly());
    }
    
    /// <summary>
    /// Creates a Rectangle container for multiple elements
    /// </summary>
    private XElement CreateRectangleContainer(List<XElement> elements, BoundingBox bounds)
    {
        var name = $"Container_{Guid.NewGuid():N}".Substring(0, 20);
        
        return RdlNamespaces.RdlElement("Rectangle",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("ReportItems", elements.ToArray()),
            RdlNamespaces.RdlElement("Top", $"{bounds.Top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{bounds.Left:F5}in"),
            RdlNamespaces.RdlElement("Height", $"{bounds.Height:F5}in"),
            RdlNamespaces.RdlElement("Width", $"{bounds.Width:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
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
        double headerHeight = 0; // Will be calculated based on actual content

        // Add header images first (only use the first unique one to avoid duplicates)
        if (headerImages.Count > 0)
        {
            var img = headerImages.First(); // Use first image, they're duplicates
            var bounds = img.Bounds.ToInches();
            var imageName = $"Image_{img.Id}";
            
            // Calculate proper dimensions
            double imgWidth = bounds.Width > 0 ? bounds.Width : 2.08;
            double imgHeight = bounds.Height > 0 ? bounds.Height : 0.69;
            
            // Set header height based on image + padding
            headerHeight = imgHeight + 0.1;

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
            
            // If no image, set a reasonable header height for text only
            if (headerHeight == 0)
            {
                headerHeight = 0.5;
            }
            
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
            // Use printable width for footer to avoid horizontal pagination
            var printableWidth = _options.Generation.PrintableWidthInches;
            
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
                RdlNamespaces.RdlElement("Width", $"{printableWidth:F2}in"),
                RdlNamespaces.RdlElement("Style")
            );

            _documentBuilder.SetPageFooter(doc, footerContent);
        }
    }
}
