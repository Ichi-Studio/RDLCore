namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 文档感知服务 - 转换管道的第 1 阶段
/// </summary>
public interface IDocumentPerceptionService
{
    /// <summary>
    /// 分析文档流并提取其结构
    /// </summary>
    /// <param name="documentStream">要分析的文档流</param>
    /// <param name="type">文档类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档结构模型</returns>
    Task<DocumentStructureModel> AnalyzeAsync(
        Stream documentStream, 
        DocumentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 识别文档中元素的逻辑角色
    /// </summary>
    /// <param name="model">文档结构模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>识别的逻辑元素</returns>
    Task<IEnumerable<LogicalElement>> IdentifyLogicalRolesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从文档中提取布局信息
    /// </summary>
    /// <param name="model">文档结构模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>元素的边界框</returns>
    Task<IEnumerable<BoundingBox>> ExtractLayoutAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);
}
