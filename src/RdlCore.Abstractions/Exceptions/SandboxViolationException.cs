namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当违反沙箱安全时抛出的异常
/// </summary>
public class SandboxViolationException(string expression, IReadOnlyList<string> violatedRules)
    : RdlCoreException($"Expression violates sandbox security rules: {string.Join(", ", violatedRules)}")
{
    /// <summary>
    /// 违反安全的表达式
    /// </summary>
    public string Expression { get; } = expression;

    /// <summary>
    /// 被违反的规则
    /// </summary>
    public IReadOnlyList<string> ViolatedRules { get; } = violatedRules;
}
