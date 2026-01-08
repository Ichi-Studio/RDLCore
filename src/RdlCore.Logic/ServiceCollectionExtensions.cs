using RdlCore.Logic.Extraction;
using RdlCore.Logic.Translation;
using Microsoft.Extensions.DependencyInjection;

namespace RdlCore.Logic;

/// <summary>
/// Extension methods for registering logic services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds logic extraction and translation services
    /// </summary>
    public static IServiceCollection AddRdlCoreLogic(this IServiceCollection services)
    {
        // Extraction services
        services.AddSingleton<FieldCodeParser>();
        services.AddSingleton<ConditionalAnalyzer>();
        services.AddSingleton<AstBuilder>();

        // Translation services
        services.AddSingleton<VbExpressionGenerator>();
        services.AddSingleton<ExpressionOptimizer>();
        services.AddSingleton<SandboxValidator>();

        // Main services
        services.AddSingleton<ILogicDecompositionService, LogicDecompositionService>();
        services.AddSingleton<ILogicTranslationService, LogicTranslationService>();

        return services;
    }
}
