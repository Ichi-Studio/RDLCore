using System.Text.RegularExpressions;

namespace RdlCore.Logic.Translation;

/// <summary>
/// Optimizes VB expressions for better performance and readability
/// </summary>
public partial class ExpressionOptimizer
{
    private readonly ILogger<ExpressionOptimizer> _logger;

    public ExpressionOptimizer(ILogger<ExpressionOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Optimizes a VB expression
    /// </summary>
    public string Optimize(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return expression;
        }

        var optimized = expression;

        // Remove unnecessary parentheses
        optimized = RemoveUnnecessaryParentheses(optimized);

        // Simplify double negation
        optimized = SimplifyDoubleNegation(optimized);

        // Simplify constant conditions
        optimized = SimplifyConstantConditions(optimized);

        // Optimize null checks
        optimized = OptimizeNullChecks(optimized);

        if (optimized != expression)
        {
            _logger.LogDebug("Optimized expression from '{Original}' to '{Optimized}'", 
                expression, optimized);
        }

        return optimized;
    }

    private string RemoveUnnecessaryParentheses(string expression)
    {
        // Remove double parentheses: ((x)) -> (x)
        var result = expression;
        var previous = string.Empty;
        
        while (result != previous)
        {
            previous = result;
            result = DoubleParenRegex().Replace(result, "($1)");
        }

        return result;
    }

    private string SimplifyDoubleNegation(string expression)
    {
        // Not Not x -> x
        return DoubleNotRegex().Replace(expression, "$1");
    }

    private string SimplifyConstantConditions(string expression)
    {
        // IIf(True, x, y) -> x
        expression = TrueConditionRegex().Replace(expression, "$1");
        
        // IIf(False, x, y) -> y
        expression = FalseConditionRegex().Replace(expression, "$1");

        return expression;
    }

    private string OptimizeNullChecks(string expression)
    {
        // IsNothing(x) Or x = "" -> IsNothing(x) Or x = ""
        // If(IsNothing(x), default, x) -> If(IsNothing(x), default, x)
        // These are already optimal, but we can simplify nested checks
        
        return expression;
    }

    [GeneratedRegex(@"\(\(([^()]+)\)\)")]
    private static partial Regex DoubleParenRegex();

    [GeneratedRegex(@"Not\s+Not\s+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex DoubleNotRegex();

    [GeneratedRegex(@"IIf\s*\(\s*True\s*,\s*([^,]+)\s*,\s*[^)]+\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex TrueConditionRegex();

    [GeneratedRegex(@"IIf\s*\(\s*False\s*,\s*[^,]+\s*,\s*([^)]+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex FalseConditionRegex();
}
