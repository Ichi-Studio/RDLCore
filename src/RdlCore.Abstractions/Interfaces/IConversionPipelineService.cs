namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 用于编排转换管道的服务
/// </summary>
public interface IConversionPipelineService
{
    /// <summary>
    /// 执行完整的转换管道
    /// </summary>
    /// <param name="request">转换请求</param>
    /// <param name="progress">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>转换结果</returns>
    Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
