namespace RdlCore.Generation.Schema;

/// <summary>
/// 从文档模型构建 RDL 文档
/// </summary>
public class RdlDocumentBuilder
{
    private readonly ILogger<RdlDocumentBuilder> _logger;
    private readonly AxiomRdlCoreOptions _options;

    public RdlDocumentBuilder(
        ILogger<RdlDocumentBuilder> logger,
        IOptions<AxiomRdlCoreOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 创建带有基本结构的空 RDL 文档
    /// </summary>
    public XDocument CreateEmptyDocument(string? dataSetName = null)
    {
        // 构建内容列表 - 仅在提供 dataSetName 时包含 DataSources/DataSets
        List<object?> reportContent =
        [
            RdlNamespaces.GetDefaultNamespaceAttributes()
        ];

        // 仅在提供 dataSetName 时添加 DataSources 和 DataSets
        // 空的 DataSources 元素违反 RDL 2016 模式
        if (!string.IsNullOrEmpty(dataSetName))
        {
            reportContent.Add(CreateDataSources(dataSetName));
            reportContent.Add(CreateDataSets(dataSetName));
        }

        reportContent.Add(CreateReportSections());

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            RdlNamespaces.RdlElement("Report", [.. reportContent.Where(x => x != null).Cast<object>()])
        );

        return doc;
    }

    /// <summary>
    /// 使用默认数据源创建 DataSources 元素
    /// </summary>
    public XElement CreateDataSources(string dataSetName)
    {
        return RdlNamespaces.RdlElement("DataSources",
            RdlNamespaces.RdlElement("DataSource",
                new XAttribute("Name", "DataSource"),
                RdlNamespaces.RdElement("DataSourceID", Guid.NewGuid().ToString()),
                RdlNamespaces.RdlElement("ConnectionProperties",
                    RdlNamespaces.RdlElement("DataProvider", "System.Data.DataSet"),
                    RdlNamespaces.RdlElement("ConnectString", "/* Local Report */")
                )
            )
        );
    }

    /// <summary>
    /// Creates the DataSets element with a default placeholder field
    /// RDL 2016 Schema requires Fields element to contain at least one Field
    /// </summary>
    public XElement CreateDataSets(string dataSetName)
    {
        return RdlNamespaces.RdlElement("DataSets",
            RdlNamespaces.RdlElement("DataSet",
                new XAttribute("Name", dataSetName),
                // Query element is required to link DataSet to DataSource
                RdlNamespaces.RdlElement("Query",
                    RdlNamespaces.RdlElement("DataSourceName", "DataSource"),
                    RdlNamespaces.RdlElement("CommandText", "/* Local Report */")
                ),
                RdlNamespaces.RdlElement("Fields",
                    // Add a default placeholder field to satisfy RDL 2016 Schema
                    RdlNamespaces.RdlElement("Field",
                        new XAttribute("Name", "PlaceholderField"),
                        RdlNamespaces.RdlElement("DataField", "PlaceholderField"),
                        RdlNamespaces.RdElement("TypeName", "System.String")
                    )
                ),
                RdlNamespaces.RdElement("DataSetInfo",
                    RdlNamespaces.RdElement("DataSetName", dataSetName)
                )
            )
        );
    }

    /// <summary>
    /// 创建 ReportSections 元素
    /// </summary>
    public XElement CreateReportSections()
    {
        var gen = _options.Generation;
        
        return RdlNamespaces.RdlElement("ReportSections",
            RdlNamespaces.RdlElement("ReportSection",
                RdlNamespaces.RdlElement("Body",
                    RdlNamespaces.RdlElement("ReportItems"),
                    RdlNamespaces.RdlElement("Height", "6in")
                ),
                // 使用可打印宽度（页面宽度 - 左边距 - 右边距）以避免水平分页
                RdlNamespaces.RdlElement("Width", gen.PrintableWidth),
                RdlNamespaces.RdlElement("Page",
                    RdlNamespaces.RdlElement("PageHeight", gen.DefaultPageHeight),
                    RdlNamespaces.RdlElement("PageWidth", gen.DefaultPageWidth),
                    RdlNamespaces.RdlElement("LeftMargin", gen.DefaultMargins.Left),
                    RdlNamespaces.RdlElement("RightMargin", gen.DefaultMargins.Right),
                    RdlNamespaces.RdlElement("TopMargin", gen.DefaultMargins.Top),
                    RdlNamespaces.RdlElement("BottomMargin", gen.DefaultMargins.Bottom)
                )
            )
        );
    }

