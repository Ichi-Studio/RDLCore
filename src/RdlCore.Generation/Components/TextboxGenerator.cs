namespace RdlCore.Generation.Components;

/// <summary>
/// Generates Textbox elements for RDL reports
/// </summary>
public class TextboxGenerator
{
    private readonly ILogger<TextboxGenerator> _logger;
    private int _textboxCounter;

    public TextboxGenerator(ILogger<TextboxGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a Textbox element from a paragraph
    /// </summary>
    public XElement CreateTextbox(ParagraphElement paragraph, double left, double top)
    {
        var name = $"Textbox{++_textboxCounter}";
        var bounds = paragraph.Bounds.ToInches();
        var text = string.Join("", paragraph.Runs.Select(r => r.Text));

        _logger.LogDebug("Creating textbox '{Name}' at ({Left}, {Top})", name, left, top);

        return RdlNamespaces.RdlElement("Textbox",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("CanGrow", "true"),
            RdlNamespaces.RdlElement("KeepTogether", "true"),
            CreateParagraphs(paragraph),
            RdlNamespaces.RdElement("DefaultName", name),
            RdlNamespaces.RdlElement("Top", $"{top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{left:F5}in"),
            RdlNamespaces.RdlElement("Height", $"{bounds.Height:F5}in"),
            RdlNamespaces.RdlElement("Width", $"{bounds.Width:F5}in"),
            CreateStyle(paragraph.Style, paragraph.Runs.FirstOrDefault()?.Style)
        );
    }

    /// <summary>
    /// Creates a simple Textbox with text
    /// </summary>
    public XElement CreateSimpleTextbox(string name, string text, double left, double top, double width, double height, bool includePosition = true)
    {
        var sanitizedText = RdlNamespaces.SanitizeXmlString(text);
        var elements = new List<object>
        {
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("CanGrow", "true"),
            RdlNamespaces.RdlElement("KeepTogether", "true"),
            RdlNamespaces.RdlElement("Paragraphs",
                RdlNamespaces.RdlElement("Paragraph",
                    RdlNamespaces.RdlElement("TextRuns",
                        RdlNamespaces.RdlElement("TextRun",
                            RdlNamespaces.RdlElement("Value", sanitizedText),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdlElement("Style")
                )
            ),
            RdlNamespaces.RdElement("DefaultName", name)
        };

        // Only include position and size when not inside a TablixCell
        if (includePosition)
        {
            elements.Add(RdlNamespaces.RdlElement("Top", $"{top:F5}in"));
            elements.Add(RdlNamespaces.RdlElement("Left", $"{left:F5}in"));
            elements.Add(RdlNamespaces.RdlElement("Height", $"{height:F5}in"));
            elements.Add(RdlNamespaces.RdlElement("Width", $"{width:F5}in"));
        }

        elements.Add(RdlNamespaces.RdlElement("Style"));

        return RdlNamespaces.RdlElement("Textbox", elements.ToArray());
    }

    /// <summary>
    /// Creates a Textbox with an expression
    /// </summary>
    public XElement CreateExpressionTextbox(string name, string expression, double left, double top, double width, double height)
    {
        var sanitizedExpression = RdlNamespaces.SanitizeXmlString(expression);
        return RdlNamespaces.RdlElement("Textbox",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("CanGrow", "true"),
            RdlNamespaces.RdlElement("KeepTogether", "true"),
            RdlNamespaces.RdlElement("Paragraphs",
                RdlNamespaces.RdlElement("Paragraph",
                    RdlNamespaces.RdlElement("TextRuns",
                        RdlNamespaces.RdlElement("TextRun",
                            RdlNamespaces.RdlElement("Value", sanitizedExpression),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdlElement("Style")
                )
            ),
            RdlNamespaces.RdElement("DefaultName", name),
            RdlNamespaces.RdlElement("Top", $"{top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{left:F5}in"),
            RdlNamespaces.RdlElement("Height", $"{height:F5}in"),
            RdlNamespaces.RdlElement("Width", $"{width:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
    }

    private XElement CreateParagraphs(ParagraphElement paragraph)
    {
        var textRuns = paragraph.Runs.Select(run => 
            RdlNamespaces.RdlElement("TextRun",
                RdlNamespaces.RdlElement("Value", GetRunValue(run)),
                CreateTextRunStyle(run.Style)
            )).ToArray();

        return RdlNamespaces.RdlElement("Paragraphs",
            RdlNamespaces.RdlElement("Paragraph",
                RdlNamespaces.RdlElement("TextRuns", textRuns),
                CreateParagraphStyle(paragraph.Style)
            )
        );
    }

    private string GetRunValue(TextRun run)
    {
        if (run.FieldCode != null)
        {
            // Convert field code to expression
            return $"=Fields!{run.FieldCode.FieldName ?? "Unknown"}.Value";
        }
        return RdlNamespaces.SanitizeXmlString(run.Text);
    }

    private XElement CreateStyle(ParagraphStyle? paraStyle, TextStyle? textStyle)
    {
        var styleElements = new List<object>();

        if (textStyle != null)
        {
            if (!string.IsNullOrEmpty(textStyle.FontFamily))
            {
                styleElements.Add(RdlNamespaces.RdlElement("FontFamily", textStyle.FontFamily));
            }
            
            styleElements.Add(RdlNamespaces.RdlElement("FontSize", $"{textStyle.FontSize}pt"));

            if (textStyle.IsBold)
            {
                styleElements.Add(RdlNamespaces.RdlElement("FontWeight", "Bold"));
            }

            if (textStyle.IsItalic)
            {
                styleElements.Add(RdlNamespaces.RdlElement("FontStyle", "Italic"));
            }

            if (textStyle.IsUnderline)
            {
                styleElements.Add(RdlNamespaces.RdlElement("TextDecoration", "Underline"));
            }

            if (!string.IsNullOrEmpty(textStyle.Color))
            {
                styleElements.Add(RdlNamespaces.RdlElement("Color", FormatColor(textStyle.Color)));
            }
        }

        if (paraStyle?.Alignment != null)
        {
            styleElements.Add(RdlNamespaces.RdlElement("TextAlign", paraStyle.Alignment));
        }

        return RdlNamespaces.RdlElement("Style", styleElements.ToArray());
    }

    private XElement CreateTextRunStyle(TextStyle style)
    {
        var elements = new List<object>();

        if (!string.IsNullOrEmpty(style.FontFamily))
        {
            elements.Add(RdlNamespaces.RdlElement("FontFamily", style.FontFamily));
        }

        elements.Add(RdlNamespaces.RdlElement("FontSize", $"{style.FontSize}pt"));

        if (style.IsBold)
        {
            elements.Add(RdlNamespaces.RdlElement("FontWeight", "Bold"));
        }

        if (style.IsItalic)
        {
            elements.Add(RdlNamespaces.RdlElement("FontStyle", "Italic"));
        }

        if (style.IsUnderline)
        {
            elements.Add(RdlNamespaces.RdlElement("TextDecoration", "Underline"));
        }

        if (!string.IsNullOrEmpty(style.Color))
        {
            elements.Add(RdlNamespaces.RdlElement("Color", FormatColor(style.Color)));
        }

        return RdlNamespaces.RdlElement("Style", elements.ToArray());
    }

    private XElement CreateParagraphStyle(ParagraphStyle style)
    {
        var elements = new List<object>();

        if (style.Alignment != null)
        {
            elements.Add(RdlNamespaces.RdlElement("TextAlign", style.Alignment));
        }

        if (style.LineSpacing.HasValue)
        {
            elements.Add(RdlNamespaces.RdlElement("LineHeight", $"{style.LineSpacing}pt"));
        }

        return RdlNamespaces.RdlElement("Style", elements.ToArray());
    }

    private static string FormatColor(string color)
    {
        // Ensure color is in proper format (#RRGGBB)
        if (color.StartsWith("#"))
        {
            return color;
        }
        return $"#{color}";
    }
}
