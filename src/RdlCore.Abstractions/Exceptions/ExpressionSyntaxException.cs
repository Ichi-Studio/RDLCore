namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当表达式语法无效时抛出的异常
/// </summary>
public class ExpressionSyntaxException(string message, string expression, int? position = null): RdlCoreException(message)
{
    /// <summary>
    /// 无效的表达式
    /// </summary>
    public string Expression { get; } = expression;

    /// <summary>
    /// 错误的位置
    /// </summary>
    public int? Position { get; } = position;
}
