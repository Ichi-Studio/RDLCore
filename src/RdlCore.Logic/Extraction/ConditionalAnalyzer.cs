namespace RdlCore.Logic.Extraction;

/// <summary>
/// Analyzes conditional logic in field codes
/// </summary>
public class ConditionalAnalyzer(
    ILogger<ConditionalAnalyzer> logger,
    FieldCodeParser parser)
{
    private readonly ILogger<ConditionalAnalyzer> _logger = logger;
    private readonly FieldCodeParser _parser = parser;

    /// <summary>
    /// Analyzes field codes to extract conditional branches
    /// </summary>
    public IEnumerable<ConditionalBranch> AnalyzeConditions(IEnumerable<FieldCode> fieldCodes)
    {
        var branchId = 0;
        
        foreach (var fieldCode in fieldCodes.Where(f => f.Type == FieldCodeType.If))
        {
            var ast = _parser.Parse(fieldCode);
            
            if (ast.NodeType != AstNodeType.Conditional || ast.Children == null || ast.Children.Count < 2)
            {
                _logger.LogWarning("Unable to extract conditional from field: {RawCode}", fieldCode.RawCode);
                continue;
            }

            yield return new ConditionalBranch(
                Id: $"cond_{++branchId}",
                Condition: ast.Children[0],
                TrueValue: ast.Children[1],
                FalseValue: ast.Children.Count > 2 ? ast.Children[2] : null,
                SourceLocation: fieldCode.Id);
        }
    }

    /// <summary>
    /// Identifies nested conditionals
    /// </summary>
    public IEnumerable<ConditionalBranch> FlattenNestedConditions(ConditionalBranch branch)
    {
        yield return branch;

        // Check if true/false values contain nested conditionals
        if (branch.TrueValue.NodeType == AstNodeType.Conditional && 
            branch.TrueValue.Children != null && 
            branch.TrueValue.Children.Count >= 2)
        {
            var nested = new ConditionalBranch(
                Id: $"{branch.Id}_nested_true",
                Condition: branch.TrueValue.Children[0],
                TrueValue: branch.TrueValue.Children[1],
                FalseValue: branch.TrueValue.Children.Count > 2 ? branch.TrueValue.Children[2] : null,
                SourceLocation: branch.SourceLocation);

            foreach (var nestedBranch in FlattenNestedConditions(nested))
            {
                yield return nestedBranch;
            }
        }

        if (branch.FalseValue?.NodeType == AstNodeType.Conditional && 
            branch.FalseValue.Children != null && 
            branch.FalseValue.Children.Count >= 2)
        {
            var nested = new ConditionalBranch(
                Id: $"{branch.Id}_nested_false",
                Condition: branch.FalseValue.Children[0],
                TrueValue: branch.FalseValue.Children[1],
                FalseValue: branch.FalseValue.Children.Count > 2 ? branch.FalseValue.Children[2] : null,
                SourceLocation: branch.SourceLocation);

            foreach (var nestedBranch in FlattenNestedConditions(nested))
            {
                yield return nestedBranch;
            }
        }
    }

    /// <summary>
    /// Checks if a conditional can be converted to a Switch statement
    /// </summary>
    public bool CanConvertToSwitch(IEnumerable<ConditionalBranch> branches)
    {
        var branchList = branches.ToList();
        
        if (branchList.Count < 2)
        {
            return false;
        }

        // Check if all conditions test the same field with different values
        var firstField = GetTestedField(branchList[0].Condition);
        if (firstField == null)
        {
            return false;
        }

        return branchList.All(b => GetTestedField(b.Condition) == firstField);
    }

    private string? GetTestedField(AbstractSyntaxTree condition)
    {
        if (condition.NodeType != AstNodeType.BinaryOperation || 
            condition.Children == null || 
            condition.Children.Count == 0)
        {
            return null;
        }

        var left = condition.Children[0];
        if (left?.NodeType == AstNodeType.FieldReference)
        {
            return left.Value?.ToString();
        }

        return null;
    }
}
