namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 文档解析器接口
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// 获取支持的文档类型
    /// </summary>
    DocumentType SupportedType { get; }

    /// <summary>
    /// 解析文档流
    /// </summary>
    /// <param name="stream">文档流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档结构模型</returns>
    Task<DocumentStructureModel> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
