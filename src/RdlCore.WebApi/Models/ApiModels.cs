using System.ComponentModel.DataAnnotations;

namespace RdlCore.WebApi.Models;

/// <summary>
/// 文档转换请求
/// </summary>
public record ConvertRequest
{
    /// <summary>
    /// 数据集名称
    /// </summary>
    [Required]
    public string DataSetName { get; init; } = "DefaultDataSet";

    /// <summary>
    /// 样式模板（可选）
    /// </summary>
    public string? StyleTemplate { get; init; }

    /// <summary>
    /// 是否详细输出
    /// </summary>
    public bool Verbose { get; init; } = false;
}

/// <summary>
/// 转换响应
/// </summary>
public record ConvertResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 转换耗时（毫秒）
    /// </summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 文档信息
    /// </summary>
    public DocumentInfo? DocumentInfo { get; init; }

    /// <summary>
    /// 转换统计
    /// </summary>
    public ConversionStatistics? Statistics { get; init; }

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// 警告列表
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// 生成的 RDLC 内容（Base64 编码）
    /// </summary>
    public string? RdlcContentBase64 { get; init; }

    /// <summary>
    /// 下载链接（如果保存到服务器）
    /// </summary>
    public string? DownloadUrl { get; init; }
}

/// <summary>
/// 文档信息
/// </summary>
public record DocumentInfo
{
    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// 检测到的文档类型
    /// </summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>
    /// 页数
    /// </summary>
    public int PageCount { get; init; }
}

/// <summary>
/// 转换统计信息
/// </summary>
public record ConversionStatistics
{
    /// <summary>
    /// 文本框数量
    /// </summary>
    public int TextboxCount { get; init; }

    /// <summary>
    /// 表格数量
    /// </summary>
    public int TableCount { get; init; }

    /// <summary>
    /// 图片数量
    /// </summary>
    public int ImageCount { get; init; }

    /// <summary>
    /// 表达式数量
    /// </summary>
    public int ExpressionCount { get; init; }

    /// <summary>
    /// 字段代码数量
    /// </summary>
    public int FieldCodeCount { get; init; }
}

/// <summary>
/// 验证请求
/// </summary>
public record ValidateRequest
{
    /// <summary>
    /// 是否验证表达式
    /// </summary>
    public bool ValidateExpressions { get; init; } = true;

    /// <summary>
    /// 是否严格模式
    /// </summary>
    public bool StrictMode { get; init; } = false;
}

/// <summary>
/// 验证响应
/// </summary>
public record ValidateResponse
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Schema 验证结果
    /// </summary>
    public SchemaValidationInfo? SchemaValidation { get; init; }

    /// <summary>
    /// 表达式验证结果
    /// </summary>
    public ExpressionValidationInfo? ExpressionValidation { get; init; }

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// 警告列表
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}

/// <summary>
/// Schema 验证信息
/// </summary>
public record SchemaValidationInfo
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误数量
    /// </summary>
    public int ErrorCount { get; init; }
}

/// <summary>
/// 表达式验证信息
/// </summary>
public record ExpressionValidationInfo
{
    /// <summary>
    /// 是否全部有效
    /// </summary>
    public bool AllValid { get; init; }

    /// <summary>
    /// 表达式总数
    /// </summary>
    public int TotalExpressions { get; init; }

    /// <summary>
    /// 有效表达式数量
    /// </summary>
    public int ValidExpressions { get; init; }

    /// <summary>
    /// 无效表达式数量
    /// </summary>
    public int InvalidExpressions { get; init; }
}

/// <summary>
/// 验证错误
/// </summary>
public record ValidationError
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 位置信息
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// 建议修复方案
    /// </summary>
    public string? SuggestedFix { get; init; }
}

/// <summary>
/// 文档分析响应
/// </summary>
public record AnalyzeResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 文档信息
    /// </summary>
    public DocumentInfo? DocumentInfo { get; init; }

    /// <summary>
    /// 检测到的元素列表
    /// </summary>
    public List<DetectedElement> Elements { get; init; } = [];

    /// <summary>
    /// 检测到的字段代码
    /// </summary>
    public List<DetectedFieldCode> FieldCodes { get; init; } = [];
}

/// <summary>
/// 检测到的元素
/// </summary>
public record DetectedElement
{
    /// <summary>
    /// 元素类型
    /// </summary>
    public string ElementType { get; init; } = string.Empty;

    /// <summary>
    /// 页码
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// 位置信息
    /// </summary>
    public BoundingBoxInfo? BoundingBox { get; init; }
}

/// <summary>
/// 边界框信息
/// </summary>
public record BoundingBoxInfo
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

/// <summary>
/// 检测到的字段代码
/// </summary>
public record DetectedFieldCode
{
    /// <summary>
    /// 字段类型
    /// </summary>
    public string FieldType { get; init; } = string.Empty;

    /// <summary>
    /// 原始内容
    /// </summary>
    public string OriginalContent { get; init; } = string.Empty;

    /// <summary>
    /// 转换后的表达式
    /// </summary>
    public string? TranslatedExpression { get; init; }
}

/// <summary>
/// 健康检查响应
/// </summary>
public record HealthResponse
{
    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; init; } = "healthy";

    /// <summary>
    /// 版本
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 运行时间
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// 服务状态
    /// </summary>
    public Dictionary<string, bool> Services { get; init; } = [];
}
