using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RdlCore.Parsing.Word;

/// <summary>
/// Provides navigation utilities for OpenXML documents
/// </summary>
public class OpenXmlNavigator
{
    private readonly ILogger<OpenXmlNavigator> _logger;

    public OpenXmlNavigator(ILogger<OpenXmlNavigator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all paragraphs from the document body
    /// </summary>
    public IEnumerable<Paragraph> GetBodyParagraphs(WordprocessingDocument document)
    {
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            yield break;
        }

        foreach (var para in body.Elements<Paragraph>())
        {
            yield return para;
        }
    }

    /// <summary>
    /// Gets all tables from the document body
    /// </summary>
    public IEnumerable<Table> GetBodyTables(WordprocessingDocument document)
    {
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            yield break;
        }

        foreach (var table in body.Descendants<Table>())
        {
            yield return table;
        }
    }

    /// <summary>
    /// Gets header paragraphs
    /// </summary>
    public IEnumerable<Paragraph> GetHeaderParagraphs(WordprocessingDocument document)
    {
        var mainPart = document.MainDocumentPart;
        if (mainPart == null)
        {
            yield break;
        }

        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header == null) continue;

            foreach (var para in headerPart.Header.Descendants<Paragraph>())
            {
                yield return para;
            }
        }
    }

    /// <summary>
    /// Gets footer paragraphs
    /// </summary>
    public IEnumerable<Paragraph> GetFooterParagraphs(WordprocessingDocument document)
    {
        var mainPart = document.MainDocumentPart;
        if (mainPart == null)
        {
            yield break;
        }

        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer == null) continue;

            foreach (var para in footerPart.Footer.Descendants<Paragraph>())
            {
                yield return para;
            }
        }
    }

    /// <summary>
    /// Gets all images from the document
    /// </summary>
    public IEnumerable<(Drawing drawing, ImagePart imagePart)> GetImages(WordprocessingDocument document)
    {
        var mainPart = document.MainDocumentPart;
        if (mainPart == null)
        {
            yield break;
        }

        foreach (var drawing in mainPart.Document?.Descendants<Drawing>() ?? Enumerable.Empty<Drawing>())
        {
            var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
            if (blip?.Embed?.Value == null) continue;

            var imagePart = mainPart.GetPartById(blip.Embed.Value) as ImagePart;
            if (imagePart != null)
            {
                yield return (drawing, imagePart);
            }
        }
    }

    /// <summary>
    /// Gets section properties for page layout information
    /// </summary>
    public IEnumerable<SectionProperties> GetSectionProperties(WordprocessingDocument document)
    {
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            yield break;
        }

        // Section properties in paragraphs
        foreach (var para in body.Descendants<Paragraph>())
        {
            var sectPr = para.ParagraphProperties?.SectionProperties;
            if (sectPr != null)
            {
                yield return sectPr;
            }
        }

        // Final section properties
        var finalSectPr = body.GetFirstChild<SectionProperties>();
        if (finalSectPr != null)
        {
            yield return finalSectPr;
        }
    }

    /// <summary>
    /// Gets the style definition for a paragraph
    /// </summary>
    public Style? GetParagraphStyle(WordprocessingDocument document, Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrEmpty(styleId))
        {
            return null;
        }

        var stylesPart = document.MainDocumentPart?.StyleDefinitionsPart;
        return stylesPart?.Styles?.Descendants<Style>()
            .FirstOrDefault(s => s.StyleId?.Value == styleId);
    }

    /// <summary>
    /// Resolves inherited styles for a paragraph
    /// </summary>
    public ParagraphProperties? GetEffectiveParagraphProperties(
        WordprocessingDocument document, 
        Paragraph paragraph)
    {
        var props = paragraph.ParagraphProperties?.CloneNode(true) as ParagraphProperties 
            ?? new ParagraphProperties();

        var style = GetParagraphStyle(document, paragraph);
        while (style != null)
        {
            MergeStyleProperties(props, style.StyleParagraphProperties);
            
            var basedOn = style.BasedOn?.Val?.Value;
            if (string.IsNullOrEmpty(basedOn)) break;
            
            style = document.MainDocumentPart?.StyleDefinitionsPart?.Styles?
                .Descendants<Style>()
                .FirstOrDefault(s => s.StyleId?.Value == basedOn);
        }

        return props;
    }

    private void MergeStyleProperties(
        ParagraphProperties target, 
        StyleParagraphProperties? source)
    {
        if (source == null) return;

        // Merge justification if not set
        if (target.Justification == null && source.Justification != null)
        {
            target.Justification = (Justification)source.Justification.CloneNode(true);
        }

        // Merge spacing if not set
        if (target.SpacingBetweenLines == null && source.SpacingBetweenLines != null)
        {
            target.SpacingBetweenLines = (SpacingBetweenLines)source.SpacingBetweenLines.CloneNode(true);
        }

        // Merge indentation if not set
        if (target.Indentation == null && source.Indentation != null)
        {
            target.Indentation = (Indentation)source.Indentation.CloneNode(true);
        }
    }
}
