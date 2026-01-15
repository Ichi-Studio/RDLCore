namespace RdlCore.Abstractions.Interfaces;

/// <summary>
/// 验证服务 - 转换管道的第 5 阶段
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// 验证 RDL 文档模式
    /// </summary>
    /// <param name="rdlDocument">要验证的 RDL 文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模式验证结果</returns>
    Task<SchemaValidationResult> ValidateSchemaAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证 RDL 文档中的所有表达式
    /// </summary>
    /// <param name="rdlDocument">要验证的 RDL 文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表达式验证结果</returns>
    Task<ExpressionValidationResult> ValidateExpressionsAsync(
        XDocument rdlDocument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 比较源文档和渲染文档的视觉输出
    /// </summary>
    /// <param name="sourceImage">源文档图像</param>
    /// <param name="renderedImage">渲染报表图像</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>视觉比较结果</returns>
    Task<VisualComparisonResult> CompareVisualsAsync(
        byte[] sourceImage, 
        byte[] renderedImage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 RDL 文档渲染为 PDF
    /// </summary>
    /// <param name="rdlDocument">要渲染的 RDL 文档</param>
    /// <param name="dataSet">要使用的数据集</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染的 PDF 字节</returns>
    Task<byte[]> RenderToPdfAsync(
        XDocument rdlDocument, 
        System.Data.DataSet dataSet,
        CancellationToken cancellationToken = default);
}
