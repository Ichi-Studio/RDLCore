namespace RdlCore.Generation.Tests;

public class RdlDocumentBuilderTests
{
    private readonly RdlDocumentBuilder _builder;

    public RdlDocumentBuilderTests()
    {
        var logger = Mock.Of<ILogger<RdlDocumentBuilder>>();
        var options = Options.Create(new AxiomRdlCoreOptions());
        _builder = new RdlDocumentBuilder(logger, options);
    }

    [Fact]
    public void CreateEmptyDocument_ShouldCreateValidStructure()
    {
        // Act
        var doc = _builder.CreateEmptyDocument();

        // Assert
        doc.Should().NotBeNull();
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("Report");
    }

    [Fact]
    public void CreateEmptyDocument_ShouldHaveCorrectNamespace()
    {
        // Act
        var doc = _builder.CreateEmptyDocument();

        // Assert
        doc.Root!.Name.Namespace.Should().Be(RdlNamespaces.Rdl);
    }

    [Fact]
    public void CreateEmptyDocument_ShouldContainReportSections()
    {
        // Act
        var doc = _builder.CreateEmptyDocument();

        // Assert
        var sections = doc.Root!.Element(RdlNamespaces.Rdl + "ReportSections");
        sections.Should().NotBeNull();
    }

    [Fact]
    public void CreateEmptyDocument_WithDataSet_ShouldContainDataSets()
    {
        // Act
        var doc = _builder.CreateEmptyDocument("TestDataSet");

        // Assert
        var dataSets = doc.Root!.Element(RdlNamespaces.Rdl + "DataSets");
        dataSets.Should().NotBeNull();
        
        var dataSet = dataSets!.Element(RdlNamespaces.Rdl + "DataSet");
        dataSet.Should().NotBeNull();
        dataSet!.Attribute("Name")!.Value.Should().Be("TestDataSet");
    }

    [Fact]
    public void CreateReportSections_ShouldContainBody()
    {
        // Act
        var sections = _builder.CreateReportSections();

        // Assert
        var body = sections.Descendants(RdlNamespaces.Rdl + "Body").FirstOrDefault();
        body.Should().NotBeNull();
    }

    [Fact]
    public void CreateReportSections_ShouldContainPageSettings()
    {
        // Act
        var sections = _builder.CreateReportSections();

        // Assert
        var page = sections.Descendants(RdlNamespaces.Rdl + "Page").FirstOrDefault();
        page.Should().NotBeNull();
        
        page!.Element(RdlNamespaces.Rdl + "PageWidth").Should().NotBeNull();
        page.Element(RdlNamespaces.Rdl + "PageHeight").Should().NotBeNull();
    }

    [Fact]
    public void AddDataField_ShouldAddFieldToDataSet()
    {
        // Arrange
        var doc = _builder.CreateEmptyDocument("TestDataSet");

        // Act
        _builder.AddDataField(doc, "TestDataSet", "CustomerName", "System.String");

        // Assert
        var field = doc.Descendants(RdlNamespaces.Rdl + "Field")
            .FirstOrDefault(f => f.Attribute("Name")?.Value == "CustomerName");
        
        field.Should().NotBeNull();
    }
}

public class RdlNamespacesTests
{
    [Fact]
    public void Rdl_ShouldHaveCorrectNamespace()
    {
        RdlNamespaces.Rdl.NamespaceName
            .Should().Be("http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition");
    }

    [Fact]
    public void ReportDesigner_ShouldHaveCorrectNamespace()
    {
        RdlNamespaces.ReportDesigner.NamespaceName
            .Should().Be("http://schemas.microsoft.com/SQLServer/reporting/reportdesigner");
    }

    [Fact]
    public void RdlElement_ShouldCreateElementInCorrectNamespace()
    {
        // Act
        var element = RdlNamespaces.RdlElement("TestElement");

        // Assert
        element.Name.LocalName.Should().Be("TestElement");
        element.Name.Namespace.Should().Be(RdlNamespaces.Rdl);
    }

    [Fact]
    public void RdElement_ShouldCreateElementInDesignerNamespace()
    {
        // Act
        var element = RdlNamespaces.RdElement("TestElement");

        // Assert
        element.Name.LocalName.Should().Be("TestElement");
        element.Name.Namespace.Should().Be(RdlNamespaces.ReportDesigner);
    }
}
