namespace RdlCore.Generation;

/// <summary>
/// Extension methods for registering generation services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RDLC generation services
    /// </summary>
    public static IServiceCollection AddRdlCoreGeneration(this IServiceCollection services)
    {
        // Schema services
        services.AddSingleton<RdlDocumentBuilder>();

        // Component generators
        services.AddSingleton<TextboxGenerator>();
        services.AddSingleton<TablixGenerator>();

        // Main service
        services.AddSingleton<ISchemaSynthesisService, SchemaSynthesisService>();

        return services;
    }
}
