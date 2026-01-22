using System.Text;
using RdlCore.Abstractions.Exceptions;
using RdlCore.Parsing.Common;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using AxiomDocumentType = RdlCore.Abstractions.Enums.DocumentType;
using AxiomFieldCode = RdlCore.Abstractions.Models.FieldCode;
using AxiomTableRow = RdlCore.Abstractions.Models.TableRow;
using AxiomTableCell = RdlCore.Abstractions.Models.TableCell;

namespace RdlCore.Parsing.Word;

/// <summary>
/// Parses Word documents (.docx) into document structure models
/// </summary>
public class WordDocumentParser : IDocumentParser
{
    private readonly ILogger<WordDocumentParser> _logger;
    private readonly FieldCodeExtractor _fieldCodeExtractor;
    private readonly AxiomRdlCoreOptions _options;
    private readonly DocBinaryConverter _docConverter;

    /// <inheritdoc />
    public AxiomDocumentType SupportedType => AxiomDocumentType.Word;

    public WordDocumentParser(
        ILogger<WordDocumentParser> logger,
        FieldCodeExtractor fieldCodeExtractor,
        IOptions<AxiomRdlCoreOptions> options,
        DocBinaryConverter docConverter)
    {
        _logger = logger;
        _fieldCodeExtractor = fieldCodeExtractor;
        _options = options.Value;
        _docConverter = docConverter;
    }

    /// <inheritdoc />
    public async Task<DocumentStructureModel> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Word document parsing");

        // First, detect if this is a .doc (binary) or .docx (OpenXML) format
        var formatType = await DetectWordFormatAsync(stream, ct);
        
        Stream processingStream = stream;
        bool needsCleanup = false;
        
