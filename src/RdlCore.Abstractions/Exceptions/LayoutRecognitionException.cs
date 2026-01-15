namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当布局识别失败时抛出的异常
/// </summary>
public class LayoutRecognitionException(string message, int? pageNumber = null): RdlCoreException(message)
{
    /// <summary>
    /// 发生错误的页码
    /// </summary>
    public int? PageNumber { get; } = pageNumber;
}
