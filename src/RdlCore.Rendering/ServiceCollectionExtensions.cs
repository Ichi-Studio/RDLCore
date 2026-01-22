using RdlCore.Rendering.Word;

namespace RdlCore.Rendering;

/// <summary>
/// Extension methods for registering rendering services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds rendering and validation services
    /// </summary>
    public static IServiceCollection AddRdlCoreRendering(this IServiceCollection services)
    {
        // Validation services
        services.AddSingleton<SsimCalculator>();
        services.AddSingleton<VisualComparer>();
        services.AddSingleton<IRdlReportRenderer, RdlReportRenderer>();
        services.AddSingleton<IPdfRasterizer, DocnetPdfRasterizer>();
        services.AddSingleton<IVisualDiffService, VisualDiffService>();
        services.AddSingleton<IWordReportService, WordReportService>();
        services.AddSingleton<IWordToPdfConverter, WordToPdfConverter>();

        // Main service
        services.AddSingleton<IValidationService, ValidationService>();

        return services;
    }
}
