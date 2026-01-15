namespace RdlCore.Logic.Extraction;

/// <summary>
/// Parses field codes from Word documents and builds AST representations
/// </summary>
public partial class FieldCodeParser
{
    private readonly ILogger<FieldCodeParser> _logger;

    public FieldCodeParser(ILogger<FieldCodeParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a field code string into an AST
    /// </summary>
    public AbstractSyntaxTree Parse(FieldCode fieldCode)
    {
        _logger.LogDebug("Parsing field code: {RawCode}", fieldCode.RawCode);

        return fieldCode.Type switch
        {
            FieldCodeType.MergeField => ParseMergeField(fieldCode),
            FieldCodeType.If => ParseIfField(fieldCode),
            FieldCodeType.Date => ParseDateField(fieldCode),
            FieldCodeType.Page => ParsePageField(fieldCode),
            FieldCodeType.NumPages => ParseNumPagesField(fieldCode),
            FieldCodeType.Formula => ParseFormula(fieldCode),
            _ => CreateLiteralNode(fieldCode.RawCode)
        };
    }

    private AbstractSyntaxTree ParseMergeField(FieldCode fieldCode)
    {
        var fieldName = fieldCode.FieldName ?? ExtractMergeFieldName(fieldCode.RawCode);
        
        if (string.IsNullOrEmpty(fieldName))
        {
            throw new ExpressionSyntaxException(
                "Unable to extract field name from MERGEFIELD",
                fieldCode.RawCode);
        }

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.FieldReference,
            Value: fieldName,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(
                SourceText: fieldCode.RawCode,
                StartPosition: null,
                EndPosition: null,
                DataType: "String"));
    }

    private AbstractSyntaxTree ParseIfField(FieldCode fieldCode)
    {
        // Parse: IF condition true_value false_value
        var match = IfFieldRegex().Match(fieldCode.RawCode);
        
        if (!match.Success)
        {
            _logger.LogWarning("Unable to parse IF field: {RawCode}", fieldCode.RawCode);
            return CreateLiteralNode(fieldCode.RawCode);
        }

        var condition = ParseCondition(match.Groups["condition"].Value.Trim());
        var trueValue = ParseValue(match.Groups["true"].Value.Trim());
        var falseValue = match.Groups["false"].Success 
            ? ParseValue(match.Groups["false"].Value.Trim()) 
            : null;

        var children = new List<AbstractSyntaxTree> { condition, trueValue };
        if (falseValue != null)
        {
            children.Add(falseValue);
        }

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.Conditional,
            Value: "IIf",
            Children: children,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(
                SourceText: fieldCode.RawCode,
                StartPosition: null,
                EndPosition: null,
                DataType: null));
    }

    private AbstractSyntaxTree ParseCondition(string condition)
    {
        // Try to parse comparison: field operator value
        var comparisonMatch = ComparisonRegex().Match(condition);
        
        if (comparisonMatch.Success)
        {
            var left = ParseValue(comparisonMatch.Groups["left"].Value.Trim());
            var op = ParseOperator(comparisonMatch.Groups["op"].Value.Trim());
            var right = ParseValue(comparisonMatch.Groups["right"].Value.Trim());

            return new AbstractSyntaxTree(
                NodeType: AstNodeType.BinaryOperation,
                Value: null,
                Children: new[] { left, right },
                OperatorSymbol: op,
                Metadata: new AstNodeMetadata(condition, null, null, "Boolean"));
        }

        // Fallback to literal
        return CreateLiteralNode(condition);
    }

    private AbstractSyntaxTree ParseValue(string value)
    {
        // Check if it's a field reference
        var mergeMatch = MergeFieldInValueRegex().Match(value);
        if (mergeMatch.Success)
        {
            return new AbstractSyntaxTree(
                NodeType: AstNodeType.FieldReference,
                Value: mergeMatch.Groups[1].Value,
                Children: null,
                OperatorSymbol: null,
                Metadata: new AstNodeMetadata(value, null, null, "String"));
        }

        // Check if it's a quoted string
        var quotedMatch = QuotedStringRegex().Match(value);
        if (quotedMatch.Success)
        {
            return CreateLiteralNode(quotedMatch.Groups[1].Value);
        }

        // Check if it's a number
        if (double.TryParse(value, out var number))
        {
            return new AbstractSyntaxTree(
                NodeType: AstNodeType.Literal,
                Value: number,
                Children: null,
                OperatorSymbol: null,
                Metadata: new AstNodeMetadata(value, null, null, "Number"));
        }

        return CreateLiteralNode(value);
    }

    private string ParseOperator(string op)
    {
        return op switch
        {
            "=" => "=",
            "<>" or "!=" => "<>",
            ">" => ">",
            "<" => "<",
            ">=" => ">=",
            "<=" => "<=",
            _ => op
        };
    }

    private AbstractSyntaxTree ParseDateField(FieldCode fieldCode)
    {
        var format = fieldCode.Switches?.GetValueOrDefault("@") ?? "yyyy-MM-dd";
        
        return new AbstractSyntaxTree(
            NodeType: AstNodeType.FunctionCall,
            Value: "Format",
            Children: new[]
            {
                new AbstractSyntaxTree(
                    AstNodeType.GlobalReference,
                    "ExecutionTime",
                    null, null, null),
                CreateLiteralNode(format)
            },
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(fieldCode.RawCode, null, null, "String"));
    }

    private AbstractSyntaxTree ParsePageField(FieldCode fieldCode)
    {
        return new AbstractSyntaxTree(
            NodeType: AstNodeType.GlobalReference,
            Value: "PageNumber",
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(fieldCode.RawCode, null, null, "Integer"));
    }

    private AbstractSyntaxTree ParseNumPagesField(FieldCode fieldCode)
    {
        return new AbstractSyntaxTree(
            NodeType: AstNodeType.GlobalReference,
            Value: "TotalPages",
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(fieldCode.RawCode, null, null, "Integer"));
    }

    private AbstractSyntaxTree ParseFormula(FieldCode fieldCode)
    {
        var expression = fieldCode.RawCode.TrimStart('=', ' ');
        
        // This is a simplified parser - a real implementation would be more sophisticated
        return new AbstractSyntaxTree(
            NodeType: AstNodeType.Literal,
            Value: expression,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(fieldCode.RawCode, null, null, null));
    }

    private AbstractSyntaxTree CreateLiteralNode(string value)
    {
        return new AbstractSyntaxTree(
            NodeType: AstNodeType.Literal,
            Value: value,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(value, null, null, "String"));
    }

    private static string? ExtractMergeFieldName(string rawCode)
    {
        var match = MergeFieldNameRegex().Match(rawCode);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"IF\s+(?<condition>.+?)\s+(?<true>""[^""]*""|[^\s""]+)(?:\s+(?<false>""[^""]*""|[^\s""]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex IfFieldRegex();

    [GeneratedRegex(@"(?<left>.+?)\s*(?<op>=|<>|!=|>=|<=|>|<)\s*(?<right>.+)")]
    private static partial Regex ComparisonRegex();

    [GeneratedRegex(@"\{\s*MERGEFIELD\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex MergeFieldInValueRegex();

    [GeneratedRegex(@"^""(.*)""$")]
    private static partial Regex QuotedStringRegex();

    [GeneratedRegex(@"MERGEFIELD\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex MergeFieldNameRegex();
}
