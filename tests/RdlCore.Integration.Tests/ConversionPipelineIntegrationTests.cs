namespace RdlCore.Integration.Tests;

public class ConversionPipelineIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public ConversionPipelineIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Configure options
        services.Configure<AxiomRdlCoreOptions>(options =>
        {
            // No need to configure, options are already initialized with defaults
        });

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add all services
        services.AddRdlCoreParsing();
        services.AddRdlCoreLogic();
        services.AddRdlCoreGeneration();
        services.AddRdlCoreRendering();
        services.AddRdlCoreAgent();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Services_ShouldBeRegisteredCorrectly()
    {
        // Assert all services are resolvable
        _serviceProvider.GetService<IDocumentPerceptionService>().Should().NotBeNull();
        _serviceProvider.GetService<ILogicDecompositionService>().Should().NotBeNull();
        _serviceProvider.GetService<ISchemaSynthesisService>().Should().NotBeNull();
        _serviceProvider.GetService<ILogicTranslationService>().Should().NotBeNull();
        _serviceProvider.GetService<IValidationService>().Should().NotBeNull();
        _serviceProvider.GetService<IConversionPipelineService>().Should().NotBeNull();
    }

    [Fact]
    public async Task ValidationService_ValidateSchema_WithEmptyDocument_ShouldFail()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<IValidationService>();
        var doc = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement("Invalid"));

        // Act
        var result = await validationService.ValidateSchemaAsync(doc);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LogicTranslationService_TranslateExpression_ShouldWork()
    {
        // Arrange
        var translationService = _serviceProvider.GetRequiredService<ILogicTranslationService>();
        var ast = new AbstractSyntaxTree(
            AstNodeType.FieldReference,
            "TestField",
            null, null, null);

        // Act
        var expression = await translationService.TranslateToVbExpressionAsync(ast);

        // Assert
        expression.Should().Be("=Fields!TestField.Value");
    }

    [Fact]
    public async Task LogicTranslationService_ValidateExpression_ValidExpression_ShouldPass()
    {
        // Arrange
        var translationService = _serviceProvider.GetRequiredService<ILogicTranslationService>();
        var expression = "=Fields!Name.Value";

        // Act
        var result = await translationService.ValidateExpressionAsync(expression);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaSynthesisService_GenerateDocument_ShouldCreateValidRdl()
    {
        // Arrange
        var synthesisService = _serviceProvider.GetRequiredService<ISchemaSynthesisService>();
        var structure = new DocumentStructureModel(
            DocumentType.Word,
            [],
            [],
            new DocumentMetadata(null, null, null, null, null, 1, null));
        var logic = new LogicExtractionResult(
            [],
            [],
            [],
            []);

        // Act
        var doc = await synthesisService.GenerateRdlDocumentAsync(structure, logic);

        // Assert
        doc.Should().NotBeNull();
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("Report");
    }
}
