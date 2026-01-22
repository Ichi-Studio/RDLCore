using System.Data;
using System.Xml.Linq;
using Microsoft.Reporting.NETCore;

namespace RdlCore.Rendering;

internal interface IRdlReportRenderer
{
    byte[] Render(XDocument rdlDocument, DataSet dataSet, string format);
}

internal sealed class RdlReportRenderer : IRdlReportRenderer
{
    private readonly ILogger<RdlReportRenderer> _logger;

    public RdlReportRenderer(ILogger<RdlReportRenderer> logger)
    {
        _logger = logger;
    }

    public byte[] Render(XDocument rdlDocument, DataSet dataSet, string format)
    {
        using var reportDefinitionStream = new MemoryStream();
        rdlDocument.Save(reportDefinitionStream);
        reportDefinitionStream.Position = 0;

        using var localReport = new LocalReport();
        localReport.LoadReportDefinition(reportDefinitionStream);

        foreach (DataTable table in dataSet.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.TableName))
            {
                continue;
            }

            _logger.LogDebug("Adding report data source: {Name} ({Rows} rows)", table.TableName, table.Rows.Count);
            localReport.DataSources.Add(new ReportDataSource(table.TableName, table));
        }

        try
        {
            var renderedBytes = localReport.Render(format);
            _logger.LogInformation("Rendered report: Format={Format}, Bytes={Bytes}", format, renderedBytes.Length);
            return renderedBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report render failed: Format={Format}", format);
            throw;
        }
    }
}
