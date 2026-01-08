using Microsoft.Extensions.DependencyInjection;

namespace RdlCore.Agent;

/// <summary>
/// Extension methods for registering agent services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds agent orchestration services
    /// </summary>
    public static IServiceCollection AddRdlCoreAgent(this IServiceCollection services)
    {
        services.AddSingleton<ProgressReporter>();
        services.AddSingleton<IHumanInterventionHandler, HumanInterventionHandler>();
        services.AddSingleton<IConversionPipelineService, ConversionPipeline>();

        return services;
    }

    /// <summary>
    /// Adds agent services with custom intervention callback
    /// </summary>
    public static IServiceCollection AddRdlCoreAgent(
        this IServiceCollection services,
        Func<Abstractions.Models.InterventionRequest, Task<Abstractions.Models.InterventionResponse>> interventionCallback)
    {
        services.AddSingleton<ProgressReporter>();
        services.AddSingleton<IHumanInterventionHandler>(sp =>
            new HumanInterventionHandler(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HumanInterventionHandler>>(),
                interventionCallback));
        services.AddSingleton<IConversionPipelineService, ConversionPipeline>();

        return services;
    }
}
