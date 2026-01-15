namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 逻辑翻译服务 - 转换管道的第 4 阶段
/// </summary>
public interface ILogicTranslationService
{
    /// <summary>
    /// 将 AST 翻译为 VB 表达式
    /// </summary>
    /// <param name="ast">要翻译的 AST</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>VB 表达式字符串</returns>
    Task<string> TranslateToVbExpressionAsync(
        AbstractSyntaxTree ast,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证表达式
    /// </summary>
    /// <param name="expression">要验证的表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<ExpressionValidationResult> ValidateExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 优化表达式
    /// </summary>
    /// <param name="expression">要优化的表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>优化后的表达式</returns>
    Task<string> OptimizeExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default);
}
