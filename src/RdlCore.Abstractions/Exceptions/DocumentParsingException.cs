namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当文档解析失败时抛出的异常
/// </summary>
public class DocumentParsingException : RdlCoreException
{
    /// <summary>
    /// 文档类型
    /// </summary>
    public DocumentType DocumentType { get; }

    /// <summary>
    /// 创建新的 DocumentParsingException
    /// </summary>
    public DocumentParsingException(string message, DocumentType type)
        : base(message)
    {
        DocumentType = type;
    }

    /// <summary>
    /// 创建带有内部异常的 DocumentParsingException
    /// </summary>
    public DocumentParsingException(string message, DocumentType type, Exception innerException)
        : base(message, innerException)
    {
        DocumentType = type;
    }
}
