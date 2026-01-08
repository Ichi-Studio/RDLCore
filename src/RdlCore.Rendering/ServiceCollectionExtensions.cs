using RdlCore.Rendering.Validation;
using Microsoft.Extensions.DependencyInjection;

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

        // Main service
        services.AddSingleton<IValidationService, ValidationService>();

        return services;
    }
}
