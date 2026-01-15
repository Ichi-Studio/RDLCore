namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 模式综合服务 - 转换管道的第 3 阶段
/// </summary>
public interface ISchemaSynthesisService
{
    /// <summary>
    /// 从文档结构和逻辑生成 RDL 文档
    /// </summary>
    /// <param name="documentStructure">文档结构模型</param>
    /// <param name="logic">逻辑提取结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的 RDL 文档</returns>
    Task<XDocument> GenerateRdlDocumentAsync(
        DocumentStructureModel documentStructure, 
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成多个 RDL 文档，源文档中的每个页面/节一个
    /// </summary>
    /// <param name="documentStructure">文档结构模型</param>
    /// <param name="logic">逻辑提取结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RDL 文档信息对象列表</returns>
    Task<IReadOnlyList<RdlDocumentInfo>> GenerateMultipleRdlDocumentsAsync(
        DocumentStructureModel documentStructure,
        LogicExtractionResult logic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为表格创建 Tablix 元素
    /// </summary>
    /// <param name="table">表格结构</param>
    /// <param name="binding">数据集绑定</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Tablix XML 元素</returns>
    Task<XElement> CreateTablixAsync(
        TableStructure table, 
        DataSetBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为段落创建 Textbox 元素
    /// </summary>
    /// <param name="paragraph">段落元素</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Textbox XML 元素</returns>
    Task<XElement> CreateTextboxAsync(
        ParagraphElement paragraph,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证 RDL 文档是否符合模式
    /// </summary>
    /// <param name="rdlDocument">要验证的 RDL 文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ValidateAgainstSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);
}
