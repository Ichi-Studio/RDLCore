using System.Xml.Linq;

namespace RdlCore.Generation.Schema;

/// <summary>
/// Defines RDL 2016 namespaces used in RDLC files
/// </summary>
public static class RdlNamespaces
{
    /// <summary>
    /// Primary RDL 2016 namespace
    /// </summary>
    public static readonly XNamespace Rdl = 
        "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

    /// <summary>
    /// Report Designer namespace
    /// </summary>
    public static readonly XNamespace ReportDesigner = 
        "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    /// <summary>
    /// Default namespace alias for RDL
    /// </summary>
    public const string RdlPrefix = "";

    /// <summary>
    /// Namespace alias for Report Designer
    /// </summary>
    public const string RdPrefix = "rd";

    /// <summary>
    /// Gets the default namespace declaration for RDL documents
    /// </summary>
    public static XAttribute[] GetDefaultNamespaceAttributes()
    {
        return new[]
        {
            new XAttribute("xmlns", Rdl.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "rd", ReportDesigner.NamespaceName)
        };
    }

    /// <summary>
    /// Creates an element in the RDL namespace
    /// </summary>
    public static XElement RdlElement(string name, params object[] content)
    {
        return new XElement(Rdl + name, content);
    }

    /// <summary>
    /// Creates an element in the Report Designer namespace
    /// </summary>
    public static XElement RdElement(string name, params object[] content)
    {
        return new XElement(ReportDesigner + name, content);
    }

    /// <summary>
    /// Removes invalid XML 1.0 characters from a string.
    /// XML 1.0 only allows: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
    /// </summary>
    public static string SanitizeXmlString(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (IsValidXmlChar(ch))
            {
                sb.Append(ch);
            }
            // Invalid chars (like 0x0C Form Feed) are removed
        }
        return sb.ToString();
    }

    /// <summary>
    /// Checks if a character is valid in XML 1.0
    /// </summary>
    private static bool IsValidXmlChar(char ch)
    {
        return ch == 0x9 ||
               ch == 0xA ||
               ch == 0xD ||
               (ch >= 0x20 && ch <= 0xD7FF) ||
               (ch >= 0xE000 && ch <= 0xFFFD);
    }
}
