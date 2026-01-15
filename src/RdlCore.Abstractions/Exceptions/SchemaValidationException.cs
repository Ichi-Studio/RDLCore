namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当模式验证失败时抛出的异常
/// </summary>
public class SchemaValidationException(IReadOnlyList<ValidationMessage> errors)
    : RdlCoreException($"Schema validation failed with {errors.Count} error(s)")
{
    /// <summary>
    /// 验证错误
    /// </summary>
    public IReadOnlyList<ValidationMessage> ValidationErrors { get; } = errors;
}
