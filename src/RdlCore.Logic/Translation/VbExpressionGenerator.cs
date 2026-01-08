using System.Text;

namespace RdlCore.Logic.Translation;

/// <summary>
/// Generates VBScript expressions from AST for RDLC reports
/// </summary>
public class VbExpressionGenerator
{
    private readonly ILogger<VbExpressionGenerator> _logger;

    public VbExpressionGenerator(ILogger<VbExpressionGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a VB expression string from an AST
    /// </summary>
    public string Generate(AbstractSyntaxTree ast)
    {
        var expression = GenerateNode(ast);
        
        // Ensure expression starts with = for RDLC
        if (!expression.StartsWith("="))
        {
            expression = "=" + expression;
        }

        _logger.LogDebug("Generated expression: {Expression}", expression);
        return expression;
    }

    private string GenerateNode(AbstractSyntaxTree node)
    {
        return node.NodeType switch
        {
            AstNodeType.Literal => GenerateLiteral(node),
            AstNodeType.FieldReference => GenerateFieldReference(node),
            AstNodeType.ParameterReference => GenerateParameterReference(node),
            AstNodeType.GlobalReference => GenerateGlobalReference(node),
            AstNodeType.BinaryOperation => GenerateBinaryOperation(node),
            AstNodeType.UnaryOperation => GenerateUnaryOperation(node),
            AstNodeType.FunctionCall => GenerateFunctionCall(node),
            AstNodeType.Conditional => GenerateConditional(node),
            AstNodeType.Aggregate => GenerateAggregate(node),
            _ => node.Value?.ToString() ?? string.Empty
        };
    }

    private string GenerateLiteral(AbstractSyntaxTree node)
    {
        var value = node.Value;
        
        if (value == null)
        {
            return "Nothing";
        }

        if (value is string str)
        {
            return $"\"{str.Replace("\"", "\"\"")}\"";
        }

        if (value is bool b)
        {
            return b ? "True" : "False";
        }

        if (value is DateTime dt)
        {
            return $"#{dt:M/d/yyyy}#";
        }

        return value.ToString() ?? string.Empty;
    }

    private string GenerateFieldReference(AbstractSyntaxTree node)
    {
        var fieldName = node.Value?.ToString() ?? "Unknown";
        return $"Fields!{fieldName}.Value";
    }

    private string GenerateParameterReference(AbstractSyntaxTree node)
    {
        var paramName = node.Value?.ToString() ?? "Unknown";
        return $"Parameters!{paramName}.Value";
    }

    private string GenerateGlobalReference(AbstractSyntaxTree node)
    {
        var globalName = node.Value?.ToString() ?? "Unknown";
        return $"Globals!{globalName}";
    }

    private string GenerateBinaryOperation(AbstractSyntaxTree node)
    {
        if (node.Children == null || node.Children.Count != 2)
        {
            return string.Empty;
        }

        var left = GenerateNode(node.Children[0]);
        var right = GenerateNode(node.Children[1]);
        var op = TranslateOperator(node.OperatorSymbol ?? "=");

        return $"({left} {op} {right})";
    }

    private string GenerateUnaryOperation(AbstractSyntaxTree node)
    {
        if (node.Children == null || node.Children.Count != 1)
        {
            return string.Empty;
        }

        var operand = GenerateNode(node.Children[0]);
        var op = node.OperatorSymbol ?? "Not";

        return $"{op} {operand}";
    }

    private string GenerateFunctionCall(AbstractSyntaxTree node)
    {
        var functionName = TranslateFunctionName(node.Value?.ToString() ?? "Unknown");
        
        if (node.Children == null || node.Children.Count == 0)
        {
            return $"{functionName}()";
        }

        var args = node.Children.Select(GenerateNode);
        return $"{functionName}({string.Join(", ", args)})";
    }

    private string GenerateConditional(AbstractSyntaxTree node)
    {
        if (node.Children == null || node.Children.Count < 2)
        {
            return string.Empty;
        }

        var condition = GenerateNode(node.Children[0]);
        var trueValue = GenerateNode(node.Children[1]);
        var falseValue = node.Children.Count > 2 
            ? GenerateNode(node.Children[2]) 
            : "Nothing";

        return $"IIf({condition}, {trueValue}, {falseValue})";
    }

    private string GenerateAggregate(AbstractSyntaxTree node)
    {
        var aggregateType = node.Value?.ToString() ?? "Sum";
        
        if (node.Children == null || node.Children.Count == 0)
        {
            return $"{aggregateType}()";
        }

        var field = GenerateNode(node.Children[0]);
        var scope = node.Children.Count > 1 
            ? $", \"{GenerateNode(node.Children[1])}\"" 
            : string.Empty;

        return $"{aggregateType}({field}{scope})";
    }

    private string TranslateOperator(string op)
    {
        return op.ToUpperInvariant() switch
        {
            "=" => "=",
            "<>" or "!=" => "<>",
            ">" => ">",
            "<" => "<",
            ">=" => ">=",
            "<=" => "<=",
            "AND" => "And",
            "OR" => "Or",
            "NOT" => "Not",
            "+" => "+",
            "-" => "-",
            "*" => "*",
            "/" => "/",
            "%" or "MOD" => "Mod",
            _ => op
        };
    }

    private string TranslateFunctionName(string name)
    {
        // Map common function names to VB equivalents
        return name.ToUpperInvariant() switch
        {
            "ISNULL" => "IsNothing",
            "IFNULL" => "If",
            "COALESCE" => "If",
            "CONCAT" => "&",
            "LENGTH" or "LEN" => "Len",
            "SUBSTRING" or "SUBSTR" => "Mid",
            "UPPER" => "UCase",
            "LOWER" => "LCase",
            "TRIM" => "Trim",
            "NOW" or "GETDATE" => "Now",
            "TODAY" => "Today",
            "YEAR" => "Year",
            "MONTH" => "Month",
            "DAY" => "Day",
            "FORMAT" => "Format",
            _ => name
        };
    }
}
