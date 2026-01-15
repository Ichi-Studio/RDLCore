namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 逻辑分解服务 - 转换管道的第 2 阶段
/// </summary>
public interface ILogicDecompositionService
{
    /// <summary>
    /// 从文档结构中提取域代码
    /// </summary>
    /// <param name="model">文档结构模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逻辑提取结果</returns>
    Task<LogicExtractionResult> ExtractFieldCodesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从表达式构建抽象语法树
    /// </summary>
    /// <param name="expression">要解析的表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析的 AST</returns>
    Task<AbstractSyntaxTree> BuildAstAsync(
        string expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 识别逻辑中的条件分支
    /// </summary>
    /// <param name="result">逻辑提取结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>识别的条件分支</returns>
    Task<IEnumerable<ConditionalBranch>> IdentifyConditionsAsync(
        LogicExtractionResult result,
        CancellationToken cancellationToken = default);
}
