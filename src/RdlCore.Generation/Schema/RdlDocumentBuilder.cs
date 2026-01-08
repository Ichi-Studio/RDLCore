using Microsoft.Extensions.Options;

namespace RdlCore.Generation.Schema;

/// <summary>
/// Builds RDL documents from document models
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
    /// Creates an empty RDL document with basic structure
    /// </summary>
    public XDocument CreateEmptyDocument(string? dataSetName = null)
    {
        // Build content list - only include DataSources/DataSets if dataSetName is provided
        var reportContent = new List<object?>
        {
            RdlNamespaces.GetDefaultNamespaceAttributes()
        };

        // Only add DataSources and DataSets when dataSetName is provided
        // Empty DataSources element violates RDL 2016 Schema
        if (!string.IsNullOrEmpty(dataSetName))
        {
            reportContent.Add(CreateDataSources(dataSetName));
            reportContent.Add(CreateDataSets(dataSetName));
        }

        reportContent.Add(CreateReportSections());

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            RdlNamespaces.RdlElement("Report", reportContent.Where(x => x != null).Cast<object>().ToArray())
        );

        return doc;
    }

    /// <summary>
    /// Creates the DataSources element with a default data source
    /// </summary>
    public XElement CreateDataSources(string dataSetName)
    {
        return RdlNamespaces.RdlElement("DataSources",
            RdlNamespaces.RdlElement("DataSource",
                new XAttribute("Name", $"{dataSetName}DataSource"),
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
                    RdlNamespaces.RdlElement("DataSourceName", $"{dataSetName}DataSource"),
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
    /// Creates the ReportSections element
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
                RdlNamespaces.RdlElement("Width", gen.DefaultPageWidth),
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
    /// Adds a data field to the document
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
    /// Adds a report item to the body
    /// </summary>
    public void AddReportItem(XDocument doc, XElement item)
    {
        var reportItems = doc.Descendants(RdlNamespaces.Rdl + "ReportItems").FirstOrDefault();
        reportItems?.Add(item);
    }

    /// <summary>
    /// Sets the page header
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
    /// Sets the page header with multiple items and custom height
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
    /// Sets the page footer
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
    /// Updates the body height based on content
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
