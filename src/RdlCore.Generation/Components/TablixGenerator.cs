namespace RdlCore.Generation.Components;

/// <summary>
/// Generates Tablix elements for RDL reports
/// </summary>
public class TablixGenerator
{
    private readonly ILogger<TablixGenerator> _logger;
    private readonly TextboxGenerator _textboxGenerator;
    private int _tablixCounter;

    public TablixGenerator(
        ILogger<TablixGenerator> logger,
        TextboxGenerator textboxGenerator)
    {
        _logger = logger;
        _textboxGenerator = textboxGenerator;
    }

    /// <summary>
    /// Creates a Tablix element from a table
    /// </summary>
    public XElement CreateTablix(TableElement table, DataSetBinding? binding, double left, double top)
    {
        var tablixId = ++_tablixCounter;
        var name = $"Tablix{tablixId}";
        var bounds = table.Bounds.ToInches();

        _logger.LogInformation("Creating Tablix '{Name}' with {Rows}x{Cols} dimensions", 
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

        _logger.LogInformation("Creating Tablix '{Name}' from structure with binding to '{DataSet}'",
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
        var rows = table.Rows.Select(row => CreateTablixRow(row, tablixId, expectedColumnCount)).ToArray();

        return RdlNamespaces.RdlElement("TablixBody",
            columns,
            RdlNamespaces.RdlElement("TablixRows", rows)
        );
    }

    private XElement CreateTablixColumnsFromTable(TableElement table, int expectedColumnCount)
    {
        if (table.Rows.Count == 0 || expectedColumnCount == 0)
        {
            return RdlNamespaces.RdlElement("TablixColumns");
        }

        var firstRow = table.Rows[0];
        var columns = new List<XElement>();
        
        for (int i = 0; i < expectedColumnCount; i++)
        {
            var width = i < firstRow.Cells.Count ? firstRow.Cells[i].Width / 72.0 : 1.0;
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
        
        // Ensure we have exactly expectedColumnCount cells
        for (int i = 0; i < expectedColumnCount; i++)
        {
            if (i < row.Cells.Count)
            {
                cells.Add(CreateTablixCell(row.Cells[i], tablixId));
            }
            else
            {
                // Add empty cell if row has fewer cells than expected
                cells.Add(CreateEmptyTablixCell(tablixId, row.Cells.Count > 0 ? row.Cells[0].RowIndex : 0, i));
            }
        }

        return RdlNamespaces.RdlElement("TablixRow",
            RdlNamespaces.RdlElement("Height", $"{row.Height / 72.0:F5}in"),
            RdlNamespaces.RdlElement("TablixCells", cells.ToArray())
        );
    }

    private XElement CreateEmptyTablixCell(int tablixId, int rowIndex, int colIndex)
    {
        var uniqueName = $"Tablix{tablixId}_Cell_{rowIndex}_{colIndex}";
        
        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents",
                _textboxGenerator.CreateSimpleTextbox(
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

        return rows.ToArray();
    }

    private XElement CreateTablixCell(TableCell cell, int tablixId)
    {
        var content = GetCellContent(cell);
        // Use unique name with tablixId to avoid conflicts across multiple Tablix
        var uniqueName = $"Tablix{tablixId}_Cell_{cell.RowIndex}_{cell.ColumnIndex}";
        
        // Determine text alignment based on content and cell style
        var textAlign = DetermineTextAlignment(content, cell);
        
        return RdlNamespaces.RdlElement("TablixCell",
            RdlNamespaces.RdlElement("CellContents",
                CreateStyledTextbox(
                    uniqueName,
                    content,
                    cell.Width / 72.0,
                    textAlign,
                    cell.Style)
            )
        );
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
            _logger.LogDebug("Applying right alignment to date content: {Content}", trimmedContent);
            return "Right";
        }
        
        return null; // No special alignment
    }
    
    /// <summary>
    /// Creates a textbox with style including alignment
    /// </summary>
    private XElement CreateStyledTextbox(string name, string content, double width, string? textAlign, TableCellStyle? cellStyle)
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
                            RdlNamespaces.RdlElement("Style")
                        )
                    ),
                    RdlNamespaces.RdlElement("Style", paragraphStyleElements.ToArray())
                )
            ),
            RdlNamespaces.RdElement("DefaultName", name),
            RdlNamespaces.RdlElement("Style", textboxStyleElements.ToArray())
        );
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
        if (cell.Content.Count == 0)
        {
            return string.Empty;
        }

        // Extract text from first content element and sanitize for XML
        var first = cell.Content[0];
        var rawText = first switch
        {
            TextElement text => text.Text,
            ParagraphElement para => string.Join("", para.Runs.Select(r => r.Text)),
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