    /// <summary>
    /// 将数据字段添加到文档
    /// </summary>
    public void AddDataField(XDocument doc, string dataSetName, string fieldName, string dataType)
    {
        var fields = doc.Descendants(RdlNamespaces.Rdl + "DataSet")
            .FirstOrDefault(ds => ds.Attribute("Name")?.Value == dataSetName)?
            .Element(RdlNamespaces.Rdl + "Fields");

        if (fields == null)
        {
            _logger.LogWarning("DataSet '{DataSetName}' not found", dataSetName);
            return;
        }

        fields.Add(RdlNamespaces.RdlElement("Field",
            new XAttribute("Name", fieldName),
            RdlNamespaces.RdlElement("DataField", fieldName),
            RdlNamespaces.RdElement("TypeName", dataType)
        ));
    }

    /// <summary>
    /// 将报表项添加到主体
    /// </summary>
    public void AddReportItem(XDocument doc, XElement item)
    {
        var reportItems = doc.Descendants(RdlNamespaces.Rdl + "ReportItems").FirstOrDefault();
        reportItems?.Add(item);
    }

    /// <summary>
    /// 设置页面页眉
    /// </summary>
    public void SetPageHeader(XDocument doc, XElement headerContent, bool printOnFirst = true, bool printOnLast = true)
    {
        var page = doc.Descendants(RdlNamespaces.Rdl + "Page").FirstOrDefault();
        
        page?.AddFirst(RdlNamespaces.RdlElement("PageHeader",
            RdlNamespaces.RdlElement("Height", "1in"),
            RdlNamespaces.RdlElement("PrintOnFirstPage", printOnFirst.ToString().ToLower()),
            RdlNamespaces.RdlElement("PrintOnLastPage", printOnLast.ToString().ToLower()),
            RdlNamespaces.RdlElement("ReportItems", headerContent)
        ));
    }

    /// <summary>
    /// 使用多个项和自定义高度设置页面页眉
    /// </summary>
    public void SetPageHeader(XDocument doc, XElement[] headerItems, double heightInInches, bool printOnFirst = true, bool printOnLast = true)
    {
        var page = doc.Descendants(RdlNamespaces.Rdl + "Page").FirstOrDefault();
        if (page == null) return;

        var reportItems = RdlNamespaces.RdlElement("ReportItems");
        foreach (var item in headerItems)
        {
            reportItems.Add(item);
        }
        
        page.AddFirst(RdlNamespaces.RdlElement("PageHeader",
            RdlNamespaces.RdlElement("Height", $"{heightInInches:F2}in"),
            RdlNamespaces.RdlElement("PrintOnFirstPage", printOnFirst.ToString().ToLower()),
            RdlNamespaces.RdlElement("PrintOnLastPage", printOnLast.ToString().ToLower()),
            reportItems
        ));
    }

    /// <summary>
    /// 设置页面页脚
    /// </summary>
    public void SetPageFooter(XDocument doc, XElement footerContent, bool printOnFirst = true, bool printOnLast = true)
    {
        var page = doc.Descendants(RdlNamespaces.Rdl + "Page").FirstOrDefault();
        
        page?.Add(RdlNamespaces.RdlElement("PageFooter",
            RdlNamespaces.RdlElement("Height", "0.5in"),
            RdlNamespaces.RdlElement("PrintOnFirstPage", printOnFirst.ToString().ToLower()),
            RdlNamespaces.RdlElement("PrintOnLastPage", printOnLast.ToString().ToLower()),
            RdlNamespaces.RdlElement("ReportItems", footerContent)
        ));
    }

    /// <summary>
    /// 根据内容更新主体高度
    /// </summary>
    public void UpdateBodyHeight(XDocument doc, double heightInInches)
    {
        var height = doc.Descendants(RdlNamespaces.Rdl + "Body")
            .FirstOrDefault()?
            .Element(RdlNamespaces.Rdl + "Height");

        if (height != null)
        {
            height.Value = $"{heightInInches:F2}in";
        }
    }
}
