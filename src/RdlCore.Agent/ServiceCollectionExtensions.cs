namespace RdlCore.Agent;

/// <summary>
/// 注册代理服务的扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加代理编排服务
    /// </summary>
    public static IServiceCollection AddRdlCoreAgent(this IServiceCollection services)
    {
        services.AddSingleton<ProgressReporter>();
        services.AddSingleton<IHumanInterventionHandler, HumanInterventionHandler>();
        services.AddSingleton<IConversionPipelineService, ConversionPipeline>();

        return services;
    }

    /// <summary>
    /// 添加带有自定义干预回调的代理服务
    /// </summary>
    public static IServiceCollection AddRdlCoreAgent(
        this IServiceCollection services,
        Func<InterventionRequest, Task<InterventionResponse>> interventionCallback)
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
