namespace RdlCore.Generation.Components;

/// <summary>
/// Generates Textbox elements for RDL reports
/// </summary>
public class TextboxGenerator
{
    private readonly ILogger<TextboxGenerator> _logger;
    private readonly AxiomRdlCoreOptions _options;
    private int _textboxCounter;

    public TextboxGenerator(ILogger<TextboxGenerator> logger, IOptions<AxiomRdlCoreOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Creates a Textbox element from a paragraph
    /// </summary>
    public XElement CreateTextbox(ParagraphElement paragraph, double left, double top)
    {
        var name = $"Textbox{++_textboxCounter}";
        var bounds = paragraph.Bounds.ToInches();
        var text = paragraph.Runs != null 
            ? string.Join("", paragraph.Runs.Where(r => r != null).Select(r => r.Text ?? string.Empty))
            : string.Empty;

        _logger.LogDebug("Creating textbox '{Name}' at ({Left}, {Top})", name, left, top);

        // Get first run style safely
        var firstRunStyle = paragraph.Runs?.FirstOrDefault()?.Style;

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
            CreateStyle(paragraph.Style, firstRunStyle)
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
        // Safely handle null or empty runs
        if (paragraph.Runs == null || !paragraph.Runs.Any())
        {
            return RdlNamespaces.RdlElement("Paragraphs",
                RdlNamespaces.RdlElement("Paragraph",
                    RdlNamespaces.RdlElement("TextRuns",
                        RdlNamespaces.RdlElement("TextRun",
                            RdlNamespaces.RdlElement("Value", string.Empty),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    CreateParagraphStyle(paragraph.Style)
                )
            );
        }

        var textRuns = paragraph.Runs
            .Where(run => run != null)
            .Select(run => 
                RdlNamespaces.RdlElement("TextRun",
                    RdlNamespaces.RdlElement("Value", GetRunValue(run)),
                    CreateTextRunStyle(run.Style)
                )).ToArray();

        // Ensure at least one TextRun exists
        if (textRuns.Length == 0)
        {
            textRuns = [RdlNamespaces.RdlElement("TextRun",
                RdlNamespaces.RdlElement("Value", string.Empty),
                RdlNamespaces.RdlElement("Style")
            )];
        }

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
                styleElements.Add(RdlNamespaces.RdlElement("FontFamily", NormalizeFontFamily(textStyle.FontFamily)));
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
            elements.Add(RdlNamespaces.RdlElement("FontFamily", NormalizeFontFamily(style.FontFamily)));
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
    
    /// <summary>
    /// Normalizes font family names to ensure they render correctly in RDLC.
    /// RDLC only supports single font names, not CSS-style font fallback lists.
    /// </summary>
    private string NormalizeFontFamily(string? fontFamily)
    {
        if (string.IsNullOrEmpty(fontFamily))
            return "Microsoft YaHei";
        
        // RDLC requires a single font name, not a comma-separated list
        // Map legacy or uncommon fonts to widely available alternatives
        return fontFamily switch
        {
            // DFKai-SB (標楷體) - Traditional Chinese calligraphy font, may not be available
            "DFKai-SB" or "標楷體" => "KaiTi",
            // MingLiU variants - map to SimSun which is more widely available
            "MingLiU" or "PMingLiU" or "MingLiU_HKSCS" => "SimSun",
            // SimSun variants
            "SimSun" or "NSimSun" or "宋体" => "SimSun",
            // Other common Chinese fonts
            "SimHei" or "黑体" => "SimHei",
            "KaiTi" or "楷体" => "KaiTi",
            "FangSong" or "仿宋" => "FangSong",
            // Microsoft YaHei is the most reliable Chinese font on Windows
            "Microsoft YaHei" or "微软雅黑" => "Microsoft YaHei",
            // Western fonts - keep single font name
            "Calibri" => "Calibri",
            "Arial" => "Arial",
            "Times New Roman" => "Times New Roman",
            // Default: keep original font name (single value only)
            _ => fontFamily.Contains(',') ? fontFamily.Split(',')[0].Trim() : fontFamily
        };
    }
}