        if (formatType == WordFormatType.BinaryDoc)
        {
            _logger.LogInformation("Detected old .doc binary format, converting to .docx");
            
            try
            {
                // Convert .doc to .docx in memory
                processingStream = await _docConverter.ConvertDocToDocxAsync(stream, ct);
                needsCleanup = true;
                _logger.LogInformation(".doc conversion successful, proceeding with .docx parsing");
            }
            catch (DocumentParsingException)
            {
                throw; // Re-throw parsing exceptions as-is
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

        try
        {
            using var document = WordprocessingDocument.Open(processingStream, false, new OpenSettings { AutoSave = false });
            
            var mainPart = document.MainDocumentPart 
                ?? throw new DocumentParsingException("Document has no main part", AxiomDocumentType.Word);

            var metadata = ExtractMetadata(document);
            var pages = await ExtractPagesAsync(mainPart, ct);
            var logicalElements = await IdentifyLogicalElementsAsync(mainPart, ct);

            _logger.LogInformation("Word document parsing completed: {PageCount} pages, {ElementCount} logical elements",
                pages.Count, logicalElements.Count);

            return new DocumentStructureModel(
                AxiomDocumentType.Word,
                pages,
                logicalElements,
                metadata);
        }
        catch (OpenXmlPackageException ex)
        {
            _logger.LogError(ex, "Invalid or corrupted Word document format");
            throw new DocumentParsingException(
                "文件格式无效或已损坏。请确保：\n" +
                "1) 文件是有效的 .docx 格式（OpenXML）\n" +
                "2) 文件未损坏\n" +
                "3) 文件未加密或有密码保护\n" +
                "4) 如果是 .doc 格式，系统会自动转换",
                AxiomDocumentType.Word, ex);
        }
        catch (Exception ex) when (ex is not DocumentParsingException)
        {
            _logger.LogError(ex, "Failed to parse Word document");
            throw new DocumentParsingException($"Failed to parse Word document: {ex.Message}", AxiomDocumentType.Word, ex);
        }
        finally
        {
            // Clean up the converted stream if needed
            if (needsCleanup && processingStream != stream)
            {
                await processingStream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Detects whether the stream contains a .doc (binary) or .docx (OpenXML) format
    /// </summary>
    private async Task<WordFormatType> DetectWordFormatAsync(Stream stream, CancellationToken ct)
    {
        var originalPosition = stream.Position;
        try
        {
            var buffer = new byte[8];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 8), ct);

            if (bytesRead < 8)
            {
                return WordFormatType.Unknown;
            }

            // Check for OLE2/CFB signature (old .doc format)
            // D0 CF 11 E0 A1 B1 1A E1
            if (buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0 &&
                buffer[4] == 0xA1 && buffer[5] == 0xB1 && buffer[6] == 0x1A && buffer[7] == 0xE1)
            {
                return WordFormatType.BinaryDoc;
            }

            // Check for ZIP signature (new .docx format)
            // 50 4B 03 04
            if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
            {
                return WordFormatType.OpenXmlDocx;
            }

            return WordFormatType.Unknown;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private DocumentMetadata ExtractMetadata(WordprocessingDocument document)
    {
        var coreProps = document.PackageProperties;
        
        return new DocumentMetadata(
            Title: coreProps.Title,
            Author: coreProps.Creator,
            Subject: coreProps.Subject,
            CreatedDate: coreProps.Created,
            ModifiedDate: coreProps.Modified,
            PageCount: EstimatePageCount(document),
            FileName: null);
    }

    private int EstimatePageCount(WordprocessingDocument document)
    {
        // Try to get page count from extended properties
        var extProps = document.ExtendedFilePropertiesPart?.Properties;
        if (extProps?.Pages?.Text != null && int.TryParse(extProps.Pages.Text, out var pages))
        {
            return pages;
        }
        
        // Estimate based on content
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null) return 1;

        var paragraphCount = body.Descendants<Paragraph>().Count();
        return Math.Max(1, paragraphCount / 30); // Rough estimate: ~30 paragraphs per page
    }

    private async Task<IReadOnlyList<PageElement>> ExtractPagesAsync(
        MainDocumentPart mainPart, 
        CancellationToken ct)
    {
        var pages = new List<PageElement>();
        var body = mainPart.Document?.Body;
        
        if (body == null)
        {
            return pages;
        }

        // Get page dimensions from section properties
        var sectionProps = body.Descendants<SectionProperties>().LastOrDefault();
        var (pageWidth, pageHeight) = GetPageDimensions(sectionProps);

        // Extract header images (shared across all pages)
        var headerImages = ExtractHeaderImages(mainPart);

        // First, try to detect side-by-side layout (parallel tables/forms)
        var parallelSections = DetectParallelFormSections(body);
        if (parallelSections.Count > 1)
        {
            _logger.LogInformation("Detected {Count} parallel form sections (side-by-side layout)", parallelSections.Count);

            var pageNumber = 0;
            foreach (var section in parallelSections)
            {
                ct.ThrowIfCancellationRequested();
                pageNumber++;

                var elements = await ExtractContentElementsFromNodesAsync(section.Elements, mainPart, ct);
                
                var allElements = new List<ContentElement>();
                // Add header images to each page (since they're separate reports)
                allElements.AddRange(headerImages);
                allElements.AddRange(elements);

                if (allElements.Count == 0) continue;

                pages.Add(new PageElement(
                    PageNumber: pageNumber,
                    Width: pageWidth,
                    Height: pageHeight,
                    Elements: allElements));
            }

            return pages;
        }

        // Otherwise, split content by page breaks
        var contentSections = SplitContentByPageBreaks(body);
        
        _logger.LogInformation("Document split into {Count} sections/pages", contentSections.Count);

        var pageNum = 0;
        foreach (var section in contentSections)
        {
            ct.ThrowIfCancellationRequested();
            pageNum++;

            var elements = await ExtractContentElementsFromNodesAsync(section, mainPart, ct);
            
            // Only add header images to the first page
            var allElements = new List<ContentElement>();
            if (pageNum == 1)
            {
                allElements.AddRange(headerImages);
            }
            allElements.AddRange(elements);

            // Skip empty pages
            if (allElements.Count == 0) continue;

            pages.Add(new PageElement(
                PageNumber: pageNum,
                Width: pageWidth,
                Height: pageHeight,
                Elements: allElements));
        }

        // If no page breaks found, treat as single page
        if (pages.Count == 0)
        {
            var elements = await ExtractContentElementsAsync(body, ct);
            var allElements = new List<ContentElement>();
            allElements.AddRange(headerImages);
            allElements.AddRange(elements);

            pages.Add(new PageElement(
                PageNumber: 1,
                Width: pageWidth,
                Height: pageHeight,
                Elements: allElements));
        }

        return pages;
    }

    /// <summary>
    /// Detects parallel form sections (side-by-side tables) in landscape documents
    /// </summary>
    private List<ParallelFormSection> DetectParallelFormSections(Body body)
    {
        var sections = new List<ParallelFormSection>();
        
        // Get all top-level tables in the document
        var tables = body.ChildElements.OfType<Table>().ToList();
        _logger.LogInformation("Checking for parallel forms in {TableCount} top-level tables", tables.Count);
        
        // Check for identifying text patterns that indicate separate forms
        // IMPORTANT: Check "非單一" patterns FIRST to avoid false matches (since "單一" is a substring of "非單一")
        // The patterns are ordered from most specific to least specific
        var formIdentifiers = new[]
        {
            // "非單一" patterns MUST come first (繁體中文)
            ("非單一判給建議書涉及單一判給實體的情況", "非單一判給表格"),
            ("非單一判給建議書涉及單一判給實體", "非單一判給表格"),
            ("非單一判給建議書涉及", "非單一判給表格"),
            ("非單一判給建議書", "非單一判給表格"),
            // "非单一" patterns (简体中文)
            ("非单一判给建议书涉及单一判给实体的情况", "非單一判給表格"),
            ("非单一判给建议书涉及单一判给实体", "非單一判給表格"),
            ("非单一判给建议书", "非單一判給表格"),
            // Then "單一" patterns (繁體中文) - must come after 非單一
            ("單一判給建議書涉及單一判給實體的情況", "單一判給表格"),
            ("單一判給建議書涉及單一判給實體", "單一判給表格"),
            ("單一判給建議書涉及單一判給", "單一判給表格"),
            ("單一判給建議書", "單一判給表格"),
            // "单一" patterns (简体中文)
            ("单一判给建议书涉及单一判给实体的情况", "單一判給表格"),
            ("单一判给建议书涉及单一判给实体", "單一判給表格"),
            ("单一判给建议书", "單一判給表格"),
        };
        
        // First pass: scan entire document to find all form type markers
        var documentText = GetElementText(body);
        var detectedFormTypes = new HashSet<string>();
        
        foreach (var (identifier, formName) in formIdentifiers)
        {
            if (documentText.Contains(identifier) && !detectedFormTypes.Contains(formName))
            {
                detectedFormTypes.Add(formName);
                _logger.LogInformation("Document contains form type: '{FormName}' (matched pattern: '{Pattern}')", formName, identifier);
            }
        }
        
        _logger.LogInformation("Total distinct form types detected in document: {Count}", detectedFormTypes.Count);
        
        // If less than 2 form types detected, no parallel forms
        if (detectedFormTypes.Count < 2)
        {
            return sections;
        }
        
        // Group tables by their form type - now scan element by element
        var formGroups = new Dictionary<string, List<(OpenXmlElement Element, int Index)>>();
        
        // Initialize groups for all detected form types
        foreach (var formType in detectedFormTypes)
        {
            formGroups[formType] = new List<(OpenXmlElement, int)>();
        }
        
        var elementIndex = 0;
        foreach (var element in body.ChildElements)
        {
            // Get ALL text from element, including nested content (tables, cells, etc.)
            var text = GetElementText(element);
            
            // Try to match each form identifier
            string? matchedForm = null;
            foreach (var (identifier, formName) in formIdentifiers)
            {
                if (text.Contains(identifier))
                {
                    matchedForm = formName;
                    _logger.LogDebug("Found form identifier '{FormName}' in element {Index} (pattern: '{Pattern}')", formName, elementIndex, identifier);
                    break;
                }
            }
            
            if (matchedForm != null && formGroups.TryGetValue(matchedForm, out var matchedGroup))
            {
                matchedGroup.Add((element, elementIndex));
            }
            
            elementIndex++;
        }
        
        // Log results
        foreach (var (formName, elements) in formGroups)
        {
            _logger.LogInformation("Form group '{FormName}' has {Count} anchor elements", formName, elements.Count);
        }
        
        // Now we know we have multiple form types, assign elements
        var allElements = body.ChildElements.ToList();
        var elementAssignments = new string?[allElements.Count];
        
        // Mark identified elements
        foreach (var (formName, elements) in formGroups)
        {
            foreach (var (element, index) in elements)
            {
                elementAssignments[index] = formName;
            }
        }
        
        // Assign remaining elements based on content similarity or proximity
        AssignRemainingElementsToForms(allElements, elementAssignments, formGroups.Keys.ToList());
        
        // Group elements by form - ensure "單一" comes before "非單一" in output order
        var orderedFormNames = formGroups.Keys
            .OrderBy(k => k.Contains('非') ? 1 : 0) // "單一" first, "非單一" second
            .ToList();
        
        foreach (var formName in orderedFormNames)
        {
            var formElements = new List<OpenXmlElement>();
            for (int i = 0; i < allElements.Count; i++)
            {
                if (elementAssignments[i] == formName)
                {
                    formElements.Add(allElements[i]);
                }
            }
            
            if (formElements.Count > 0)
            {
                sections.Add(new ParallelFormSection(formName, formElements));
                _logger.LogInformation("Created parallel section '{FormName}' with {Count} elements", formName, formElements.Count);
            }
        }
        
        return sections;
    }

    /// <summary>
    /// Assigns unidentified elements to form groups based on content analysis
    /// </summary>
    private void AssignRemainingElementsToForms(
        List<OpenXmlElement> allElements, 
        string?[] assignments, 
        List<string> formNames)
    {
        // Strategy: Use position-based assignment with page-end markers
        // Each form section ends with "部門主管" and starts with "日期"
        
        var singleForm = formNames.FirstOrDefault(f => (f.Contains("單一") || f.Contains("单一")) && !f.Contains('非'));
        var nonSingleForm = formNames.FirstOrDefault(f => f.Contains('非'));
        
        if (singleForm == null || nonSingleForm == null)
        {
            _logger.LogWarning("Could not identify both form types. Single: {Single}, NonSingle: {NonSingle}", singleForm, nonSingleForm);
            return;
        }
        
        // Find the ORIGINAL anchor indices (marked during detection phase)
        int firstSingleAnchorIndex = -1;
        int firstNonSingleAnchorIndex = -1;
        for (int i = 0; i < assignments.Length; i++)
        {
            if (assignments[i] == singleForm && firstSingleAnchorIndex == -1)
            {
                firstSingleAnchorIndex = i;
            }
            if (assignments[i] == nonSingleForm && firstNonSingleAnchorIndex == -1)
            {
                firstNonSingleAnchorIndex = i;
            }
        }
        
        _logger.LogDebug("First single anchor index: {SingleIndex}, First non-single anchor index: {NonSingleIndex}, total elements: {Total}", 
            firstSingleAnchorIndex, firstNonSingleAnchorIndex, allElements.Count);
        
        // Find page-end markers ("部門主管") to determine form boundaries
        var pageEndIndices = new List<int>();
        for (int i = 0; i < allElements.Count; i++)
        {
            var text = GetElementText(allElements[i]);
            if (text.Contains("部門主管") || text.Contains("部门主管"))
            {
                pageEndIndices.Add(i);
                _logger.LogDebug("Found page-end marker at index {Index}", i);
            }
        }
        
        // Determine section boundary
        int sectionBoundary;
        
        if (pageEndIndices.Count >= 1 && firstNonSingleAnchorIndex >= 0)
        {
            // Find the page-end marker that is before the non-single anchor but closest to it
            // This marks the end of the single form section
            var pageEndBeforeNonSingle = pageEndIndices
                .Where(idx => idx < firstNonSingleAnchorIndex)
                .OrderByDescending(idx => idx)
                .FirstOrDefault(-1);
            
            if (pageEndBeforeNonSingle >= 0)
            {
                // The non-single form starts right after the page-end marker
                sectionBoundary = pageEndBeforeNonSingle + 1;
                _logger.LogInformation("Section boundary set after page-end marker at index {Index}", sectionBoundary);
            }
            else
            {
                // No page-end marker found before non-single anchor, use anchor position
                // But look back for "日期" to find the actual start
                sectionBoundary = FindSectionStartFromAnchor(allElements, firstNonSingleAnchorIndex, firstSingleAnchorIndex);
            }
        }
        else if (firstNonSingleAnchorIndex >= 0)
        {
            // No page-end markers, use traditional approach
            sectionBoundary = FindSectionStartFromAnchor(allElements, firstNonSingleAnchorIndex, firstSingleAnchorIndex);
        }
        else
        {
            // No non-single anchor found, assign all to single form
            for (int i = 0; i < assignments.Length; i++)
            {
                assignments[i] ??= singleForm;
            }
            return;
        }
        
        _logger.LogInformation("Final section boundary at index: {Index}", sectionBoundary);
        
        // Assign all elements based on section boundary
        for (int i = 0; i < assignments.Length; i++)
        {
            if (assignments[i] != null) continue;
            
            if (i >= sectionBoundary)
            {
                assignments[i] = nonSingleForm;
            }
            else
            {
                assignments[i] = singleForm;
            }
        }
    }
    
    /// <summary>
    /// Finds the start of a section by looking backwards from an anchor for header patterns
    /// </summary>
    private int FindSectionStartFromAnchor(List<OpenXmlElement> allElements, int anchorIndex, int minBoundary)
    {
        int sectionBoundary = anchorIndex;
        int effectiveMinBoundary = minBoundary >= 0 ? minBoundary + 1 : 0;
        
        // Look backwards from anchor to find the section start (typically "日期")
        // But stop if we hit a page-end marker
        for (int i = anchorIndex - 1; i >= effectiveMinBoundary; i--)
        {
            var text = GetElementText(allElements[i]);
            
            // Stop at page-end markers
            if (text.Contains("部門主管") || text.Contains("部门主管"))
            {
                sectionBoundary = i + 1;
                _logger.LogDebug("Stopped at page-end marker, boundary at {Index}", sectionBoundary);
                break;
            }
            
            // Update boundary if we find header patterns
            if ((text.Contains("日期") && text.Trim().Length < 30) ||
                (text.Contains('致') && text.Contains(':') && text.Trim().Length < 50) ||
                (text.Contains('由') && text.Contains(':') && text.Trim().Length < 50))
            {
                sectionBoundary = i;
                _logger.LogDebug("Found header pattern at index {Index}: {Text}", i, text.Substring(0, Math.Min(30, text.Length)));
            }
        }
        
        return sectionBoundary;
    }

    /// <summary>
    /// Counts the number of columns in a table
    /// </summary>
    private int CountTableColumns(Table table)
    {
        var firstRow = table.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableRow>().FirstOrDefault();
        return firstRow?.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableCell>().Count() ?? 0;
    }

    /// <summary>
    /// Splits document content by page breaks and section breaks
    /// </summary>
    private List<List<OpenXmlElement>> SplitContentByPageBreaks(Body body)
    {
        var sections = new List<List<OpenXmlElement>>();
        var currentSection = new List<OpenXmlElement>();

        foreach (var element in body.ChildElements)
        {
            // Check for page break in paragraph
            if (element is Paragraph para)
            {
                // Check for hard page break: <w:br w:type="page"/>
                var pageBreak = para.Descendants<Break>()
                    .FirstOrDefault(b => b.Type?.Value == BreakValues.Page);

                // Check for section break in paragraph properties
                var sectPr = para.ParagraphProperties?.SectionProperties;

                if (pageBreak != null || sectPr != null)
                {
                    // Add current paragraph to current section first
                    if (pageBreak == null) // Section break is at end of paragraph
                    {
                        currentSection.Add(element);
                    }

                    // Start new section if current has content
                    if (currentSection.Count > 0)
                    {
                        sections.Add(currentSection);
                        currentSection = new List<OpenXmlElement>();
                    }

                    // If page break, add the paragraph to the new section (minus the break)
                    if (pageBreak != null)
                    {
                        currentSection.Add(element);
                    }
                    continue;
                }
            }

            currentSection.Add(element);
        }

        // Add the last section
        if (currentSection.Count > 0)
        {
            sections.Add(currentSection);
        }

        return sections;
    }

    /// <summary>
    /// Extracts content elements from a list of OpenXml nodes
    /// </summary>
    private async Task<List<ContentElement>> ExtractContentElementsFromNodesAsync(
        List<OpenXmlElement> nodes,
        MainDocumentPart? mainPart,
        CancellationToken ct)
    {
        var elements = new List<ContentElement>();
        var elementId = 0;
        double currentTop = 0;

        foreach (var element in nodes)
        {
            ct.ThrowIfCancellationRequested();

            if (element is Paragraph para)
            {
                var imageElements = ExtractImagesFromParagraph(para, mainPart, ref elementId, ref currentTop);
                elements.AddRange(imageElements);

                var paragraphElement = ExtractParagraph(para, ref elementId, ref currentTop);
                if (paragraphElement != null)
                {
                    elements.Add(paragraphElement);
                }
            }
            else if (element is Table table)
            {
                var tableElement = ExtractTable(table, ref elementId, ref currentTop);
                if (tableElement != null)
                {
                    elements.Add(tableElement);
                }
            }
        }

        return elements;
    }

    private List<ImageElement> ExtractHeaderImages(MainDocumentPart mainPart)
    {
        var images = new List<ImageElement>();
        var elementId = 0;
        double currentTop = 10; // Start near top of page for header

        _logger.LogInformation("Checking {Count} header parts for images", mainPart.HeaderParts.Count());

        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header == null) 
            {
                _logger.LogDebug("Header part has null Header");
                continue;
            }

            // Log header content for debugging
            var headerXml = headerPart.Header.OuterXml;
            _logger.LogDebug("Header XML length: {Length}", headerXml.Length);

            // Try DrawingML images (modern format)
            var drawings = headerPart.Header.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().ToList();
            _logger.LogInformation("Found {Count} Drawing elements in header", drawings.Count);

            foreach (var drawing in drawings)
            {
                var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                if (blip?.Embed?.Value == null) 
                {
                    _logger.LogDebug("Drawing has no embedded blip");
                    continue;
                }

                try
                {
                    var imagePart = headerPart.GetPartById(blip.Embed.Value) as ImagePart;
                    if (imagePart == null) continue;

                    var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault();
                    double width = 100;
                    double height = 50;

                    if (extent != null)
                    {
                        if (extent.Cx?.HasValue == true)
                            width = extent.Cx.Value / 914400.0 * 72.0;
                        if (extent.Cy?.HasValue == true)
                            height = extent.Cy.Value / 914400.0 * 72.0;
                    }

                    byte[] imageData;
                    using (var stream = imagePart.GetStream())
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        imageData = memoryStream.ToArray();
                    }

                    var mimeType = imagePart.ContentType ?? "image/png";
                    var bounds = new BoundingBox(72, currentTop, width, height);

                    _logger.LogInformation("Extracted header image (Drawing): {Width}x{Height} points, {Size} bytes", 
                        width, height, imageData.Length);

                    images.Add(new ImageElement(
                        Id: $"header_img_{++elementId}",
                        Bounds: bounds,
                        ZIndex: elementId,
                        ImageData: imageData,
                        MimeType: mimeType,
                        AlternateText: "Header Logo"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract Drawing image from header");
                }
            }

            // Try VML/Picture images (legacy format)
            var pictures = headerPart.Header.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().ToList();
            _logger.LogInformation("Found {Count} VML ImageData elements in header", pictures.Count);

            foreach (var vmlImage in pictures)
            {
                if (vmlImage.RelationshipId?.Value == null) continue;

                try
                {
                    var imagePart = headerPart.GetPartById(vmlImage.RelationshipId.Value) as ImagePart;
                    if (imagePart == null) continue;

                    byte[] imageData;
                    using (var stream = imagePart.GetStream())
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        imageData = memoryStream.ToArray();
                    }

                    var mimeType = imagePart.ContentType ?? "image/png";
                    var bounds = new BoundingBox(72, currentTop, 150, 50); // Default size for VML

                    _logger.LogInformation("Extracted header image (VML): {Size} bytes", imageData.Length);

                    images.Add(new ImageElement(
                        Id: $"header_img_{++elementId}",
                        Bounds: bounds,
                        ZIndex: elementId,
                        ImageData: imageData,
                        MimeType: mimeType,
                        AlternateText: "Header Logo"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract VML image from header");
                }
            }

            // Also check for Blip elements directly in any namespace
            var allImageParts = headerPart.ImageParts.ToList();
            _logger.LogInformation("Header part has {Count} ImageParts total", allImageParts.Count);

            // If we haven't found images yet but ImageParts exist, try to get them directly
            if (images.Count == 0 && allImageParts.Count > 0)
            {
                foreach (var imagePart in allImageParts)
                {
                    try
                    {
                        byte[] imageData;
                        using (var stream = imagePart.GetStream())
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            imageData = memoryStream.ToArray();
                        }

                        var mimeType = imagePart.ContentType ?? "image/png";
                        var bounds = new BoundingBox(72, currentTop, 150, 50);

                        _logger.LogInformation("Extracted header image (Direct): {Size} bytes, MIME: {MimeType}", 
                            imageData.Length, mimeType);

                        images.Add(new ImageElement(
                            Id: $"header_img_{++elementId}",
                            Bounds: bounds,
                            ZIndex: elementId,
                            ImageData: imageData,
                            MimeType: mimeType,
                            AlternateText: "Header Logo"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract direct image from header");
                    }
                }
            }
        }

        _logger.LogInformation("Total header images extracted: {Count}", images.Count);
        return images;
    }

    private (double width, double height) GetPageDimensions(SectionProperties? sectionProps)
    {
        // Default to Letter size (8.5" x 11")
        double width = 612; // 8.5 * 72 points
        double height = 792; // 11 * 72 points

        if (sectionProps?.GetFirstChild<PageSize>() is { } pageSize)
        {
            if (pageSize.Width?.HasValue == true)
            {
                width = BoundingBoxCalculator.TwipsToPoints((int)pageSize.Width.Value);
            }
            if (pageSize.Height?.HasValue == true)
            {
                height = BoundingBoxCalculator.TwipsToPoints((int)pageSize.Height.Value);
            }
        }

        return (width, height);
    }

    private async Task<IReadOnlyList<ContentElement>> ExtractContentElementsAsync(
        Body body, 
        CancellationToken ct)
    {
        var elements = new List<ContentElement>();
        var elementId = 0;
        double currentTop = 0;

        // Get the MainDocumentPart for image extraction
        var mainPart = body.Ancestors<Document>().FirstOrDefault()?.MainDocumentPart;

        foreach (var element in body.ChildElements)
        {
            ct.ThrowIfCancellationRequested();

            if (element is Paragraph para)
            {
                // Check for images in the paragraph first
                var imageElements = ExtractImagesFromParagraph(para, mainPart, ref elementId, ref currentTop);
                elements.AddRange(imageElements);

                var paragraphElement = ExtractParagraph(para, ref elementId, ref currentTop);
                if (paragraphElement != null)
                {
                    elements.Add(paragraphElement);
                }
            }
            else if (element is Table table)
            {
                var tableElement = ExtractTable(table, ref elementId, ref currentTop);
                if (tableElement != null)
                {
                    elements.Add(tableElement);
                }
            }
        }

        return elements;
    }

    private List<ImageElement> ExtractImagesFromParagraph(
        Paragraph para,
        MainDocumentPart? mainPart,
        ref int elementId,
        ref double currentTop)
    {
        var images = new List<ImageElement>();
        if (mainPart == null) return images;

        foreach (var drawing in para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>())
        {
            var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
            if (blip?.Embed?.Value == null) continue;

            try
            {
                var imagePart = mainPart.GetPartById(blip.Embed.Value) as ImagePart;
                if (imagePart == null) continue;

                // Get image dimensions from the drawing
                var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault();
                double width = 100; // Default width in points
                double height = 100; // Default height in points

                if (extent != null)
                {
                    // EMUs to points: 1 inch = 914400 EMUs, 1 inch = 72 points
                    if (extent.Cx?.HasValue == true)
                    {
                        width = extent.Cx.Value / 914400.0 * 72.0;
                    }
                    if (extent.Cy?.HasValue == true)
                    {
                        height = extent.Cy.Value / 914400.0 * 72.0;
                    }
                }

                // Read image data
                byte[] imageData;
                using (var stream = imagePart.GetStream())
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Determine MIME type
                var mimeType = imagePart.ContentType ?? "image/png";

                var bounds = new BoundingBox(72, currentTop, width, height);
                currentTop += height + 10; // Add some spacing

                _logger.LogDebug("Extracted image: {Width}x{Height} pixels, {Size} bytes", 
                    width, height, imageData.Length);

                images.Add(new ImageElement(
                    Id: $"img_{++elementId}",
                    Bounds: bounds,
                    ZIndex: elementId,
                    ImageData: imageData,
                    MimeType: mimeType,
                    AlternateText: null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract image from paragraph");
            }
        }

        return images;
    }

    private ParagraphElement? ExtractParagraph(Paragraph para, ref int elementId, ref double currentTop)
    {
        var runs = new List<TextRun>();
        
        foreach (var run in para.Descendants<Run>())
        {
            var text = GetRunText(run);
            if (string.IsNullOrEmpty(text)) continue;

            var style = ExtractTextStyle(run);
            var fieldCode = ExtractFieldCodeFromRun(run);
            
            runs.Add(new TextRun(text, style, fieldCode));
        }

        if (runs.Count == 0) return null;

        var paraStyle = ExtractParagraphStyle(para);
        var height = 20.0; // Default line height in points
        
        var bounds = new BoundingBox(72, currentTop, 468, height); // Default margins
        currentTop += height + (paraStyle.SpaceAfter ?? 0);

        return new ParagraphElement(
            Id: $"para_{++elementId}",
            Bounds: bounds,
            ZIndex: elementId,
            Runs: runs,
            Style: paraStyle);
    }

    private string GetRunText(Run run)
    {
        var sb = new StringBuilder();
        
        foreach (var child in run.ChildElements)
        {
            if (child is Text text)
            {
                sb.Append(text.Text);
            }
            else if (child is Break br)
            {
                sb.Append(br.Type?.Value == BreakValues.Page ? "\f" : "\n");
            }
            else if (child is TabChar)
            {
                sb.Append('\t');
            }
        }
        
        return sb.ToString();
    }

    private TextStyle ExtractTextStyle(Run run)
    {
        var props = run.RunProperties;
        var text = GetRunText(run);
        var runFonts = props?.RunFonts;

        var fontFamily = ResolveFontFamily(runFonts, text);
        
        return new TextStyle(
            FontFamily: fontFamily,
            FontSize: props?.FontSize?.Val?.Value != null 
                ? double.Parse(props.FontSize.Val.Value) / 2 
                : 11,
            IsBold: props?.Bold != null,
            IsItalic: props?.Italic != null,
            IsUnderline: props?.Underline != null,
            Color: props?.Color?.Val?.Value,
            BackgroundColor: props?.Shading?.Fill?.Value,
            HorizontalAlignment: null,
            VerticalAlignment: null);
    }

    private static string ResolveFontFamily(RunFonts? runFonts, string text)
    {
        if (ContainsCjk(text))
        {
            return runFonts?.EastAsia?.Value
                ?? runFonts?.Ascii?.Value
                ?? runFonts?.HighAnsi?.Value
                ?? "Microsoft YaHei";
        }

        return runFonts?.Ascii?.Value
            ?? runFonts?.HighAnsi?.Value
            ?? runFonts?.EastAsia?.Value
            ?? "Calibri";
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            var code = (int)ch;
            if (code is
                (>= 0x4E00 and <= 0x9FFF) or
                (>= 0x3400 and <= 0x4DBF) or
                (>= 0xF900 and <= 0xFAFF) or
                (>= 0x3040 and <= 0x30FF) or
                (>= 0xAC00 and <= 0xD7AF))
            {
                return true;
            }
        }

        return false;
    }

    private ParagraphStyle ExtractParagraphStyle(Paragraph para)
    {
        var props = para.ParagraphProperties;
        var justVal = props?.Justification?.Val?.Value;
        
        string? alignment = null;
        if (justVal == JustificationValues.Left) alignment = "Left";
        else if (justVal == JustificationValues.Center) alignment = "Center";
        else if (justVal == JustificationValues.Right) alignment = "Right";
        else if (justVal == JustificationValues.Both) alignment = "Left"; // RDL does not support Justify, fallback to Left

        return new ParagraphStyle(
            LineSpacing: props?.SpacingBetweenLines?.Line?.Value != null 
                ? double.Parse(props.SpacingBetweenLines.Line.Value) / 240 
                : null,
            SpaceBefore: props?.SpacingBetweenLines?.Before?.Value != null 
                ? BoundingBoxCalculator.TwipsToPoints(int.Parse(props.SpacingBetweenLines.Before.Value)) 
                : null,
            SpaceAfter: props?.SpacingBetweenLines?.After?.Value != null 
                ? BoundingBoxCalculator.TwipsToPoints(int.Parse(props.SpacingBetweenLines.After.Value)) 
                : null,
            FirstLineIndent: props?.Indentation?.FirstLine?.Value != null 
                ? BoundingBoxCalculator.TwipsToPoints(int.Parse(props.Indentation.FirstLine.Value)) 
                : null,
            Alignment: alignment);
    }

    private AxiomFieldCode? ExtractFieldCodeFromRun(Run run)
    {
        var fieldChar = run.GetFirstChild<FieldChar>();
        if (fieldChar != null)
        {
            // This is part of a complex field - handled elsewhere
            return null;
        }

        var simpleField = run.Parent?.Parent as SimpleField;
        if (simpleField?.Instruction?.Value != null)
        {
            // This is part of a simple field
            var type = DetermineFieldType(simpleField.Instruction.Value);
            return new AxiomFieldCode(
                Guid.NewGuid().ToString(),
                type,
                simpleField.Instruction.Value,
                ExtractFieldName(simpleField.Instruction.Value),
                null,
                null);
        }

        return null;
    }

    private static FieldCodeType DetermineFieldType(string instruction)
    {
        var upper = instruction.ToUpperInvariant().TrimStart();
        
        if (upper.StartsWith("MERGEFIELD")) return FieldCodeType.MergeField;
        if (upper.StartsWith("IF")) return FieldCodeType.If;
        if (upper.StartsWith("DATE")) return FieldCodeType.Date;
        if (upper.StartsWith("PAGE")) return FieldCodeType.Page;
        if (upper.StartsWith("NUMPAGES")) return FieldCodeType.NumPages;
        
        return FieldCodeType.Unknown;
    }

    private static string? ExtractFieldName(string instruction)
    {
        var parts = instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : null;
    }

    private TableElement? ExtractTable(Table table, ref int elementId, ref double currentTop)
    {
        var rows = new List<AxiomTableRow>();
        var tableGrid = table.GetFirstChild<TableGrid>();
        var columnWidths = tableGrid?.Descendants<GridColumn>()
            .Select(gc => gc.Width?.Value != null 
                ? BoundingBoxCalculator.TwipsToPoints(int.Parse(gc.Width.Value)) 
                : 100.0)
            .ToList() ?? new List<double>();

        var rowIndex = 0;
        foreach (var tr in table.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
        {
            var cells = new List<AxiomTableCell>();
            var colIndex = 0;
            var rowHeight = 20.0;

            foreach (var tc in tr.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
            {
                var cellContent = new List<ContentElement>();
                var paraId = 0;
                var cellTop = 0.0;

                foreach (var para in tc.Descendants<Paragraph>())
                {
                    var paraElement = ExtractParagraph(para, ref paraId, ref cellTop);
                    if (paraElement != null)
                    {
                        cellContent.Add(paraElement);
                    }
                }

                var cellProps = tc.TableCellProperties;
                var cellWidth = cellProps?.TableCellWidth?.Width?.Value != null
                    ? BoundingBoxCalculator.TwipsToPoints(int.Parse(cellProps.TableCellWidth.Width.Value))
                    : (colIndex < columnWidths.Count ? columnWidths[colIndex] : 100.0);
                
                // Get column span from GridSpan property
                var colSpan = cellProps?.GridSpan?.Val?.Value ?? 1;

                cells.Add(new AxiomTableCell(
                    RowIndex: rowIndex,
                    ColumnIndex: colIndex,
                    RowSpan: 1, // TODO: Handle vertical merged cells
                    ColSpan: colSpan,
                    Width: cellWidth,
                    Content: cellContent,
                    Style: ExtractCellStyle(cellProps)));

                colIndex += colSpan;
            }

            rows.Add(new AxiomTableRow(rowIndex, rowHeight, cells.AsReadOnly()));
            rowIndex++;
        }

        if (rows.Count == 0) return null;

        var tableWidth = columnWidths.Sum();
        var tableHeight = rows.Sum(r => r.Height);
        var bounds = new BoundingBox(72, currentTop, tableWidth, tableHeight);
        currentTop += tableHeight + 10;

        return new TableElement(
            Id: $"table_{++elementId}",
            Bounds: bounds,
            ZIndex: elementId,
            RowCount: rows.Count,
            ColumnCount: rows.FirstOrDefault()?.Cells.Count ?? 0,
            Rows: rows.AsReadOnly());
    }

    private TableCellStyle? ExtractCellStyle(TableCellProperties? props)
    {
        if (props == null) return null;

        string? verticalAlignment = null;
        var vAlignValue = props.TableCellVerticalAlignment?.Val?.Value;
        if (vAlignValue != null)
        {
            if (vAlignValue.Equals(TableVerticalAlignmentValues.Top))
            {
                verticalAlignment = "Top";
            }
            else if (vAlignValue.Equals(TableVerticalAlignmentValues.Center))
            {
                verticalAlignment = "Center";
            }
            else if (vAlignValue.Equals(TableVerticalAlignmentValues.Bottom))
            {
                verticalAlignment = "Bottom";
            }
        }

        return new TableCellStyle(
            BackgroundColor: props.Shading?.Fill?.Value,
            TopBorder: ExtractBorder(props.TableCellBorders?.TopBorder),
            BottomBorder: ExtractBorder(props.TableCellBorders?.BottomBorder),
            LeftBorder: ExtractBorder(props.TableCellBorders?.LeftBorder),
            RightBorder: ExtractBorder(props.TableCellBorders?.RightBorder),
            VerticalAlignment: verticalAlignment,
            Padding: null);
    }

    private BorderStyle? ExtractBorder(BorderType? border)
    {
        if (border?.Val?.Value == BorderValues.Nil || border?.Val?.Value == BorderValues.None)
            return null;

        return new BorderStyle(
            Style: border?.Val?.Value.ToString() ?? "Solid",
            Width: border?.Size?.Value != null ? border.Size.Value / 8.0 : 1.0,
            Color: border?.Color?.Value);
    }

    private async Task<IReadOnlyList<LogicalElement>> IdentifyLogicalElementsAsync(
        MainDocumentPart mainPart, 
        CancellationToken ct)
    {
        var elements = new List<LogicalElement>();
        var elementId = 0;

        // Identify headers
        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header != null)
            {
                elements.Add(new LogicalElement(
                    Id: $"header_{++elementId}",
                    Role: LogicalRole.Header,
                    Bounds: new BoundingBox(0, 0, 612, 72),
                    Content: GetElementText(headerPart.Header),
                    Properties: null));
            }
        }

        // Identify footers
        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer != null)
            {
                elements.Add(new LogicalElement(
                    Id: $"footer_{++elementId}",
                    Role: LogicalRole.Footer,
                    Bounds: new BoundingBox(0, 720, 612, 72),
                    Content: GetElementText(footerPart.Footer),
                    Properties: null));
            }
        }

        return elements;
    }

    private string GetElementText(OpenXmlElement element)
    {
        var sb = new StringBuilder();
        foreach (var text in element.Descendants<Text>())
        {
            sb.Append(text.Text);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Represents a parallel form section (for side-by-side layouts)
/// </summary>
internal sealed record ParallelFormSection(string FormName, List<OpenXmlElement> Elements);

/// <summary>
/// Represents the Word document format type
/// </summary>
internal enum WordFormatType
{
    /// <summary>Unknown format</summary>
    Unknown = 0,
    
    /// <summary>Old binary .doc format (Word 97-2003)</summary>
    BinaryDoc = 1,
    
    /// <summary>New OpenXML .docx format (Word 2007+)</summary>
    OpenXmlDocx = 2
}
