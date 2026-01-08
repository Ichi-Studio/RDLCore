namespace RdlCore.Logic.Extraction;

/// <summary>
/// Builds Abstract Syntax Trees from expressions
/// </summary>
public class AstBuilder
{
    private readonly ILogger<AstBuilder> _logger;

    public AstBuilder(ILogger<AstBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds an AST from a simple expression string
    /// </summary>
    public AbstractSyntaxTree Build(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return CreateLiteral(string.Empty);
        }

        expression = expression.Trim();

        // Check for field reference pattern: Fields!Name.Value
        if (expression.StartsWith("Fields!", StringComparison.OrdinalIgnoreCase))
        {
            return ParseFieldReference(expression);
        }

        // Check for parameter reference: Parameters!Name.Value
        if (expression.StartsWith("Parameters!", StringComparison.OrdinalIgnoreCase))
        {
            return ParseParameterReference(expression);
        }

        // Check for global reference: Globals!Name
        if (expression.StartsWith("Globals!", StringComparison.OrdinalIgnoreCase))
        {
            return ParseGlobalReference(expression);
        }

        // Check for function call: FunctionName(args)
        var funcMatch = TryParseFunctionCall(expression);
        if (funcMatch != null)
        {
            return funcMatch;
        }

        // Check for binary operation
        var binaryMatch = TryParseBinaryOperation(expression);
        if (binaryMatch != null)
        {
            return binaryMatch;
        }

        // Default to literal
        return CreateLiteral(expression);
    }

    private AbstractSyntaxTree ParseFieldReference(string expression)
    {
        // Pattern: Fields!FieldName.Value
        var parts = expression.Split('!', '.', StringSplitOptions.RemoveEmptyEntries);
        var fieldName = parts.Length > 1 ? parts[1] : expression;

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.FieldReference,
            Value: fieldName,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(expression, null, null, null));
    }

    private AbstractSyntaxTree ParseParameterReference(string expression)
    {
        var parts = expression.Split('!', '.', StringSplitOptions.RemoveEmptyEntries);
        var paramName = parts.Length > 1 ? parts[1] : expression;

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.ParameterReference,
            Value: paramName,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(expression, null, null, null));
    }

    private AbstractSyntaxTree ParseGlobalReference(string expression)
    {
        var parts = expression.Split('!', StringSplitOptions.RemoveEmptyEntries);
        var globalName = parts.Length > 1 ? parts[1] : expression;

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.GlobalReference,
            Value: globalName,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(expression, null, null, null));
    }

    private AbstractSyntaxTree? TryParseFunctionCall(string expression)
    {
        var openParen = expression.IndexOf('(');
        var closeParen = expression.LastIndexOf(')');

        if (openParen <= 0 || closeParen != expression.Length - 1)
        {
            return null;
        }

        var functionName = expression[..openParen].Trim();
        var argsString = expression[(openParen + 1)..closeParen];
        var args = ParseArguments(argsString);

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.FunctionCall,
            Value: functionName,
            Children: args.Select(Build).ToList(),
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(expression, null, null, null));
    }

    private IEnumerable<string> ParseArguments(string argsString)
    {
        if (string.IsNullOrWhiteSpace(argsString))
        {
            yield break;
        }

        var depth = 0;
        var currentArg = new List<char>();

        foreach (var ch in argsString)
        {
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                yield return new string(currentArg.ToArray()).Trim();
                currentArg.Clear();
                continue;
            }

            currentArg.Add(ch);
        }

        if (currentArg.Count > 0)
        {
            yield return new string(currentArg.ToArray()).Trim();
        }
    }

    private AbstractSyntaxTree? TryParseBinaryOperation(string expression)
    {
        var operators = new[] { " And ", " Or ", ">=", "<=", "<>", "=", ">", "<", "+", "-", "*", "/" };

        foreach (var op in operators)
        {
            var index = expression.IndexOf(op, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && index < expression.Length - op.Length)
            {
                var left = expression[..index].Trim();
                var right = expression[(index + op.Length)..].Trim();

                return new AbstractSyntaxTree(
                    NodeType: AstNodeType.BinaryOperation,
                    Value: null,
                    Children: new[] { Build(left), Build(right) },
                    OperatorSymbol: op.Trim(),
                    Metadata: new AstNodeMetadata(expression, null, null, null));
            }
        }

        return null;
    }

    private AbstractSyntaxTree CreateLiteral(object value)
    {
        var dataType = value switch
        {
            int or long => "Integer",
            float or double or decimal => "Number",
            bool => "Boolean",
            _ => "String"
        };

        return new AbstractSyntaxTree(
            NodeType: AstNodeType.Literal,
            Value: value,
            Children: null,
            OperatorSymbol: null,
            Metadata: new AstNodeMetadata(value?.ToString(), null, null, dataType));
    }
}
