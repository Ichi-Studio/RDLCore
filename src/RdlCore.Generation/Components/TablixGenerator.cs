namespace RdlCore.Generation.Components;

/// <summary>
/// Generates Tablix elements for RDL reports
/// </summary>
public class TablixGenerator(
    ILogger<TablixGenerator> logger,
    TextboxGenerator textboxGenerator,
    IOptions<AxiomRdlCoreOptions> options)
{
    private int _tablixCounter;
    private readonly AxiomRdlCoreOptions _options = options.Value;

    /// <summary>
    /// Creates a Tablix element from a table
    /// </summary>
    public XElement CreateTablix(TableElement table, DataSetBinding? binding, double left, double top)
    {
        var tablixId = ++_tablixCounter;
        var name = $"Tablix{tablixId}";
        var bounds = table.Bounds.ToInches();

        logger.LogInformation("Creating Tablix '{Name}' with {Rows}x{Cols} dimensions", 
            name, table.RowCount, table.ColumnCount);

        // Get consistent column and row counts
        var columnCount = table.Rows.Count > 0 ? table.Rows[0].Cells.Count : 0;
        var rowCount = table.Rows.Count;

        return RdlNamespaces.RdlElement("Tablix",
            new XAttribute("Name", name),
            CreateTablixBody(table, tablixId, columnCount),
            CreateTablixColumnHierarchy(columnCount),
            CreateTablixRowHierarchy(rowCount),
            binding != null ? RdlNamespaces.RdlElement("DataSetName", binding.DataSetName) : null,
            RdlNamespaces.RdlElement("Top", $"{top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{left:F5}in"),
            RdlNamespaces.RdlElement("Height", $"{bounds.Height:F5}in"),
            RdlNamespaces.RdlElement("Width", $"{bounds.Width:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
    }

    /// <summary>
    /// Creates a Tablix from a table structure with dataset binding
    /// </summary>
    public XElement CreateTablixFromStructure(TableStructure structure, DataSetBinding binding, double left, double top)
    {
        var name = $"Tablix{++_tablixCounter}";

        logger.LogInformation("Creating Tablix '{Name}' from structure with binding to '{DataSet}'",
            name, binding.DataSetName);

        var tablixColumns = CreateTablixColumns(structure.ColumnWidths);
        var tablixRows = CreateTablixRows(structure, binding);

        return RdlNamespaces.RdlElement("Tablix",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("TablixBody",
                tablixColumns,
                RdlNamespaces.RdlElement("TablixRows", tablixRows)
            ),
            CreateTablixColumnHierarchyFromStructure(structure),
            CreateTablixRowHierarchyFromStructure(structure, binding),
            RdlNamespaces.RdlElement("DataSetName", binding.DataSetName),
            RdlNamespaces.RdlElement("Top", $"{top:F5}in"),
            RdlNamespaces.RdlElement("Left", $"{left:F5}in"),
            RdlNamespaces.RdlElement("Style")
        );
    }

    private XElement CreateTablixBody(TableElement table, int tablixId, int expectedColumnCount)
    {
        var columns = CreateTablixColumnsFromTable(table, expectedColumnCount);
        var rows = CreateTablixRowsFromGrid(table, tablixId, expectedColumnCount);

        return RdlNamespaces.RdlElement("TablixBody",
            columns,
            RdlNamespaces.RdlElement("TablixRows", rows)
        );
    }

    private XElement[] CreateTablixRowsFromGrid(TableElement table, int tablixId, int expectedColumnCount)
    {
        var rowCount = table.Rows.Count;
        if (rowCount == 0 || expectedColumnCount == 0)
        {
            return Array.Empty<XElement>();
        }

        var starts = new TableCell?[rowCount, expectedColumnCount];
        var covered = new bool[rowCount, expectedColumnCount];

        foreach (var row in table.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell == null) continue;

                var startRow = cell.RowIndex;
                var startCol = cell.ColumnIndex;

                if (startRow < 0 || startRow >= rowCount || startCol < 0 || startCol >= expectedColumnCount)
                {
                    continue;
                }

                starts[startRow, startCol] = cell;

                var rowSpan = Math.Max(1, cell.RowSpan);
                var colSpan = Math.Max(1, cell.ColSpan);

                var maxRow = Math.Min(rowCount, startRow + rowSpan);
                var maxCol = Math.Min(expectedColumnCount, startCol + colSpan);

                for (var r = startRow; r < maxRow; r++)
                {
                    for (var c = startCol; c < maxCol; c++)
                    {
                        if (r == startRow && c == startCol) continue;
                        covered[r, c] = true;
                    }
                }
            }
        }

        var rows = new List<XElement>(rowCount);
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var cells = new List<XElement>(expectedColumnCount);

            for (var colPos = 0; colPos < expectedColumnCount; colPos++)
            {
                if (covered[rowIndex, colPos])
                {
                    cells.Add(RdlNamespaces.RdlElement("TablixCell"));
                    continue;
                }

                var cell = starts[rowIndex, colPos];
                if (cell == null)
                {
                    cells.Add(CreateEmptyTablixCell(tablixId, rowIndex, colPos));
                    continue;
                }

                var effectiveColSpan = Math.Min(Math.Max(1, cell.ColSpan), expectedColumnCount - colPos);
                var effectiveRowSpan = Math.Min(Math.Max(1, cell.RowSpan), rowCount - rowIndex);

                cells.Add(CreateTablixCellWithSpan(cell, tablixId, effectiveColSpan, effectiveRowSpan));

                for (var span = 1; span < effectiveColSpan; span++)
                {
                    cells.Add(RdlNamespaces.RdlElement("TablixCell"));
                    colPos++;
                }
            }

            rows.Add(RdlNamespaces.RdlElement("TablixRow",
                RdlNamespaces.RdlElement("Height", $"{table.Rows[rowIndex].Height / 72.0:F5}in"),
                RdlNamespaces.RdlElement("TablixCells", cells.ToArray())
            ));
        }

        return rows.ToArray();
    }

    private XElement CreateTablixColumnsFromTable(TableElement table, int expectedColumnCount)
    {
        if (table.Rows.Count == 0 || expectedColumnCount == 0)
        {
            // Return empty TablixColumns with at least one default column to avoid invalid RDLC
            if (expectedColumnCount == 0 && table.Rows.Count > 0)
            {
                return RdlNamespaces.RdlElement("TablixColumns",
                    RdlNamespaces.RdlElement("TablixColumn",
                        RdlNamespaces.RdlElement("Width", "1.00000in")
                    ));
            }
            return RdlNamespaces.RdlElement("TablixColumns");
        }

        var firstRow = table.Rows[0];
        var columns = new List<XElement>();
        
        for (int i = 0; i < expectedColumnCount; i++)
        {
            // Safely access cell width with bounds checking
            double width = 1.0; // Default width
            if (i < firstRow.Cells.Count && firstRow.Cells[i] != null)
            {
                width = firstRow.Cells[i].Width / 72.0;
                // Ensure minimum width to prevent rendering issues
                if (width < 0.1) width = 0.5;
            }
            columns.Add(RdlNamespaces.RdlElement("TablixColumn",
                RdlNamespaces.RdlElement("Width", $"{width:F5}in")
            ));
        }

        return RdlNamespaces.RdlElement("TablixColumns", columns.ToArray());
    }

    private XElement CreateTablixColumns(IReadOnlyList<double> widths)
    {
        var columns = widths.Select(w =>
            RdlNamespaces.RdlElement("TablixColumn",
                RdlNamespaces.RdlElement("Width", $"{w / 72.0:F5}in")
            )).ToArray();

        return RdlNamespaces.RdlElement("TablixColumns", columns);
    }

    private XElement CreateTablixRow(Abstractions.Models.TableRow row, int tablixId, int expectedColumnCount)
    {
        var cells = new List<XElement>();
        var cellIndex = 0;
        
        // Process each cell, accounting for ColSpan
        for (int colPos = 0; colPos < expectedColumnCount; colPos++)
        {
            if (cellIndex < row.Cells.Count)
            {
                var cell = row.Cells[cellIndex];
                
                // Check if this is the position where the cell starts
                if (cell.ColumnIndex == colPos)
                {
                    // Clamp ColSpan to not exceed the remaining columns
                    var effectiveColSpan = Math.Min(cell.ColSpan, expectedColumnCount - colPos);
                    
                    // Create the cell with potentially adjusted ColSpan
                    if (effectiveColSpan != cell.ColSpan)
                    {
                        logger.LogWarning("Adjusted ColSpan for cell at row {Row}, col {Col} from {Original} to {Effective}",
                            cell.RowIndex, cell.ColumnIndex, cell.ColSpan, effectiveColSpan);
                    }
                    
                    cells.Add(CreateTablixCellWithColSpan(cell, tablixId, effectiveColSpan));
                    
                    // Add empty TablixCell for merged columns (use <= to include the last position)
                    for (int span = 1; span < effectiveColSpan; span++)
                    {
                        cells.Add(RdlNamespaces.RdlElement("TablixCell"));
                        colPos++;
                    }
                    
                    cellIndex++;
                }
                else
                {
                    // Fill gap with empty cell
                    cells.Add(CreateEmptyTablixCell(tablixId, row.Cells.Count > 0 ? row.Cells[0].RowIndex : 0, colPos));
                }
            }
            else
            {
                // Add empty cell if row has fewer cells than expected
                cells.Add(CreateEmptyTablixCell(tablixId, row.Cells.Count > 0 ? row.Cells[0].RowIndex : 0, colPos));
            }
        }

        return RdlNamespaces.RdlElement("TablixRow",
            RdlNamespaces.RdlElement("Height", $"{row.Height / 72.0:F5}in"),
            RdlNamespaces.RdlElement("TablixCells", cells.ToArray())
        );
    }
    
    /// <summary>
    /// Creates a TablixCell with explicit ColSpan value (may differ from original cell.ColSpan if clamped)
    /// </summary>
    private XElement CreateTablixCellWithColSpan(TableCell cell, int tablixId, int effectiveColSpan)
    {
        return CreateTablixCellWithSpan(cell, tablixId, effectiveColSpan, 1);
    }

    private XElement CreateTablixCellWithSpan(TableCell cell, int tablixId, int effectiveColSpan, int effectiveRowSpan)
    {
        var content = GetCellContent(cell);
        var uniqueName = $"Tablix{tablixId}_Cell_{cell.RowIndex}_{cell.ColumnIndex}";
        var textAlign = DetermineTextAlignment(content, cell);
        var textStyle = ExtractTextStyle(cell);

        var cellContents = new List<object>
        {
            CreateStyledTextbox(
                uniqueName,
                content,
                cell.Width / 72.0,
                textAlign,
                cell.Style,
                textStyle)
        };

        if (effectiveColSpan > 1)
        {
            cellContents.Add(RdlNamespaces.RdlElement("ColSpan", effectiveColSpan.ToString()));
        }

        if (effectiveRowSpan > 1)
        {
            cellContents.Add(RdlNamespaces.RdlElement("RowSpan", effectiveRowSpan.ToString()));
        }

        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents", cellContents.ToArray())
        );
    }

    private XElement CreateEmptyTablixCell(int tablixId, int rowIndex, int colIndex)
    {
        var uniqueName = $"Tablix{tablixId}_Cell_{rowIndex}_{colIndex}";
        
        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents",
                textboxGenerator.CreateSimpleTextbox(
                    uniqueName,
                    string.Empty,
                    0, 0, 1.0, 0.25,
                    includePosition: false)
            )
        );
    }

    private XElement[] CreateTablixRows(TableStructure structure, DataSetBinding binding)
    {
        var rows = new List<XElement>();
        var fieldIndex = 0;

        // Header row if present
        if (structure.HasHeaderRow)
        {
            var headerCells = binding.Fields
                .Take(structure.ColumnCount)
                .Select(f => CreateHeaderCell(f.DisplayName ?? f.Name))
                .ToArray();

            rows.Add(RdlNamespaces.RdlElement("TablixRow",
                RdlNamespaces.RdlElement("Height", "0.25in"),
                RdlNamespaces.RdlElement("TablixCells", headerCells)
            ));
        }

        // Data row
        var dataCells = binding.Fields
            .Take(structure.ColumnCount)
            .Select(f => CreateDataCell(f.Name))
            .ToArray();

        rows.Add(RdlNamespaces.RdlElement("TablixRow",
            RdlNamespaces.RdlElement("Height", "0.25in"),
            RdlNamespaces.RdlElement("TablixCells", dataCells)
        ));

        return [.. rows];
    }

    /// <summary>
    /// Extracts text style from cell content, checking all runs for styling
    /// </summary>
    private TextStyle? ExtractTextStyle(TableCell cell)
    {
        // Guard against null or empty content
        if (cell.Content == null || cell.Content.Count == 0)
        {
            return null;
        }
        
        TextStyle? firstStyle = null;
        bool hasUnderline = false;
        bool hasBold = false;
        bool hasItalic = false;
        
        foreach (var element in cell.Content)
        {
            if (element is ParagraphElement para && para.Runs != null)
            {
                foreach (var run in para.Runs)
                {
                    if (run == null) continue;
                    
                    if (firstStyle == null && run.Style != null)
                    {
                        firstStyle = run.Style;
                    }
                    if (run.Style?.IsUnderline == true)
                    {
                        hasUnderline = true;
                    }
                    if (run.Style?.IsBold == true)
                    {
                        hasBold = true;
                    }
                    if (run.Style?.IsItalic == true)
                    {
                        hasItalic = true;
                    }
                }
            }
        }
        
        if (firstStyle == null)
            return null;
            
        // Return style with merged underline/bold/italic from any run
        return firstStyle with 
        { 
            IsUnderline = hasUnderline || firstStyle.IsUnderline,
            IsBold = hasBold || firstStyle.IsBold,
            IsItalic = hasItalic || firstStyle.IsItalic
        };
    }
    
    /// <summary>
    /// Determines the text alignment based on content and cell style
    /// </summary>
    private string? DetermineTextAlignment(string content, TableCell cell)
    {
        // First, check if the cell has an explicit alignment from the original document
        if (cell.Style?.VerticalAlignment != null)
        {
            // Check if any paragraph inside has explicit alignment
            foreach (var element in cell.Content)
            {
                if (element is ParagraphElement para && para.Style?.Alignment != null)
                {
                    return para.Style.Alignment;
                }
            }
        }
        
        // Special handling for date-related content - should be right-aligned
        var trimmedContent = content.Trim();
        if (trimmedContent.Contains("日期") && !trimmedContent.Contains('(') && trimmedContent.Length < 20)
        {
            logger.LogDebug("Applying right alignment to date content: {Content}", trimmedContent);
            return "Right";
        }
        
        return null; // No special alignment
    }
    
    /// <summary>
    /// Creates a textbox with style including alignment and text decoration
    /// </summary>
    private XElement CreateStyledTextbox(string name, string content, double width, string? textAlign, TableCellStyle? cellStyle, TextStyle? textStyle = null)
    {
        var sanitizedContent = RdlNamespaces.SanitizeXmlString(content);
        
        var paragraphStyleElements = new List<object>();
        if (!string.IsNullOrEmpty(textAlign))
        {
            paragraphStyleElements.Add(RdlNamespaces.RdlElement("TextAlign", textAlign));
        }
        
        var textboxStyleElements = new List<object>();
        if (cellStyle != null)
        {
            if (!string.IsNullOrEmpty(cellStyle.BackgroundColor))
            {
                textboxStyleElements.Add(RdlNamespaces.RdlElement("BackgroundColor", cellStyle.BackgroundColor));
            }

            if (!string.IsNullOrWhiteSpace(cellStyle.VerticalAlignment))
            {
                var align = cellStyle.VerticalAlignment.Trim();
                var normalized = align.Equals("Center", StringComparison.OrdinalIgnoreCase)
                    ? "Middle"
                    : align.Equals("Top", StringComparison.OrdinalIgnoreCase) ? "Top"
                    : align.Equals("Bottom", StringComparison.OrdinalIgnoreCase) ? "Bottom"
                    : align;

                if (normalized is "Top" or "Middle" or "Bottom")
                {
                    textboxStyleElements.Add(RdlNamespaces.RdlElement("VerticalAlign", normalized));
                }
            }
            
            // Apply cell borders
            if (cellStyle.TopBorder != null)
            {
                textboxStyleElements.Add(CreateBorderElement("TopBorder", cellStyle.TopBorder));
            }
            if (cellStyle.BottomBorder != null)
            {
                textboxStyleElements.Add(CreateBorderElement("BottomBorder", cellStyle.BottomBorder));
            }
            if (cellStyle.LeftBorder != null)
            {
                textboxStyleElements.Add(CreateBorderElement("LeftBorder", cellStyle.LeftBorder));
            }
            if (cellStyle.RightBorder != null)
            {
                textboxStyleElements.Add(CreateBorderElement("RightBorder", cellStyle.RightBorder));
            }
        }
        
        // Build TextRun style elements
        var textRunStyleElements = new List<object>();
        if (textStyle != null)
        {
            if (textStyle.IsBold)
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("FontWeight", "Bold"));
            }
            if (textStyle.IsItalic)
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("FontStyle", "Italic"));
            }
            if (textStyle.IsUnderline)
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("TextDecoration", "Underline"));
            }
            if (!string.IsNullOrEmpty(textStyle.FontFamily))
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("FontFamily", NormalizeFontFamily(textStyle.FontFamily)));
            }
            if (textStyle.FontSize > 0)
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("FontSize", $"{textStyle.FontSize}pt"));
            }
            if (!string.IsNullOrEmpty(textStyle.Color))
            {
                textRunStyleElements.Add(RdlNamespaces.RdlElement("Color", textStyle.Color));
            }
        }
        
        return RdlNamespaces.RdlElement("Textbox",
            new XAttribute("Name", name),
            RdlNamespaces.RdlElement("CanGrow", "true"),
            RdlNamespaces.RdlElement("KeepTogether", "true"),
            RdlNamespaces.RdlElement("Paragraphs",
                RdlNamespaces.RdlElement("Paragraph",
                    RdlNamespaces.RdlElement("TextRuns",
                        RdlNamespaces.RdlElement("TextRun",
                            RdlNamespaces.RdlElement("Value", sanitizedContent),
                            RdlNamespaces.RdlElement("Style", textRunStyleElements.ToArray())
                        )
                    ),
                    RdlNamespaces.RdlElement("Style", paragraphStyleElements.ToArray())
                )
            ),
            RdlNamespaces.RdElement("DefaultName", name),
            RdlNamespaces.RdlElement("Style", textboxStyleElements.ToArray())
        );
    }
    
    private XElement CreateBorderElement(string borderName, BorderStyle border)
    {
        var elements = new List<object>
        {
            RdlNamespaces.RdlElement("Style", "Solid")
        };
        
        if (border.Width > 0)
        {
            elements.Add(RdlNamespaces.RdlElement("Width", $"{border.Width}pt"));
        }
        
        // Filter out invalid color values like "auto" which are valid in Word but not in RDLC
        if (!string.IsNullOrEmpty(border.Color) && 
            !border.Color.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure color is in valid format (#RRGGBB or named color)
            var color = NormalizeColor(border.Color);
            if (!string.IsNullOrEmpty(color))
            {
                elements.Add(RdlNamespaces.RdlElement("Color", color));
            }
        }
        
        return RdlNamespaces.RdlElement(borderName, elements.ToArray());
    }
    
    /// <summary>
    /// Normalizes color values to valid RDLC format
    /// </summary>
    private static string? NormalizeColor(string color)
    {
        if (string.IsNullOrEmpty(color))
            return null;
            
        // Skip invalid/special values
        if (color.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            color.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;
            
        // If already has # prefix, return as-is
        if (color.StartsWith('#'))
            return color;
            
        // If it's a 6-digit hex without #, add it
        if (color.Length == 6 && color.All(c => char.IsAsciiHexDigit(c)))
            return $"#{color}";
            
        // Return as-is for named colors (Black, White, etc.)
        return color;
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

    private XElement CreateHeaderCell(string headerText)
    {
        var name = $"HeaderCell_{Guid.NewGuid():N}".Substring(0, 20);
        
        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents",
                RdlNamespaces.RdlElement("Textbox",
                    new XAttribute("Name", name),
                    RdlNamespaces.RdlElement("CanGrow", "true"),
                    RdlNamespaces.RdlElement("Paragraphs",
                        RdlNamespaces.RdlElement("Paragraph",
                            RdlNamespaces.RdlElement("TextRuns",
                                RdlNamespaces.RdlElement("TextRun",
                                    RdlNamespaces.RdlElement("Value", headerText),
                                    RdlNamespaces.RdlElement("Style",
                                        RdlNamespaces.RdlElement("FontWeight", "Bold")
                                    )
                                )
                            ),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdElement("DefaultName", name),
                    RdlNamespaces.RdlElement("Style",
                        RdlNamespaces.RdlElement("BackgroundColor", "LightGray"),
                        RdlNamespaces.RdlElement("PaddingLeft", "2pt"),
                        RdlNamespaces.RdlElement("PaddingRight", "2pt")
                    )
                )
            )
        );
    }

    private XElement CreateDataCell(string fieldName)
    {
        var name = $"DataCell_{Guid.NewGuid():N}".Substring(0, 20);
        
        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents",
                RdlNamespaces.RdlElement("Textbox",
                    new XAttribute("Name", name),
                    RdlNamespaces.RdlElement("CanGrow", "true"),
                    RdlNamespaces.RdlElement("Paragraphs",
                        RdlNamespaces.RdlElement("Paragraph",
                            RdlNamespaces.RdlElement("TextRuns",
                                RdlNamespaces.RdlElement("TextRun",
                                    RdlNamespaces.RdlElement("Value", $"=Fields!{fieldName}.Value"),
                                    RdlNamespaces.RdlElement("Style")
                                )
                            ),
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdElement("DefaultName", name),
                    RdlNamespaces.RdlElement("Style",
                        RdlNamespaces.RdlElement("PaddingLeft", "2pt"),
                        RdlNamespaces.RdlElement("PaddingRight", "2pt")
                    )
                )
            )
        );
    }

    private string GetCellContent(TableCell cell)
    {
        if (cell.Content == null || cell.Content.Count == 0)
        {
            return string.Empty;
        }

        // Extract text from first content element and sanitize for XML
        var first = cell.Content[0];
        if (first == null)
        {
            return string.Empty;
        }
        
        var rawText = first switch
        {
            TextElement text => text.Text ?? string.Empty,
            ParagraphElement para => para.Runs != null 
                ? string.Join("", para.Runs.Where(r => r != null).Select(r => r.Text ?? string.Empty))
                : string.Empty,
            _ => string.Empty
        };
        return RdlNamespaces.SanitizeXmlString(rawText);
    }

    private XElement CreateTablixColumnHierarchy(int columnCount)
    {
        var members = Enumerable.Range(0, columnCount)
            .Select(_ => RdlNamespaces.RdlElement("TablixMember"))
            .ToArray();

        return RdlNamespaces.RdlElement("TablixColumnHierarchy",
            RdlNamespaces.RdlElement("TablixMembers", members)
        );
    }

    private XElement CreateTablixColumnHierarchyFromStructure(TableStructure structure)
    {
        var members = Enumerable.Range(0, structure.ColumnCount)
            .Select(_ => RdlNamespaces.RdlElement("TablixMember"))
            .ToArray();

        return RdlNamespaces.RdlElement("TablixColumnHierarchy",
            RdlNamespaces.RdlElement("TablixMembers", members)
        );
    }

    private XElement CreateTablixRowHierarchy(int rowCount)
    {
        var members = Enumerable.Range(0, rowCount)
            .Select(_ => RdlNamespaces.RdlElement("TablixMember"))
            .ToArray();

        return RdlNamespaces.RdlElement("TablixRowHierarchy",
            RdlNamespaces.RdlElement("TablixMembers", members)
        );
    }

    private XElement CreateTablixRowHierarchyFromStructure(TableStructure structure, DataSetBinding binding)
    {
        var members = new List<XElement>();

        // Header row (static)
        if (structure.HasHeaderRow)
        {
            members.Add(RdlNamespaces.RdlElement("TablixMember",
                RdlNamespaces.RdlElement("KeepWithGroup", "After")
            ));
        }

        // Data row (repeating)
        var detailMember = RdlNamespaces.RdlElement("TablixMember",
            RdlNamespaces.RdlElement("Group",
                new XAttribute("Name", "Details")
            )
        );

        // Add grouping if specified
        if (!string.IsNullOrEmpty(structure.GroupByField))
        {
            detailMember = RdlNamespaces.RdlElement("TablixMember",
                RdlNamespaces.RdlElement("Group",
                    new XAttribute("Name", $"Group_{structure.GroupByField}"),
                    RdlNamespaces.RdlElement("GroupExpressions",
                        RdlNamespaces.RdlElement("GroupExpression", $"=Fields!{structure.GroupByField}.Value")
                    )
                ),
                RdlNamespaces.RdlElement("TablixMembers",
                    RdlNamespaces.RdlElement("TablixMember",
                        RdlNamespaces.RdlElement("Group",
                            new XAttribute("Name", "Details")
                        )
                    )
                )
            );
        }

        members.Add(detailMember);

        return RdlNamespaces.RdlElement("TablixRowHierarchy",
            RdlNamespaces.RdlElement("TablixMembers", members.ToArray())
        );
    }
}
