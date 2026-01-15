namespace RdlCore.Abstractions.Models;

/// <summary>
/// 表示解析文档的结构
/// </summary>
/// <param name="Type">文档类型</param>
/// <param name="Pages">页面元素列表</param>
/// <param name="LogicalElements">逻辑元素列表</param>
/// <param name="Metadata">文档元数据</param>
public record DocumentStructureModel(
    Enums.DocumentType Type,
    IReadOnlyList<PageElement> Pages,
    IReadOnlyList<LogicalElement> LogicalElements,
    DocumentMetadata Metadata);

/// <summary>
/// 表示文档元数据
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
/// 表示文档中的页面元素
/// </summary>
public record PageElement(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<ContentElement> Elements);

/// <summary>
/// 内容元素的基类
/// </summary>
public abstract record ContentElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex);

/// <summary>
/// 表示布局定位的边界框
/// </summary>
/// <param name="Left">左侧位置（点）</param>
/// <param name="Top">顶部位置（点）</param>
/// <param name="Width">宽度（点）</param>
/// <param name="Height">高度（点）</param>
public record BoundingBox(double Left, double Top, double Width, double Height)
{
    /// <summary>右边缘位置</summary>
    public double Right => Left + Width;
    
    /// <summary>底部边缘位置</summary>
    public double Bottom => Top + Height;
    
    /// <summary>
    /// 将点转换为英寸（72 点 = 1 英寸）
    /// </summary>
    public BoundingBox ToInches() => new(
        Left / 72.0,
        Top / 72.0,
        Width / 72.0,
        Height / 72.0);
}

/// <summary>
/// 表示文档中的逻辑元素
/// </summary>
public record LogicalElement(
    string Id,
    Enums.LogicalRole Role,
    BoundingBox Bounds,
    string? Content,
    IReadOnlyDictionary<string, string>? Properties);

/// <summary>
/// 表示文本元素
/// </summary>
public record TextElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    string Text,
    TextStyle Style) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// 表示文本样式
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
/// 表示表格元素
/// </summary>
public record TableElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<TableRow> Rows) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// 表示表格行
/// </summary>
public record TableRow(
    int RowIndex,
    double Height,
    IReadOnlyList<TableCell> Cells);

/// <summary>
/// 表示表格单元格
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
/// 表示表格单元格样式
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
/// 表示边框样式
/// </summary>
public record BorderStyle(
    string Style,
    double Width,
    string? Color);

/// <summary>
/// 表示内边距
/// </summary>
public record PaddingStyle(
    double Top,
    double Bottom,
    double Left,
    double Right);

/// <summary>
/// 表示图像元素
/// </summary>
public record ImageElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    byte[] ImageData,
    string MimeType,
    string? AlternateText) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// 表示段落元素
/// </summary>
public record ParagraphElement(
    string Id,
    BoundingBox Bounds,
    int ZIndex,
    IReadOnlyList<TextRun> Runs,
    ParagraphStyle Style) : ContentElement(Id, Bounds, ZIndex);

/// <summary>
/// 表示段落中的文本运行
/// </summary>
public record TextRun(
    string Text,
    TextStyle Style,
    FieldCode? FieldCode);

/// <summary>
/// 表示段落样式
/// </summary>
public record ParagraphStyle(
    double? LineSpacing,
    double? SpaceBefore,
    double? SpaceAfter,
    double? FirstLineIndent,
    string? Alignment);
