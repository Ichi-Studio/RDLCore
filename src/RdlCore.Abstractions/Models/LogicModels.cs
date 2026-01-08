using RdlCore.Abstractions.Enums;

namespace RdlCore.Abstractions.Models;

/// <summary>
/// Represents a field code extracted from a document
/// </summary>
public record FieldCode(
    string Id,
    FieldCodeType Type,
    string RawCode,
    string? FieldName,
    IReadOnlyDictionary<string, string>? Switches,
    IReadOnlyList<FieldCode>? NestedFields);

/// <summary>
/// Represents the result of logic extraction
/// </summary>
public record LogicExtractionResult(
    IReadOnlyList<FieldCode> FieldCodes,
    IReadOnlyList<ConditionalBranch> Conditions,
    IReadOnlyList<CalculationFormula> Formulas,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Represents a conditional branch in the logic
/// </summary>
public record ConditionalBranch(
    string Id,
    AbstractSyntaxTree Condition,
    AbstractSyntaxTree TrueValue,
    AbstractSyntaxTree? FalseValue,
    string SourceLocation);

/// <summary>
/// Represents a calculation formula
/// </summary>
public record CalculationFormula(
    string Id,
    string RawExpression,
    AbstractSyntaxTree ParsedExpression,
    string SourceLocation);

/// <summary>
/// Represents an Abstract Syntax Tree node
/// </summary>
public record AbstractSyntaxTree(
    AstNodeType NodeType,
    object? Value,
    IReadOnlyList<AbstractSyntaxTree>? Children,
    string? OperatorSymbol,
    AstNodeMetadata? Metadata);

/// <summary>
/// Metadata for AST nodes
/// </summary>
public record AstNodeMetadata(
    string? SourceText,
    int? StartPosition,
    int? EndPosition,
    string? DataType);

/// <summary>
/// Represents a validated expression ready for RDLC
/// </summary>
public record ValidatedExpression(
    string Expression,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ReferencedFields,
    IReadOnlyList<string> ReferencedParameters);

/// <summary>
/// Represents the result of expression validation
/// </summary>
public record ExpressionValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationMessage> Messages,
    IReadOnlyList<string> SandboxViolations);

/// <summary>
/// Represents a validation message
/// </summary>
public record ValidationMessage(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Location);

/// <summary>
/// Represents page analysis result
/// </summary>
public record PageAnalysisResult(
    int PageNumber,
    IReadOnlyList<ContentElement> Elements,
    IReadOnlyList<LogicalElement> LogicalElements,
    double Confidence);
