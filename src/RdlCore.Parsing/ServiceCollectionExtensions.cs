using RdlCore.Parsing.Common;
using RdlCore.Parsing.Pdf;
using RdlCore.Parsing.Word;
using Microsoft.Extensions.DependencyInjection;

namespace RdlCore.Parsing;

/// <summary>
/// Extension methods for registering parsing services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds parsing services to the service collection
    /// </summary>
    public static IServiceCollection AddRdlCoreParsing(this IServiceCollection services)
    {
        // Common services
        services.AddSingleton<DocumentTypeDetector>();
        services.AddSingleton<BoundingBoxCalculator>();

        // Word parsing
        services.AddSingleton<FieldCodeExtractor>();
        services.AddSingleton<OpenXmlNavigator>();
        services.AddSingleton<DocBinaryConverter>();
        services.AddSingleton<IDocumentParser, WordDocumentParser>();

        // PDF parsing
        services.AddSingleton<LayoutAnalyzer>();
        services.AddSingleton<OcrEngine>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();

        // Main perception service
        services.AddSingleton<IDocumentPerceptionService, DocumentPerceptionService>();
        services.AddSingleton<IPageAnalyzer, DocumentPerceptionService>();

        return services;
    }
}
