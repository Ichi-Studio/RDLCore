namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 用于处理人工干预请求的服务
/// </summary>
public interface IHumanInterventionHandler
{
    /// <summary>
    /// 请求人工干预
    /// </summary>
    /// <param name="request">干预请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>干预响应</returns>
    Task<InterventionResponse> RequestInterventionAsync(
        InterventionRequest request,
        CancellationToken cancellationToken = default);
}
