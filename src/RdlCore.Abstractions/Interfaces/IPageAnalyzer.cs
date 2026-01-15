namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 异步页面分析接口
/// </summary>
public interface IPageAnalyzer
{
    /// <summary>
    /// 使用异步流异步分析页面
    /// </summary>
    /// <param name="documentStream">文档流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面分析结果的异步可枚举</returns>
    IAsyncEnumerable<PageAnalysisResult> AnalyzePagesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
