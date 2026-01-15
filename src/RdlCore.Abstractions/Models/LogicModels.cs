namespace RdlCore.Abstractions.Models;

/// <summary>
/// 表示从文档提取的域代码
/// </summary>
public record FieldCode(
    string Id,
    FieldCodeType Type,
    string RawCode,
    string? FieldName,
    IReadOnlyDictionary<string, string>? Switches,
    IReadOnlyList<FieldCode>? NestedFields);

/// <summary>
/// 表示逻辑提取的结果
/// </summary>
public record LogicExtractionResult(
    IReadOnlyList<FieldCode> FieldCodes,
    IReadOnlyList<ConditionalBranch> Conditions,
    IReadOnlyList<CalculationFormula> Formulas,
    IReadOnlyList<string> Warnings);

/// <summary>
/// 表示逻辑中的条件分支
/// </summary>
public record ConditionalBranch(
    string Id,
    AbstractSyntaxTree Condition,
    AbstractSyntaxTree TrueValue,
    AbstractSyntaxTree? FalseValue,
    string SourceLocation);

/// <summary>
/// 表示计算公式
/// </summary>
public record CalculationFormula(
    string Id,
    string RawExpression,
    AbstractSyntaxTree ParsedExpression,
    string SourceLocation);

/// <summary>
/// 表示抽象语法树节点
/// </summary>
public record AbstractSyntaxTree(
    AstNodeType NodeType,
    object? Value,
    IReadOnlyList<AbstractSyntaxTree>? Children,
    string? OperatorSymbol,
    AstNodeMetadata? Metadata);

/// <summary>
/// AST 节点的元数据
/// </summary>
public record AstNodeMetadata(
    string? SourceText,
    int? StartPosition,
    int? EndPosition,
    string? DataType);

/// <summary>
/// 表示验证后的表达式，准备用于 RDLC
/// </summary>
public record ValidatedExpression(
    string Expression,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ReferencedFields,
    IReadOnlyList<string> ReferencedParameters);

/// <summary>
/// 表示表达式验证的结果
/// </summary>
public record ExpressionValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationMessage> Messages,
    IReadOnlyList<string> SandboxViolations);

/// <summary>
/// 表示验证消息
/// </summary>
public record ValidationMessage(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Location);

/// <summary>
/// 表示页面分析结果
/// </summary>
public record PageAnalysisResult(
    int PageNumber,
    IReadOnlyList<ContentElement> Elements,
    IReadOnlyList<LogicalElement> LogicalElements,
    double Confidence);
