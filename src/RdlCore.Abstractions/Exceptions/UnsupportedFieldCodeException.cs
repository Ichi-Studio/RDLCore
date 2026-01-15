namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当域代码不被支持时抛出的异常
/// </summary>
public class UnsupportedFieldCodeException(FieldCodeType type, string rawCode)
    : RdlCoreException($"Unsupported field code type: {type}")
{
    /// <summary>
    /// 域代码类型
    /// </summary>
    public FieldCodeType FieldCodeType { get; } = type;

    /// <summary>
    /// 原始域代码
    /// </summary>
    public string RawFieldCode { get; } = rawCode;
}
