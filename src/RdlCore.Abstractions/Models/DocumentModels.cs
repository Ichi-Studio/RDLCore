using RdlCore.Abstractions.Enums;

namespace RdlCore.Abstractions.Models;

/// <summary>
/// Represents the structure of a parsed document
/// </summary>
/// <param name="Type">The document type</param>
/// <param name="Pages">The list of page elements</param>
/// <param name="LogicalElements">The list of logical elements</param>
/// <param name="Metadata">Document metadata</param>
public record DocumentStructureModel(
    Enums.DocumentType Type,
    IReadOnlyList<PageElement> Pages,
    IReadOnlyList<LogicalElement> LogicalElements,
    DocumentMetadata Metadata);

/// <summary>
/// Represents document metadata
/// </summary>
public record DocumentMetadata(
    string? Title,
    string? Author,
    string? Subject,
    DateTime? CreatedDate,
    DateTime? ModifiedDate,
    int PageCount,
    string? FileName);

/// <summary>
/// Represents a page element in the document
/// </summary>
public record PageElement(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<ContentElement> Elements);

/// <summary>
/// Base class for content elements
/// </summary>
public abstract record ContentElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex);

/// <summary>
/// Represents a bounding box for layout positioning
/// </summary>
/// <param name="Left">Left position in points</param>
/// <param name="Top">Top position in points</param>
/// <param name="Width">Width in points</param>
/// <param name="Height">Height in points</param>
public record BoundingBox(double Left, double Top, double Width, double Height)
{
    /// <summary>Right edge position</summary>
    public double Right => Left + Width;
    
    /// <summary>Bottom edge position</summary>
    public double Bottom => Top + Height;
    
    /// <summary>
    /// Converts points to inches (72 points = 1 inch)
    /// </summary>
    public BoundingBox ToInches() => new(
        Left / 72.0,
        Top / 72.0,
        Width / 72.0,
        Height / 72.0);
}

/// <summary>
/// Represents a logical element in the document
/// </summary>
public record LogicalElement(
    string Id,
    Enums.LogicalRole Role,
    BoundingBox Bounds,
    string? Content,
    IReadOnlyDictionary<string, string>? Properties);

/// <summary>
/// Represents a text element
/// </summary>
public record TextElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    string Text,
    TextStyle Style) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// Represents text styling
/// </summary>
public record TextStyle(
    string FontFamily,
    double FontSize,
    bool IsBold,
    bool IsItalic,
    bool IsUnderline,
    string? Color,
    string? BackgroundColor,
    string? HorizontalAlignment,
    string? VerticalAlignment);

/// <summary>
/// Represents a table element
/// </summary>
public record TableElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<TableRow> Rows) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// Represents a table row
/// </summary>
public record TableRow(
    int RowIndex,
    double Height,
    IReadOnlyList<TableCell> Cells);

/// <summary>
/// Represents a table cell
/// </summary>
public record TableCell(
    int RowIndex,
    int ColumnIndex,
    int RowSpan,
    int ColSpan,
    double Width,
    IReadOnlyList<ContentElement> Content,
    TableCellStyle? Style);

/// <summary>
/// Represents table cell styling
/// </summary>
public record TableCellStyle(
    string? BackgroundColor,
    BorderStyle? TopBorder,
    BorderStyle? BottomBorder,
    BorderStyle? LeftBorder,
    BorderStyle? RightBorder,
    string? VerticalAlignment,
    PaddingStyle? Padding);

/// <summary>
/// Represents border styling
/// </summary>
public record BorderStyle(
    string Style,
    double Width,
    string? Color);

/// <summary>
/// Represents padding
/// </summary>
public record PaddingStyle(
    double Top,
    double Bottom,
    double Left,
    double Right);

/// <summary>
/// Represents an image element
/// </summary>
public record ImageElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    byte[] ImageData,
    string MimeType,
    string? AlternateText) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// Represents a paragraph element
/// </summary>
public record ParagraphElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    IReadOnlyList<TextRun> Runs,
    ParagraphStyle Style) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// Represents a text run within a paragraph
/// </summary>
public record TextRun(
    string Text,
    TextStyle Style,
    FieldCode? FieldCode);

/// <summary>
/// Represents paragraph styling
/// </summary>
public record ParagraphStyle(
    double? LineSpacing,
    double? SpaceBefore,
    double? SpaceAfter,
    double? FirstLineIndent,
    string? Alignment);
